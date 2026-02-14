using System.Collections.Generic;
using Vintagestory.API.Common;

namespace VsQuest
{
    public static class QuestTimeGateUtil
    {
        // Кэш предвычисленных gate'ов для квестов
        private static readonly Dictionary<string, QuestGateCache> gateCacheByQuestId = 
            new Dictionary<string, QuestGateCache>(System.StringComparer.OrdinalIgnoreCase);

        private class QuestGateCache
        {
            public List<TimeGateInfo> TimeGates = new List<TimeGateInfo>();
            public List<LandGateInfo> LandGates = new List<LandGateInfo>();
        }

        private class TimeGateInfo
        {
            public string[] Args;
            public string GateScope;
            public string GateObjectiveId;
        }

        private class LandGateInfo
        {
            public string[] Args;
            public string GateObjectiveId;
        }

        private static QuestGateCache BuildGateCache(Quest questDef)
        {
            var cache = new QuestGateCache();
            if (questDef?.actionObjectives == null) return cache;

            for (int i = 0; i < questDef.actionObjectives.Count; i++)
            {
                var ao = questDef.actionObjectives[i];
                if (ao == null) continue;

                if (ao.id == "timeofday")
                {
                    ParseTimeGateArgs(ao.args, questDef, out string gateScope, out string gateObjectiveId);
                    cache.TimeGates.Add(new TimeGateInfo 
                    { 
                        Args = ao.args, 
                        GateScope = gateScope, 
                        GateObjectiveId = gateObjectiveId 
                    });
                }
                else if (ao.id == "landgate")
                {
                    if (LandGateObjective.TryParseArgs(ao.args, out _, out string gateObjectiveId, out _, out _))
                    {
                        cache.LandGates.Add(new LandGateInfo 
                        { 
                            Args = ao.args, 
                            GateObjectiveId = gateObjectiveId 
                        });
                    }
                }
            }

            return cache;
        }

        public static bool AllowsProgress(IPlayer player, Quest questDef, Dictionary<string, ActionObjectiveBase> actionObjectiveRegistry)
        {
            return AllowsProgress(player, questDef, actionObjectiveRegistry, null, null);
        }

        public static bool AllowsProgress(IPlayer player, Quest questDef, Dictionary<string, ActionObjectiveBase> actionObjectiveRegistry, string scope)
        {
            return AllowsProgress(player, questDef, actionObjectiveRegistry, scope, null);
        }

        public static bool AllowsProgress(IPlayer player, Quest questDef, Dictionary<string, ActionObjectiveBase> actionObjectiveRegistry, string scope, string objectiveId)
        {
            if (player == null || questDef == null) return true;
            if (questDef.actionObjectives == null) return true;

            // Получаем или создаем кэш для этого квеста
            if (!gateCacheByQuestId.TryGetValue(questDef.id, out var cache))
            {
                cache = BuildGateCache(questDef);
                gateCacheByQuestId[questDef.id] = cache;
            }

            var timeOfDayImpl = actionObjectiveRegistry != null && actionObjectiveRegistry.TryGetValue("timeofday", out var tod) ? tod : null;
            var landGateImpl = actionObjectiveRegistry != null && actionObjectiveRegistry.TryGetValue("landgate", out var lg) ? lg : null;

            bool foundMatchingGate = false;
            bool allows = true;

            // Проверяем time-of-day gates из кэша
            if (timeOfDayImpl != null && cache.TimeGates.Count > 0)
            {
                for (int i = 0; i < cache.TimeGates.Count; i++)
                {
                    var gate = cache.TimeGates[i];
                    if (!AppliesToScope(gate.GateScope, scope)) continue;
                    if (!AppliesToObjectiveId(gate.GateObjectiveId, objectiveId)) continue;

                    foundMatchingGate = true;
                    allows &= timeOfDayImpl.IsCompletable(player, gate.Args);
                }
            }

            // Проверяем land gates из кэша
            if (landGateImpl != null && cache.LandGates.Count > 0)
            {
                for (int i = 0; i < cache.LandGates.Count; i++)
                {
                    var gate = cache.LandGates[i];
                    if (!string.IsNullOrWhiteSpace(gate.GateObjectiveId) && !AppliesToObjectiveId(gate.GateObjectiveId, objectiveId))
                        continue;

                    foundMatchingGate = true;
                    allows &= landGateImpl.IsCompletable(player, gate.Args);
                }
            }

            return !foundMatchingGate || allows;
        }

        private static bool AppliesToScope(string gateScope, string requestedScope)
        {
            if (string.IsNullOrWhiteSpace(gateScope)) return false;
            if (string.IsNullOrWhiteSpace(requestedScope)) return false;

            return string.Equals(gateScope.Trim(), requestedScope.Trim(), System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool AppliesToObjectiveId(string gateObjectiveId, string requestedObjectiveId)
        {
            if (string.IsNullOrWhiteSpace(gateObjectiveId)) return false;
            if (string.IsNullOrWhiteSpace(requestedObjectiveId)) return false;

            return string.Equals(gateObjectiveId.Trim(), requestedObjectiveId.Trim(), System.StringComparison.OrdinalIgnoreCase);
        }

        private static void ParseTimeGateArgs(string[] args, Quest questDef, out string gateScope, out string gateObjectiveId)
        {
            gateScope = null;
            gateObjectiveId = null;

            // Required format: [mode, objectiveId]
            if (args == null || args.Length != 2) return;

            gateObjectiveId = args[1];
            gateScope = InferScopeFromObjectiveId(questDef, gateObjectiveId);
        }

        private static string InferScopeFromObjectiveId(Quest questDef, string objectiveId)
        {
            if (questDef?.actionObjectives == null) return null;
            if (string.IsNullOrWhiteSpace(objectiveId)) return null;

            for (int i = 0; i < questDef.actionObjectives.Count; i++)
            {
                var ao = questDef.actionObjectives[i];
                if (ao == null) continue;
                if (!string.Equals(ao.objectiveId, objectiveId, System.StringComparison.OrdinalIgnoreCase)) continue;

                // Map actionObjective type -> scope
                if (string.Equals(ao.id, "walkdistance", System.StringComparison.OrdinalIgnoreCase)) return "tick";
                if (string.Equals(ao.id, "randomkill", System.StringComparison.OrdinalIgnoreCase)) return "kill";
                if (string.Equals(ao.id, "killnear", System.StringComparison.OrdinalIgnoreCase)) return "kill";
                if (string.Equals(ao.id, "temporalstorm", System.StringComparison.OrdinalIgnoreCase)) return "tick";
            }

            return null;
        }

        // Очистка кэша при перезагрузке квестов
        public static void ClearCache()
        {
            gateCacheByQuestId.Clear();
        }
    }
}
