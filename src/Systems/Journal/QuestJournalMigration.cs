using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsQuest
{
    public static class QuestJournalMigration
    {
        private const string MigrationFlagKey = "alegacyvsquest:journal:migratedfromvanilla";

        public static int MigrateFromVanilla(ICoreServerAPI sapi, IServerPlayer player, string lorePrefix = "alegacyvsquest")
        {
            if (sapi == null || player == null) return 0;
            if (player.Entity?.WatchedAttributes == null) return 0;

            var wa = player.Entity.WatchedAttributes;
            bool alreadyMigrated = wa.GetBool(MigrationFlagKey, false);

            var modJournal = sapi.ModLoader.GetModSystem<ModJournal>();
            if (modJournal == null)
            {
                sapi.Logger.VerboseDebug($"[alegacyvsquest] Journal migration skipped for {player.PlayerUID}: ModJournal not found.");
                return 0;
            }

            var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();

            var t = modJournal.GetType();
            var journalsField = t.GetField("journalsByPlayerUid", BindingFlags.Instance | BindingFlags.NonPublic);

            Dictionary<string, Journal> journals = journalsField?.GetValue(modJournal) as Dictionary<string, Journal>;
            if (journals == null)
            {
                var fields = t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
                var jf = fields.FirstOrDefault(f => typeof(Dictionary<string, Journal>).IsAssignableFrom(f.FieldType));
                journals = jf?.GetValue(modJournal) as Dictionary<string, Journal>;
            }
            if (journals == null)
            {
                sapi.Logger.Warning($"[alegacyvsquest] Journal migration failed for {player.PlayerUID}: could not access ModJournal journals dictionary (field name mismatch?).");
                return 0;
            }

            if (!journals.TryGetValue(player.PlayerUID, out var journal) || journal?.Entries == null) return 0;

            var entries = QuestJournalEntry.Load(wa);
            bool changed = false;
            int importedCount = 0;
            int eligibleCount = 0;
            var legacyLoreCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var legacyEntry in journal.Entries)
            {
                if (legacyEntry == null) continue;
                if (string.IsNullOrWhiteSpace(legacyEntry.LoreCode)) continue;
                if (!IsQuestLoreCode(legacyEntry.LoreCode, lorePrefix, questSystem)) continue;

                eligibleCount++;

                legacyLoreCodes.Add(legacyEntry.LoreCode);

                string resolvedQuestId = ResolveQuestId(legacyEntry.LoreCode, questSystem);
                string resolvedTitle = ResolveLegacyTitle(legacyEntry.LoreCode, legacyEntry.Title);
                string resolvedLoreCode = legacyEntry.LoreCode;

                if (TryResolveQuestEntryFromLegacy(legacyEntry, questSystem, out var mappedQuestId, out var mappedLoreCode, out var mappedTitle))
                {
                    if (!string.IsNullOrWhiteSpace(mappedQuestId)) resolvedQuestId = mappedQuestId;
                    if (!string.IsNullOrWhiteSpace(mappedLoreCode)) resolvedLoreCode = mappedLoreCode;
                    if (!string.IsNullOrWhiteSpace(mappedTitle)) resolvedTitle = mappedTitle;
                }
                else if (IsQuestGiverLoreCode(legacyEntry.LoreCode, questSystem))
                {
                    continue;
                }

                string forcedQuestId = ResolveHardcodedQuestId(resolvedLoreCode, legacyEntry.LoreCode);
                if (!string.IsNullOrWhiteSpace(forcedQuestId))
                {
                    resolvedQuestId = forcedQuestId;
                }

                var existing = entries.FirstOrDefault(e => e != null && string.Equals(e.LoreCode, resolvedLoreCode, StringComparison.OrdinalIgnoreCase));
                if (existing == null && !string.Equals(resolvedLoreCode, legacyEntry.LoreCode, StringComparison.OrdinalIgnoreCase))
                {
                    var legacyExisting = entries.FirstOrDefault(e => e != null && string.Equals(e.LoreCode, legacyEntry.LoreCode, StringComparison.OrdinalIgnoreCase));
                    if (legacyExisting != null)
                    {
                        legacyExisting.LoreCode = resolvedLoreCode;
                        legacyExisting.Title = resolvedTitle;
                        existing = legacyExisting;
                        changed = true;
                    }
                }
                if (existing == null)
                {
                    existing = new QuestJournalEntry
                    {
                        QuestId = resolvedQuestId,
                        LoreCode = resolvedLoreCode,
                        Title = resolvedTitle,
                        Chapters = new List<string>()
                    };
                    entries.Add(existing);
                    changed = true;
                    importedCount++;
                }
                else if (string.IsNullOrWhiteSpace(existing.QuestId) && !string.IsNullOrWhiteSpace(resolvedQuestId))
                {
                    existing.QuestId = resolvedQuestId;
                    changed = true;
                }
                else if (!string.IsNullOrWhiteSpace(resolvedQuestId)
                    && !string.Equals(existing.QuestId, resolvedQuestId, StringComparison.OrdinalIgnoreCase)
                    && (string.IsNullOrWhiteSpace(existing.QuestId)
                        || string.Equals(existing.QuestId, resolvedLoreCode, StringComparison.OrdinalIgnoreCase)))
                {
                    existing.QuestId = resolvedQuestId;
                    changed = true;
                }

                if (!string.IsNullOrWhiteSpace(resolvedTitle)
                    && ShouldUpdateLegacyTitle(existing.Title, legacyEntry.Title, resolvedTitle))
                {
                    existing.Title = resolvedTitle;
                    changed = true;
                }

                if (existing.Chapters == null)
                {
                    existing.Chapters = new List<string>();
                    changed = true;
                }

                var chapterTexts = legacyEntry.Chapters?.Select(c => c?.Text)
                    .Where(text => !string.IsNullOrWhiteSpace(text))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

                if (chapterTexts != null && chapterTexts.Count > 0)
                {
                    var existingTexts = new HashSet<string>(existing.Chapters.Where(c => !string.IsNullOrWhiteSpace(c)), StringComparer.Ordinal);
                    foreach (var text in chapterTexts)
                    {
                        if (existingTexts.Contains(text)) continue;
                        existing.Chapters.Add(text);
                        existingTexts.Add(text);
                        changed = true;
                    }
                }
            }

            if (eligibleCount > 0)
            {
                if (changed)
                {
                    QuestJournalEntry.Save(wa, entries);
                    wa.MarkPathDirty(QuestJournalEntry.JournalEntriesKey);
                }

                if (legacyLoreCodes.Count > 0)
                {
                    RemoveLegacyEntries(modJournal, player, legacyLoreCodes);
                }

                // Mark migration as completed when we've processed quest entries.
                wa.SetBool(MigrationFlagKey, true);
                wa.MarkPathDirty(MigrationFlagKey);
            }
            else
            {
                sapi.Logger.VerboseDebug($"[alegacyvsquest] Journal migration found no eligible quest entries for {player.PlayerUID}. Migration flag not set.");
            }

            if (importedCount > 0)
            {
                sapi.Logger.Notification($"[alegacyvsquest] Imported {importedCount} journal entr{(importedCount == 1 ? "y" : "ies")} from vanilla journal for {player.PlayerUID}.");
            }

            return importedCount;
        }

        private static bool IsQuestLoreCode(string loreCode, string preferredPrefix, QuestSystem questSystem)
        {
            if (string.IsNullOrWhiteSpace(loreCode)) return false;

            if (questSystem?.QuestRegistry != null && questSystem.QuestRegistry.ContainsKey(loreCode)) return true;

            // Current mod id
            if (loreCode.StartsWith(preferredPrefix, StringComparison.OrdinalIgnoreCase)) return true;

            // Legacy mod id
            if (loreCode.StartsWith("vsquest", StringComparison.OrdinalIgnoreCase)) return true;

            // Legacy quest packs with their own namespaces
            if (loreCode.StartsWith("albase", StringComparison.OrdinalIgnoreCase)) return true;
            if (loreCode.StartsWith("newyear2026", StringComparison.OrdinalIgnoreCase)) return true;

            return false;
        }

        private static string ResolveHardcodedQuestId(string resolvedLoreCode, string legacyLoreCode)
        {
            if (string.Equals(resolvedLoreCode, "newyear2026:newyear2026", StringComparison.OrdinalIgnoreCase)
                || string.Equals(legacyLoreCode, "newyear2026:newyear2026", StringComparison.OrdinalIgnoreCase))
            {
                return "albase:witness";
            }

            return null;
        }

        private static string NormalizeJournalText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;

            var parts = text
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" ", parts);
        }

        private static bool TryResolveQuestEntryFromGroupIdFallback(JournalEntry legacyEntry, QuestSystem questSystem, out string resolvedQuestId, out string resolvedLoreCode, out string resolvedTitle)
        {
            resolvedQuestId = null;
            resolvedLoreCode = null;
            resolvedTitle = null;

            if (legacyEntry == null || questSystem?.QuestRegistry == null) return false;
            if (string.IsNullOrWhiteSpace(legacyEntry.LoreCode)) return false;

            var candidates = new List<Tuple<string, string, string>>();

            foreach (var kvp in questSystem.QuestRegistry)
            {
                var quest = kvp.Value;
                if (quest == null) continue;

                void CollectFromActions(List<ActionWithArgs> actions)
                {
                    if (actions == null) return;
                    foreach (var action in actions)
                    {
                        if (action == null) continue;
                        if (!string.Equals(action.id, "addjournalentry", StringComparison.OrdinalIgnoreCase)) continue;

                        if (!TryExtractJournalActionInfo(action.args, out var groupId, out var loreCode, out var titleArg, out var chapterArgs)) continue;
                        if (string.IsNullOrWhiteSpace(groupId)) continue;
                        if (!string.Equals(groupId, legacyEntry.LoreCode, StringComparison.OrdinalIgnoreCase)) continue;

                        string candidateQuestId = groupId;
                        string candidateLoreCode = string.IsNullOrWhiteSpace(loreCode) ? candidateQuestId : loreCode;
                        string candidateTitle = ResolveLegacyTitle(candidateLoreCode, LocalizationUtils.GetSafe(titleArg));
                        candidates.Add(Tuple.Create(candidateQuestId, candidateLoreCode, candidateTitle));
                    }
                }

                CollectFromActions(quest.onAcceptedActions);
                CollectFromActions(quest.actionRewards);
            }

            if (candidates.Count == 1)
            {
                resolvedQuestId = candidates[0].Item1;
                resolvedLoreCode = candidates[0].Item2;
                resolvedTitle = candidates[0].Item3;
                return true;
            }

            return false;
        }

        private static bool TryResolveQuestEntryFromLegacy(JournalEntry legacyEntry, QuestSystem questSystem, out string questId, out string loreCode, out string title)
        {
            questId = null;
            loreCode = null;
            title = null;

            if (legacyEntry == null || questSystem?.QuestRegistry == null) return false;

            var legacyTexts = new HashSet<string>(StringComparer.Ordinal);
            if (legacyEntry.Chapters != null)
            {
                foreach (var ch in legacyEntry.Chapters)
                {
                    if (!string.IsNullOrWhiteSpace(ch?.Text)) legacyTexts.Add(NormalizeJournalText(ch.Text));
                }
            }

            foreach (var kvp in questSystem.QuestRegistry)
            {
                var quest = kvp.Value;
                if (quest == null) continue;

                if (TryResolveQuestEntryFromActions(kvp.Key, quest.onAcceptedActions, legacyEntry, legacyTexts, out questId, out loreCode, out title)) return true;
                if (TryResolveQuestEntryFromActions(kvp.Key, quest.actionRewards, legacyEntry, legacyTexts, out questId, out loreCode, out title)) return true;
            }

            if (TryResolveQuestEntryFromGroupIdFallback(legacyEntry, questSystem, out questId, out loreCode, out title)) return true;

            return false;
        }

        private static bool TryResolveQuestEntryFromActions(string questId, List<ActionWithArgs> actions, JournalEntry legacyEntry, HashSet<string> legacyTexts, out string resolvedQuestId, out string resolvedLoreCode, out string resolvedTitle)
        {
            resolvedQuestId = null;
            resolvedLoreCode = null;
            resolvedTitle = null;

            if (actions == null) return false;

            foreach (var action in actions)
            {
                if (action == null) continue;
                if (!string.Equals(action.id, "addjournalentry", StringComparison.OrdinalIgnoreCase)) continue;

                if (!TryExtractJournalActionInfo(action.args, out var groupId, out var loreCode, out var titleArg, out var chapterArgs)) continue;

                if (!string.IsNullOrWhiteSpace(loreCode) && string.Equals(loreCode, legacyEntry.LoreCode, StringComparison.OrdinalIgnoreCase))
                {
                    resolvedQuestId = questId;
                    resolvedLoreCode = loreCode;
                    resolvedTitle = ResolveLegacyTitle(loreCode, LocalizationUtils.GetSafe(titleArg));
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(groupId) && string.Equals(groupId, legacyEntry.LoreCode, StringComparison.OrdinalIgnoreCase))
                {
                    if (!ActionMatchesLegacyTexts(chapterArgs, legacyTexts)) continue;
                    resolvedQuestId = groupId;
                    resolvedLoreCode = string.IsNullOrWhiteSpace(loreCode) ? questId : loreCode;
                    resolvedTitle = ResolveLegacyTitle(resolvedLoreCode, LocalizationUtils.GetSafe(titleArg));
                    return true;
                }
            }

            return false;
        }

        private static bool TryExtractJournalActionInfo(string[] args, out string groupId, out string loreCode, out string titleArg, out List<string> chapterArgs)
        {
            groupId = null;
            loreCode = null;
            titleArg = null;
            chapterArgs = new List<string>();

            if (args == null || args.Length == 0) return false;

            int index;
            if (args.Length >= 2 && LooksLikeLoreCode(args[1]))
            {
                groupId = args[0];
                loreCode = args[1];
                index = 2;
            }
            else
            {
                loreCode = args[0];
                index = 1;
            }

            while (index < args.Length && IsJournalFlag(args[index])) index++;

            if (index < args.Length)
            {
                titleArg = args[index];
                index++;
            }

            while (index < args.Length && IsJournalFlag(args[index])) index++;

            for (int i = index; i < args.Length; i++)
            {
                chapterArgs.Add(args[i]);
            }

            return !string.IsNullOrWhiteSpace(loreCode);
        }

        private static bool ActionMatchesLegacyTexts(List<string> chapterArgs, HashSet<string> legacyTexts)
        {
            if (legacyTexts == null || legacyTexts.Count == 0) return true;
            if (chapterArgs == null || chapterArgs.Count == 0) return false;

            foreach (var arg in chapterArgs)
            {
                var text = NormalizeJournalText(LocalizationUtils.GetSafe(arg));
                if (!string.IsNullOrWhiteSpace(text) && legacyTexts.Contains(text)) return true;
            }

            return false;
        }

        private static bool LooksLikeLoreCode(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && value.Contains(":");
        }

        private static bool ShouldUpdateLegacyTitle(string existingTitle, string legacyTitle, string resolvedTitle)
        {
            if (string.IsNullOrWhiteSpace(resolvedTitle)) return false;
            if (string.IsNullOrWhiteSpace(existingTitle)) return true;
            if (string.Equals(existingTitle, resolvedTitle, StringComparison.Ordinal)) return false;

            return !string.IsNullOrWhiteSpace(legacyTitle)
                && string.Equals(existingTitle, legacyTitle, StringComparison.Ordinal)
                && !string.Equals(legacyTitle, resolvedTitle, StringComparison.Ordinal);
        }

        private static bool IsJournalFlag(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            return string.Equals(value, "overwrite", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "note", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "quest", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveLegacyTitle(string loreCode, string legacyTitle)
        {
            string resolved = legacyTitle;
            string candidateKey = string.IsNullOrWhiteSpace(loreCode) ? null : loreCode + "-title";
            string candidateTitle = string.IsNullOrWhiteSpace(candidateKey) ? null : LocalizationUtils.GetSafe(candidateKey);
            if (string.Equals(loreCode, "newyear2026:newyear2026", StringComparison.OrdinalIgnoreCase)
                && (string.IsNullOrWhiteSpace(candidateTitle)
                    || string.Equals(candidateTitle, candidateKey, StringComparison.OrdinalIgnoreCase)))
            {
                return "Привет, 2026";
            }
            if (!string.IsNullOrWhiteSpace(candidateTitle) && !string.Equals(candidateTitle, candidateKey, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(legacyTitle) || string.Equals(legacyTitle, loreCode, StringComparison.OrdinalIgnoreCase))
                {
                    return candidateTitle;
                }

                string questGiverCode = GetQuestGiverCode(loreCode);
                if (!string.IsNullOrWhiteSpace(questGiverCode))
                {
                    string questGiverKey = questGiverCode + "-title";
                    string questGiverTitle = LocalizationUtils.GetSafe(questGiverKey);
                    if (!string.IsNullOrWhiteSpace(questGiverTitle) && !string.Equals(questGiverTitle, questGiverKey, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(legacyTitle, questGiverTitle, StringComparison.Ordinal))
                    {
                        return candidateTitle;
                    }
                }
            }

            return resolved;
        }

        private static string GetQuestGiverCode(string loreCode)
        {
            if (string.IsNullOrWhiteSpace(loreCode)) return null;
            int dashIndex = loreCode.LastIndexOf('-');
            if (dashIndex <= 0) return null;
            return loreCode.Substring(0, dashIndex);
        }

        private static bool IsQuestGiverLoreCode(string loreCode, QuestSystem questSystem)
        {
            if (string.IsNullOrWhiteSpace(loreCode)) return false;
            if (questSystem?.QuestRegistry == null) return false;
            if (questSystem.QuestRegistry.ContainsKey(loreCode)) return false;

            string prefix = loreCode + "-";
            foreach (var questId in questSystem.QuestRegistry.Keys)
            {
                if (questId != null && questId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void RemoveLegacyEntries(ModJournal modJournal, IServerPlayer player, HashSet<string> loreCodes)
        {
            if (modJournal == null || player == null || loreCodes == null || loreCodes.Count == 0) return;

            try
            {
                var t = modJournal.GetType();
                var journalsField = t.GetField("journalsByPlayerUid", BindingFlags.Instance | BindingFlags.NonPublic);
                var channelField = t.GetField("serverChannel", BindingFlags.Instance | BindingFlags.NonPublic);

                Dictionary<string, Journal> journals = journalsField?.GetValue(modJournal) as Dictionary<string, Journal>;
                IServerNetworkChannel serverChannel = channelField?.GetValue(modJournal) as IServerNetworkChannel;

                if (journals == null || serverChannel == null)
                {
                    var fields = t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
                    if (journals == null)
                    {
                        var jf = fields.FirstOrDefault(f => typeof(Dictionary<string, Journal>).IsAssignableFrom(f.FieldType));
                        journals = jf?.GetValue(modJournal) as Dictionary<string, Journal>;
                    }

                    if (serverChannel == null)
                    {
                        var cf = fields.FirstOrDefault(f => typeof(IServerNetworkChannel).IsAssignableFrom(f.FieldType));
                        serverChannel = cf?.GetValue(modJournal) as IServerNetworkChannel;
                    }
                }

                if (journals == null || serverChannel == null) return;
                if (!journals.TryGetValue(player.PlayerUID, out var journal) || journal?.Entries == null) return;

                int before = journal.Entries.Count;
                journal.Entries.RemoveAll(e => e != null && !string.IsNullOrWhiteSpace(e.LoreCode) && loreCodes.Contains(e.LoreCode));
                if (journal.Entries.Count == before) return;

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

                serverChannel.SendPacket(journal, player);
            }
            catch
            {
            }
        }

        private static string ResolveQuestId(string loreCode, QuestSystem questSystem)
        {
            if (!string.IsNullOrWhiteSpace(loreCode) && questSystem?.QuestRegistry != null && questSystem.QuestRegistry.ContainsKey(loreCode))
            {
                return loreCode;
            }

            return loreCode;
        }

        public static string NormalizeQuestId(string questId, Dictionary<string, Quest> questRegistry)
        {
            if (string.IsNullOrWhiteSpace(questId)) return questId;
            if (questRegistry == null) return questId;
            if (questRegistry.ContainsKey(questId)) return questId;

            const string legacyPrefix = "vsquest:";
            const string currentPrefix = "alegacyvsquest:";

            if (questId.StartsWith(legacyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                string mapped = currentPrefix + questId.Substring(legacyPrefix.Length);
                if (questRegistry.ContainsKey(mapped)) return mapped;
            }

            return questId;
        }

        public static string[] GetNormalizedCompletedQuestIds(Vintagestory.API.Common.IPlayer player, Dictionary<string, Quest> questRegistry)
        {
            var wa = player?.Entity?.WatchedAttributes;
            if (wa == null) return new string[0];

            var current = wa.GetStringArray("alegacyvsquest:playercompleted", new string[0]) ?? new string[0];
            var legacy = wa.GetStringArray("vsquest:playercompleted", null);

            var combined = new List<string>(current.Length + (legacy?.Length ?? 0));
            combined.AddRange(current);
            if (legacy != null) combined.AddRange(legacy);

            var normalizedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var questId in combined)
            {
                if (string.IsNullOrWhiteSpace(questId)) continue;
                normalizedSet.Add(NormalizeQuestId(questId, questRegistry));
            }

            var normalized = normalizedSet.ToArray();

            bool changed = legacy != null;
            if (!changed)
            {
                if (current.Length != normalized.Length)
                {
                    changed = true;
                }
                else
                {
                    var currentSet = new HashSet<string>(current, StringComparer.OrdinalIgnoreCase);
                    foreach (var id in normalized)
                    {
                        if (!currentSet.Contains(id))
                        {
                            changed = true;
                            break;
                        }
                    }
                }
            }

            if (changed)
            {
                wa.SetStringArray("alegacyvsquest:playercompleted", normalized);
                wa.MarkPathDirty("alegacyvsquest:playercompleted");

                if (legacy != null)
                {
                    wa.RemoveAttribute("vsquest:playercompleted");
                    wa.MarkPathDirty("vsquest:playercompleted");
                }
            }

            return normalized;
        }
    }
}
