using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace VsQuest
{
    public static class QuestProgressTextUtil
    {
        private static string LocalizeMobName(string code)
        {
            return LocalizationUtils.GetMobDisplayName(code);
        }

        private static bool TryParseWalkDistanceArgs(string[] args, out string questId, out int slot, out int needMeters)
        {
            questId = null;
            slot = 0;
            needMeters = 0;

            if (args == null || args.Length < 2) return false;

            questId = args[0];
            if (string.IsNullOrWhiteSpace(questId)) return false;

            if (args.Length >= 3 && int.TryParse(args[1], out int parsedSlot))
            {
                slot = parsedSlot;
                if (!int.TryParse(args[2], out needMeters)) needMeters = 0;
            }
            else
            {
                slot = 0;
                if (!int.TryParse(args[1], out needMeters)) needMeters = 0;
            }

            if (slot < 0) slot = 0;
            if (needMeters < 0) needMeters = 0;

            return true;
        }

        private static bool TryParseTimeOfDayArgs(string[] args, out string mode)
        {
            mode = null;
            if (args == null || args.Length < 1) return false;
            mode = args[0];
            if (string.IsNullOrWhiteSpace(mode)) return false;
            return true;
        }

        public static bool TryBuildRandomKillProgressText(ICoreClientAPI capi, IClientPlayer player, ActiveQuest activeQuest, out string progressText)
        {
            progressText = null;
            if (capi == null || player == null || activeQuest == null) return false;

            try
            {
                var questSystem = capi.ModLoader.GetModSystem<QuestSystem>();
                if (questSystem == null) return false;
                if (!questSystem.QuestRegistry.TryGetValue(activeQuest.questId, out var questDef)) return false;

                var wa = player.Entity?.WatchedAttributes;
                if (wa == null) return false;

                if (questDef.actionObjectives == null || !questDef.actionObjectives.Exists(obj => obj.id == "randomkill")) return false;

                string questId = activeQuest.questId;
                int slots = wa.GetInt($"vsquest:randkill:{questId}:slots", 0);

                string BuildLine(int slot)
                {
                    string code = wa.GetString($"vsquest:randkill:{questId}:slot{slot}:code", "?");
                    int have = wa.GetInt($"vsquest:randkill:{questId}:slot{slot}:have", 0);
                    int need = wa.GetInt($"vsquest:randkill:{questId}:slot{slot}:need", 0);

                    if (need < 0) need = 0;
                    if (have < 0) have = 0;
                    if (need > 0 && have > need) have = need;

                    return $"- {LocalizeMobName(code)}: {have}/{need}";
                }

                var lines = new List<string>();

                if (slots > 0)
                {
                    for (int slot = 0; slot < slots; slot++)
                    {
                        lines.Add(BuildLine(slot));
                    }
                }
                else
                {
                    // Legacy single target
                    string legacyCode = wa.GetString($"vsquest:randkill:{questId}:code", "?");
                    int legacyHave = wa.GetInt($"vsquest:randkill:{questId}:have", 0);
                    int legacyNeed = wa.GetInt($"vsquest:randkill:{questId}:need", 0);
                    if (legacyNeed < 0) legacyNeed = 0;
                    if (legacyHave < 0) legacyHave = 0;
                    if (legacyNeed > 0 && legacyHave > legacyNeed) legacyHave = legacyNeed;

                    lines.Add($"- {LocalizeMobName(legacyCode)}: {legacyHave}/{legacyNeed}");
                }

                // Append walk distance objectives if present
                if (questDef.actionObjectives != null)
                {
                    int walkLineIndex = 0;
                    foreach (var obj in questDef.actionObjectives)
                    {
                        if (obj?.id != "walkdistance") continue;
                        if (!TryParseWalkDistanceArgs(obj.args, out string walkQuestId, out int walkSlot, out int needMeters)) continue;

                        float have = wa.GetFloat(WalkDistanceObjective.HaveKey(walkQuestId, walkSlot), 0f);
                        if (have < 0f) have = 0f;
                        int haveInt = (int)Math.Floor(have);
                        if (needMeters > 0 && haveInt > needMeters) haveInt = needMeters;
                        if (needMeters < 0) needMeters = 0;

                        walkLineIndex++;
                        string baseLabel = Lang.HasTranslation("objective-walkdistance")
                            ? Lang.Get("objective-walkdistance")
                            : (Lang.HasTranslation("vsquest:objective-walkdistance") ? Lang.Get("vsquest:objective-walkdistance") : "Walk");

                        string unit = Lang.HasTranslation("unit-meter-short")
                            ? Lang.Get("unit-meter-short")
                            : (Lang.HasTranslation("vsquest:unit-meter-short") ? Lang.Get("vsquest:unit-meter-short") : "m");
                        string label = walkLineIndex > 1 ? $"{baseLabel} {walkLineIndex}" : baseLabel;
                        lines.Add($"- {label}: {haveInt}/{needMeters} {unit}");
                    }
                }

                // Append time-of-day gate if present
                if (questDef.actionObjectives != null)
                {
                    foreach (var obj in questDef.actionObjectives)
                    {
                        if (obj?.id != "timeofday") continue;
                        if (!TryParseTimeOfDayArgs(obj.args, out string mode)) continue;

                        bool isNight = string.Equals(mode, "night", StringComparison.OrdinalIgnoreCase);
                        string label = isNight
                            ? (Lang.HasTranslation("objective-timeofday-night") ? Lang.Get("objective-timeofday-night") : "Night")
                            : (Lang.HasTranslation("objective-timeofday-day") ? Lang.Get("objective-timeofday-day") : "Day");

                        // The objective itself reports 0/1 or 1/1
                        var objectiveImpl = questSystem.ActionObjectiveRegistry["timeofday"] as TimeOfDayObjective;
                        var prog = objectiveImpl?.progress(player, obj.args) ?? new List<int>(new int[] { 0, 1 });
                        int have = prog.Count > 0 ? prog[0] : 0;
                        int need = prog.Count > 1 ? prog[1] : 1;
                        lines.Add($"- {label}: {have}/{need}");
                    }
                }

                // Append normal kill objectives if present
                if (questDef.killObjectives != null && activeQuest.killTrackers != null)
                {
                    int total = Math.Min(questDef.killObjectives.Count, activeQuest.killTrackers.Count);
                    for (int i = 0; i < total; i++)
                    {
                        int have = activeQuest.killTrackers[i]?.count ?? 0;
                        int need = questDef.killObjectives[i]?.demand ?? 0;
                        if (have < 0) have = 0;
                        if (need < 0) need = 0;
                        if (need > 0 && have > need) have = need;

                        string code = null;
                        if (activeQuest.killTrackers[i]?.relevantCodes != null && activeQuest.killTrackers[i].relevantCodes.Count > 0)
                        {
                            code = activeQuest.killTrackers[i].relevantCodes[0];
                        }

                        string name = string.IsNullOrWhiteSpace(code) ? "?" : LocalizeMobName(code);
                        lines.Add($"- {name}: {have}/{need}");
                    }
                }

                progressText = string.Join("\n", lines);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
