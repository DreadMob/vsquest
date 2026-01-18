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
            if (wa.GetBool(MigrationFlagKey, false))
            {
                return 0;
            }

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

            foreach (var legacyEntry in journal.Entries)
            {
                if (legacyEntry == null) continue;
                if (string.IsNullOrWhiteSpace(legacyEntry.LoreCode)) continue;
                if (!IsQuestLoreCode(legacyEntry.LoreCode, lorePrefix, questSystem)) continue;

                eligibleCount++;

                string resolvedQuestId = ResolveQuestId(legacyEntry.LoreCode, questSystem);

                var existing = entries.FirstOrDefault(e => e != null && string.Equals(e.LoreCode, legacyEntry.LoreCode, StringComparison.OrdinalIgnoreCase));
                if (existing == null)
                {
                    existing = new QuestJournalEntry
                    {
                        QuestId = resolvedQuestId,
                        LoreCode = legacyEntry.LoreCode,
                        Title = legacyEntry.Title,
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

                if (!string.IsNullOrWhiteSpace(legacyEntry.Title) && !string.Equals(existing.Title, legacyEntry.Title, StringComparison.Ordinal))
                {
                    existing.Title = legacyEntry.Title;
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

            return false;
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
