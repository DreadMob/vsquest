using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsQuest
{
    public static class QuestSystemAdminUtils
    {
        private static void RemoveQuestJournalEntries(ICoreServerAPI sapi, QuestSystem questSystem, IServerPlayer player, string questId)
        {
            if (sapi == null || player == null || string.IsNullOrWhiteSpace(questId)) return;
            if (player.Entity?.WatchedAttributes == null) return;

            string loreCodesKey = $"vsquest:journal:{questId}:lorecodes";
            string[] loreCodes = player.Entity.WatchedAttributes.GetStringArray(loreCodesKey, null);

            // Backfill from quest definition if no per-player tracking is present (e.g. journal entries created before tracking existed)
            if ((loreCodes == null || loreCodes.Length == 0) && questSystem?.QuestRegistry != null && questSystem.QuestRegistry.TryGetValue(questId, out var quest) && quest != null)
            {
                var fromQuest = new List<string>();
                void AddFromActions(System.Collections.Generic.List<ActionWithArgs> actions)
                {
                    if (actions == null) return;
                    foreach (var a in actions)
                    {
                        if (a == null) continue;
                        if (!string.Equals(a.id, "addjournalentry", StringComparison.OrdinalIgnoreCase)) continue;
                        if (a.args == null || a.args.Length < 1) continue;
                        if (!string.IsNullOrWhiteSpace(a.args[0])) fromQuest.Add(a.args[0]);
                    }
                }

                AddFromActions(quest.onAcceptedActions);
                AddFromActions(quest.actionRewards);

                loreCodes = fromQuest.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            }

            if (loreCodes == null || loreCodes.Length == 0)
            {
                player.Entity.WatchedAttributes.RemoveAttribute(loreCodesKey);
                player.Entity.WatchedAttributes.MarkPathDirty(loreCodesKey);
                return;
            }

            ModJournal modJournal = null;
            try
            {
                modJournal = sapi.ModLoader.GetModSystem<ModJournal>();
            }
            catch
            {
            }
            if (modJournal == null) return;

            try
            {
                var t = modJournal.GetType();
                var journalsField = t.GetField("journalsByPlayerUid", BindingFlags.Instance | BindingFlags.NonPublic);
                var channelField = t.GetField("serverChannel", BindingFlags.Instance | BindingFlags.NonPublic);

                var journals = journalsField?.GetValue(modJournal) as Dictionary<string, Journal>;
                var serverChannel = channelField?.GetValue(modJournal) as IServerNetworkChannel;
                if (journals == null || serverChannel == null) return;

                if (!journals.TryGetValue(player.PlayerUID, out var journal) || journal?.Entries == null) return;

                var loreSet = new HashSet<string>(loreCodes.Where(c => !string.IsNullOrWhiteSpace(c)), StringComparer.OrdinalIgnoreCase);
                if (loreSet.Count == 0) return;

                int before = journal.Entries.Count;
                journal.Entries.RemoveAll(e => e != null && !string.IsNullOrWhiteSpace(e.LoreCode) && loreSet.Contains(e.LoreCode));
                if (journal.Entries.Count != before)
                {
                    // Reindex entries and fix chapter EntryId references
                    for (int i = 0; i < journal.Entries.Count; i++)
                    {
                        var entry = journal.Entries[i];
                        if (entry == null) continue;
                        entry.EntryId = i;
                        if (entry.Chapters != null)
                        {
                            for (int j = 0; j < entry.Chapters.Count; j++)
                            {
                                if (entry.Chapters[j] != null) entry.Chapters[j].EntryId = i;
                            }
                        }
                    }

                    // Resync full journal to the client
                    serverChannel.SendPacket(journal, player);
                }
            }
            catch
            {
            }
            finally
            {
                // Always clear tracking so future forgive doesn't keep trying
                player.Entity.WatchedAttributes.RemoveAttribute(loreCodesKey);
                player.Entity.WatchedAttributes.MarkPathDirty(loreCodesKey);
            }
        }

        private static void ClearPerQuestPlayerState(IServerPlayer player, string questId)
        {
            if (player?.Entity?.WatchedAttributes == null) return;

            string cooldownKey = string.Format("vsquest:lastaccepted-{0}", questId);
            player.Entity.WatchedAttributes.RemoveAttribute(cooldownKey);
            player.Entity.WatchedAttributes.MarkPathDirty(cooldownKey);

            // Clear randkill state (legacy + multi-slot)
            player.Entity.WatchedAttributes.RemoveAttribute($"vsquest:randkill:{questId}:code");
            player.Entity.WatchedAttributes.RemoveAttribute($"vsquest:randkill:{questId}:need");
            player.Entity.WatchedAttributes.RemoveAttribute($"vsquest:randkill:{questId}:have");
            player.Entity.WatchedAttributes.RemoveAttribute($"vsquest:randkill:{questId}:slots");
            player.Entity.WatchedAttributes.RemoveAttribute($"vsquest:randkill:{questId}:onprogress");
            player.Entity.WatchedAttributes.RemoveAttribute($"vsquest:randkill:{questId}:oncomplete");

            // We don't know how many slots were used, just clear a reasonable range
            for (int slot = 0; slot < 16; slot++)
            {
                player.Entity.WatchedAttributes.RemoveAttribute($"vsquest:randkill:{questId}:slot{slot}:code");
                player.Entity.WatchedAttributes.RemoveAttribute($"vsquest:randkill:{questId}:slot{slot}:need");
                player.Entity.WatchedAttributes.RemoveAttribute($"vsquest:randkill:{questId}:slot{slot}:have");
            }
        }

        private static void RemoveQuestFromCompletedList(IServerPlayer player, string questId)
        {
            if (player?.Entity?.WatchedAttributes == null) return;

            var completed = player.Entity.WatchedAttributes.GetStringArray("vsquest:playercompleted", new string[0]);
            if (completed == null || completed.Length == 0) return;

            var filtered = completed.Where(id => id != questId).ToArray();
            if (filtered.Length == completed.Length) return;

            player.Entity.WatchedAttributes.SetStringArray("vsquest:playercompleted", filtered);
            player.Entity.WatchedAttributes.MarkAllDirty();
        }

        public static bool ForceCompleteQuestForPlayer(QuestSystem questSystem, IServerPlayer player, string questId, ICoreServerAPI sapi)
        {
            var quests = questSystem.GetPlayerQuests(player.PlayerUID);
            var activeQuest = quests.Find(q => q.questId == questId);
            if (activeQuest == null)
            {
                return false;
            }

            var message = new QuestCompletedMessage { questGiverId = activeQuest.questGiverId, questId = questId };
            bool completed = questSystem.ForceCompleteQuestInternal(player, message, sapi);
            if (completed)
            {
                questSystem.SavePlayerQuests(player.PlayerUID, quests);
            }
            return completed;
        }

        public static bool ForceCompleteActiveQuestForPlayer(QuestSystem questSystem, IServerPlayer player, ICoreServerAPI sapi, out string questId)
        {
            questId = null;
            var quests = questSystem.GetPlayerQuests(player.PlayerUID);
            if (quests == null || quests.Count == 0)
            {
                return false;
            }

            var activeQuest = quests[0];
            if (activeQuest == null || string.IsNullOrEmpty(activeQuest.questId))
            {
                return false;
            }

            questId = activeQuest.questId;
            var message = new QuestCompletedMessage { questGiverId = activeQuest.questGiverId, questId = questId };
            bool completed = questSystem.ForceCompleteQuestInternal(player, message, sapi);
            if (completed)
            {
                questSystem.SavePlayerQuests(player.PlayerUID, quests);
            }
            return completed;
        }

        public static bool ResetQuestForPlayer(QuestSystem questSystem, IServerPlayer player, string questId, ICoreServerAPI sapi = null)
        {
            var quests = questSystem.GetPlayerQuests(player.PlayerUID);
            var activeQuest = quests.Find(q => q.questId == questId);
            bool removed = false;

            if (activeQuest != null)
            {
                quests.Remove(activeQuest);
                removed = true;
            }

            if (sapi != null)
            {
                RemoveQuestJournalEntries(sapi, questSystem, player, questId);
            }
            ClearPerQuestPlayerState(player, questId);
            RemoveQuestFromCompletedList(player, questId);

            questSystem.SavePlayerQuests(player.PlayerUID, quests);
            return removed;
        }

        public static int ResetAllQuestsForPlayer(QuestSystem questSystem, IServerPlayer player, ICoreServerAPI sapi = null)
        {
            var quests = questSystem.GetPlayerQuests(player.PlayerUID);
            int removedCount = quests?.Count ?? 0;

            if (quests != null)
            {
                quests.Clear();
            }

            if (sapi != null)
            {
                foreach (var questId in questSystem.QuestRegistry.Keys.ToList())
                {
                    RemoveQuestJournalEntries(sapi, questSystem, player, questId);
                }
            }

            if (player.Entity?.WatchedAttributes != null)
            {
                // Clear completed list
                player.Entity.WatchedAttributes.RemoveAttribute("vsquest:playercompleted");
                player.Entity.WatchedAttributes.MarkPathDirty("vsquest:playercompleted");

                // Clear cooldowns and any per-quest state we store on the player
                foreach (var questId in questSystem.QuestRegistry.Keys.ToList())
                {
                    ClearPerQuestPlayerState(player, questId);
                }

                player.Entity.WatchedAttributes.MarkAllDirty();
            }

            questSystem.SavePlayerQuests(player.PlayerUID, quests ?? new System.Collections.Generic.List<ActiveQuest>());
            return removedCount;
        }
    }
}
