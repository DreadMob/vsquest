using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using System.Linq;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class QuestLifecycleManager
    {
        private readonly Dictionary<string, Quest> questRegistry;
        private readonly Dictionary<string, IQuestAction> actionRegistry;
        private readonly ICoreAPI api;

        private static string BuildQuestHoverText(string questId)
        {
            if (string.IsNullOrWhiteSpace(questId)) return null;

            string hoverKey = questId + "-hover";
            string hoverText = LocalizationUtils.GetSafe(hoverKey);
            if (!string.IsNullOrWhiteSpace(hoverText)
                && !string.Equals(hoverText, hoverKey, StringComparison.OrdinalIgnoreCase))
            {
                return hoverText.Replace("\r", "").Replace("\"", "&quot;");
            }

            string langKey = questId + "-desc";
            string desc = LocalizationUtils.GetSafe(langKey);
            if (string.IsNullOrWhiteSpace(desc) || string.Equals(desc, langKey, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string[] lines = desc.Replace("\r", "").Split('\n');
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                return line.Trim().Replace("\"", "&quot;");
            }

            return null;
        }

        public QuestLifecycleManager(Dictionary<string, Quest> questRegistry, Dictionary<string, IQuestAction> actionRegistry, ICoreAPI api)
        {
            this.questRegistry = questRegistry;
            this.actionRegistry = actionRegistry;
            this.api = api;
        }

        private static void ClearKillActionTargetProgressOnAccept(IServerPlayer player, Quest quest)
        {
            if (player?.Entity?.WatchedAttributes == null) return;
            if (quest?.actionObjectives == null) return;

            var wa = player.Entity.WatchedAttributes;

            try
            {
                foreach (var ao in quest.actionObjectives)
                {
                    if (ao == null) continue;
                    if (!string.Equals(ao.id, "killactiontarget", StringComparison.OrdinalIgnoreCase)) continue;
                    if (ao.args == null || ao.args.Length < 2) continue;

                    string questId = ao.args[0];
                    string objectiveId = ao.args[1];
                    if (string.IsNullOrWhiteSpace(questId) || string.IsNullOrWhiteSpace(objectiveId)) continue;

                    string key = $"vsquest:killactiontarget:{questId}:{objectiveId}:count";
                    wa.RemoveAttribute(key);
                    wa.MarkPathDirty(key);
                }
            }
            catch
            {
            }
        }

        private List<EventTracker> CreateTrackers(List<Objective> objectives)
        {
            var trackers = new List<EventTracker>();
            if (objectives == null) return trackers;
            foreach (var objective in objectives)
            {
                if (objective == null)
                {
                    trackers.Add(new EventTracker() { count = 0, relevantCodes = new List<string>() });
                    continue;
                }

                var tracker = new EventTracker()
                {
                    count = 0,
                    relevantCodes = objective.validCodes != null
                        ? new List<string>(objective.validCodes)
                        : new List<string>()
                };
                trackers.Add(tracker);
            }
            return trackers;
        }

        public void OnQuestAccepted(IServerPlayer fromPlayer, QuestAcceptedMessage message, ICoreServerAPI sapi, System.Func<string, List<ActiveQuest>> getPlayerQuests)
        {
            if (!questRegistry.TryGetValue(message.questId, out var quest))
            {
                sapi.Logger.Error($"[alegacyvsquest] Could not accept quest with id '{message.questId}' because it was not found in the QuestRegistry.");
                return;
            }

            QuestInteractAtUtil.ResetCompletedInteractAtObjectives(quest, fromPlayer);
            ClearKillActionTargetProgressOnAccept(fromPlayer, quest);
            var playerQuests = getPlayerQuests(fromPlayer.PlayerUID);

            if (playerQuests.Exists(q => q.questId == message.questId))
            {
                return;
            }

            QuestActionObjectiveCompletionUtil.ResetCompletionFlags(quest, fromPlayer);

            var killTrackers = CreateTrackers(quest.killObjectives);
            var blockPlaceTrackers = CreateTrackers(quest.blockPlaceObjectives);
            var blockBreakTrackers = CreateTrackers(quest.blockBreakObjectives);
            var interactTrackers = CreateTrackers(quest.interactObjectives);

            var activeQuest = new ActiveQuest()
            {
                questGiverId = message.questGiverId,
                questId = message.questId,
                killTrackers = killTrackers,
                blockPlaceTrackers = blockPlaceTrackers,
                blockBreakTrackers = blockBreakTrackers,
                interactTrackers = interactTrackers
            };
            playerQuests.Add(activeQuest);
            foreach (var action in quest.onAcceptedActions)
            {
                try
                {
                    actionRegistry[action.id].Execute(sapi, message, fromPlayer, action.args);
                }
                catch (Exception ex)
                {
                    sapi.Logger.Error(string.Format("Action {0} caused an Error in Quest {1}. The Error had the following message: {2}\n Stacktrace:", action.id, quest.id, ex.Message, ex.StackTrace));
                    sapi.SendMessage(fromPlayer, GlobalConstants.InfoLogChatGroup, Lang.Get("alegacyvsquest:quest-action-error", quest.id), EnumChatType.Notification);
                }
            }

            try
            {
                QuestObjectiveAnnounceUtil.AnnounceOnAccept(fromPlayer, message, sapi, quest);
            }
            catch (Exception e)
            {
                sapi.Logger.Warning($"[alegacyvsquest] Error announcing quest objective on accept for quest '{message.questId}': {e.Message}");
            }
        }

        public void OnQuestCompleted(IServerPlayer fromPlayer, QuestCompletedMessage message, ICoreServerAPI sapi, System.Func<string, List<ActiveQuest>> getPlayerQuests)
        {
            var playerQuests = getPlayerQuests(fromPlayer.PlayerUID);
            var activeQuest = playerQuests.Find(item => item.questId == message.questId);
            
            if (activeQuest == null)
            {
                sapi.Logger.Warning($"[alegacyvsquest] Player {fromPlayer.PlayerName} attempted to complete quest '{message.questId}' which is not active.");
                return;
            }
            
            if (activeQuest.IsCompletable(fromPlayer))
            {
                activeQuest.completeQuest(fromPlayer);
                playerQuests.Remove(activeQuest);
                var questgiver = sapi.World.GetEntityById(message.questGiverId);
                RewardPlayer(fromPlayer, message, sapi, questgiver);
                MarkQuestCompleted(fromPlayer, message, questgiver);

                try
                {
                    string title = LocalizationUtils.GetSafe(message.questId + "-title");
                    if (string.IsNullOrWhiteSpace(title) || string.Equals(title, message.questId + "-title", StringComparison.OrdinalIgnoreCase))
                    {
                        title = message.questId;
                    }

                    string playerName = ChatFormatUtil.Font(fromPlayer.PlayerName, "#ffd75e");
                    string hoverText = BuildQuestHoverText(message.questId);
                    string questName = string.IsNullOrWhiteSpace(hoverText)
                        ? ChatFormatUtil.Font(title, "#77ddff")
                        : $"<font color=\"#77ddff\"><qhover text=\"{hoverText}\">{title}</qhover></font>";
                    string text = ChatFormatUtil.PrefixAlert(Lang.Get("alegacyvsquest:quest-completed-broadcast", playerName, questName));
                    GlobalChatBroadcastUtil.BroadcastGeneralChat(sapi, text, EnumChatType.Notification);
                }
                catch
                {
                }

                // Questgiver chain cooldown timestamp (enforced by EntityBehaviorQuestGiver when configured)
                if (fromPlayer?.Entity?.WatchedAttributes != null)
                {
                    string chainKey = EntityBehaviorQuestGiver.ChainCooldownLastCompletedKey(message.questGiverId);
                    fromPlayer.Entity.WatchedAttributes.SetDouble(chainKey, sapi.World.Calendar.TotalDays);
                    fromPlayer.Entity.WatchedAttributes.MarkPathDirty(chainKey);
                }

                if (questRegistry.TryGetValue(message.questId, out var quest))
                {
                    var key = String.Format("alegacyvsquest:lastaccepted-{0}", quest.id);
                    fromPlayer.Entity.WatchedAttributes.SetDouble(key, sapi.World.Calendar.TotalDays);
                    fromPlayer.Entity.WatchedAttributes.MarkPathDirty(key);

                    if (questgiver != null)
                    {
                        var legacyKey = quest.perPlayer ? String.Format("lastaccepted-{0}-{1}", quest.id, fromPlayer.PlayerUID) : String.Format("lastaccepted-{0}", quest.id);
                        questgiver.WatchedAttributes.SetDouble(legacyKey, sapi.World.Calendar.TotalDays);
                        questgiver.WatchedAttributes.MarkPathDirty(legacyKey);
                    }
                }
            }
            else
            {
                sapi.SendMessage(fromPlayer, GlobalConstants.InfoLogChatGroup, LocalizationUtils.GetSafe("alegacyvsquest:quest-could-not-complete"), EnumChatType.Notification);
            }
        }

        public bool ForceCompleteQuest(IServerPlayer fromPlayer, QuestCompletedMessage message, ICoreServerAPI sapi, System.Func<string, List<ActiveQuest>> getPlayerQuests)
        {
            var playerQuests = getPlayerQuests(fromPlayer.PlayerUID);
            var activeQuest = playerQuests.Find(item => item.questId == message.questId);
            if (activeQuest == null)
            {
                return false;
            }

            activeQuest.completeQuest(fromPlayer);
            playerQuests.Remove(activeQuest);

            var questgiver = sapi.World.GetEntityById(message.questGiverId);
            RewardPlayer(fromPlayer, message, sapi, questgiver);
            MarkQuestCompleted(fromPlayer, message, questgiver);

            try
            {
                string title = LocalizationUtils.GetSafe(message.questId + "-title");
                if (string.IsNullOrWhiteSpace(title) || string.Equals(title, message.questId + "-title", StringComparison.OrdinalIgnoreCase))
                {
                    title = message.questId;
                }

                string playerName = ChatFormatUtil.Font(fromPlayer.PlayerName, "#ffd75e");
                string hoverText = BuildQuestHoverText(message.questId);
                string questName = string.IsNullOrWhiteSpace(hoverText)
                    ? ChatFormatUtil.Font(title, "#77ddff")
                    : $"<font color=\"#77ddff\"><qhover text=\"{hoverText}\">{title}</qhover></font>";
                string text = ChatFormatUtil.PrefixAlert(Lang.Get("alegacyvsquest:quest-completed-broadcast", playerName, questName));
                GlobalChatBroadcastUtil.BroadcastGeneralChat(sapi, text, EnumChatType.Notification);
            }
            catch
            {
            }

            // Questgiver chain cooldown timestamp (enforced by EntityBehaviorQuestGiver when configured)
            if (fromPlayer?.Entity?.WatchedAttributes != null)
            {
                string chainKey = EntityBehaviorQuestGiver.ChainCooldownLastCompletedKey(message.questGiverId);
                fromPlayer.Entity.WatchedAttributes.SetDouble(chainKey, sapi.World.Calendar.TotalDays);
                fromPlayer.Entity.WatchedAttributes.MarkPathDirty(chainKey);
            }

            if (questRegistry.TryGetValue(message.questId, out var quest))
            {
                var key = String.Format("alegacyvsquest:lastaccepted-{0}", quest.id);
                fromPlayer.Entity.WatchedAttributes.SetDouble(key, sapi.World.Calendar.TotalDays);
                fromPlayer.Entity.WatchedAttributes.MarkPathDirty(key);

                if (questgiver != null)
                {
                    var legacyKey = quest.perPlayer ? String.Format("lastaccepted-{0}-{1}", quest.id, fromPlayer.PlayerUID) : String.Format("lastaccepted-{0}", quest.id);
                    questgiver.WatchedAttributes.SetDouble(legacyKey, sapi.World.Calendar.TotalDays);
                    questgiver.WatchedAttributes.MarkPathDirty(legacyKey);
                }
            }
            return true;
        }

        private void RewardPlayer(IServerPlayer fromPlayer, QuestCompletedMessage message, ICoreServerAPI sapi, Entity questgiver)
        {
            if (!questRegistry.TryGetValue(message.questId, out var quest))
            {
                sapi.Logger.Error($"[alegacyvsquest] Could not reward player for quest with id '{message.questId}' because it was not found in the QuestRegistry.");
                return;
            }
            foreach (var reward in quest.itemRewards)
            {
                CollectibleObject item = sapi.World.GetItem(new AssetLocation(reward.itemCode));
                if (item == null)
                {
                    item = sapi.World.GetBlock(new AssetLocation(reward.itemCode));
                }
                if (item == null)
                {
                    sapi.Logger.Error($"alegacyvsquest: Quest '{quest.id}' has invalid item reward code '{reward.itemCode}'. Skipping reward.");
                    continue;
                }

                var stack = new ItemStack(item, reward.amount);
                if (!fromPlayer.InventoryManager.TryGiveItemstack(stack))
                {
                    sapi.World.SpawnItemEntity(stack, (questgiver ?? fromPlayer.Entity).ServerPos.XYZ);
                }
            }
            var randomItems = quest.randomItemRewards?.items == null
                ? new List<RandomItem>()
                : new List<RandomItem>(quest.randomItemRewards.items);

            int selectAmount = quest.randomItemRewards?.selectAmount ?? 0;
            for (int i = 0; i < selectAmount; i++)
            {
                if (randomItems.Count <= 0) break;
                var randomItem = randomItems[sapi.World.Rand.Next(0, randomItems.Count)];
                randomItems.Remove(randomItem);
                CollectibleObject item = sapi.World.GetItem(new AssetLocation(randomItem.itemCode));
                if (item == null)
                {
                    item = sapi.World.GetBlock(new AssetLocation(randomItem.itemCode));
                }
                if (item == null)
                {
                    sapi.Logger.Error($"alegacyvsquest: Quest '{quest.id}' has invalid random item reward code '{randomItem.itemCode}'. Skipping reward.");
                    continue;
                }

                var stack = new ItemStack(item, sapi.World.Rand.Next(randomItem.minAmount, randomItem.maxAmount + 1));
                if (!fromPlayer.InventoryManager.TryGiveItemstack(stack))
                {
                    sapi.World.SpawnItemEntity(stack, (questgiver ?? fromPlayer.Entity).ServerPos.XYZ);
                }
            }
            foreach (var action in quest.actionRewards)
            {
                try
                {
                    actionRegistry[action.id].Execute(sapi, message, fromPlayer, action.args);
                }
                catch (Exception ex)
                {
                    sapi.Logger.Error(string.Format("Action {0} caused an Error in Quest {1}. The Error had the following message: {2}\n Stacktrace:", action.id, quest.id, ex.Message, ex.StackTrace));
                    sapi.SendMessage(fromPlayer, GlobalConstants.InfoLogChatGroup, Lang.Get("alegacyvsquest:quest-action-error", quest.id), EnumChatType.Notification);
                }
            }
        }

        private void MarkQuestCompleted(IServerPlayer fromPlayer, QuestCompletedMessage message, Entity questgiver)
        {
            var key = "alegacyvsquest:playercompleted";
            var completedQuests = fromPlayer.Entity.WatchedAttributes.GetStringArray(key, new string[0]).ToList();
            string questId = NormalizeQuestId(message.questId, completedQuests, questRegistry);
            if (!completedQuests.Contains(questId))
            {
                completedQuests.Add(questId);
                fromPlayer.Entity.WatchedAttributes.SetStringArray(key, completedQuests.ToArray());
                fromPlayer.Entity.WatchedAttributes.MarkPathDirty(key);
            }
        }

        private static string NormalizeQuestId(string questId, List<string> completedQuests, Dictionary<string, Quest> registry)
        {
            if (string.IsNullOrWhiteSpace(questId)) return questId;
            if (registry != null && registry.ContainsKey(questId)) return questId;

            const string legacyPrefix = "vsquest:";
            const string currentPrefix = "alegacyvsquest:";

            if (questId.StartsWith(legacyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                string mapped = currentPrefix + questId.Substring(legacyPrefix.Length);
                if ((registry != null && registry.ContainsKey(mapped))
                    || (completedQuests != null && completedQuests.Contains(mapped)))
                {
                    return mapped;
                }
            }

            return questId;
        }
    }
}
