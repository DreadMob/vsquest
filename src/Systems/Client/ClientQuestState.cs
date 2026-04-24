using System;
using System.Collections.Generic;
using System.Linq;

namespace VsQuest
{
    /// <summary>
    /// Client-side cache of active player quests to avoid unnecessary network traffic.
    /// Updated whenever QuestInfoMessage is received from server.
    /// </summary>
    public static class ClientQuestState
    {
        private static List<ActiveQuest> _activeQuests = new List<ActiveQuest>();
        private static readonly object _lock = new object();

        public static void UpdateActiveQuests(List<ActiveQuest> quests)
        {
            lock (_lock)
            {
                _activeQuests = quests ?? new List<ActiveQuest>();
            }
        }

        public static List<ActiveQuest> GetActiveQuests()
        {
            lock (_lock)
            {
                return _activeQuests.ToList();
            }
        }

        /// <summary>
        /// Checks if player has any active quest with interactat or interactcount objectives.
        /// Used to skip sending VanillaBlockInteractMessage when not needed.
        /// </summary>
        public static bool HasInteractObjectives(QuestSystem questSystem)
        {
            lock (_lock)
            {
                if (_activeQuests == null || _activeQuests.Count == 0) return false;
                if (questSystem?.QuestRegistry == null) return false;

                foreach (var activeQuest in _activeQuests)
                {
                    if (activeQuest == null || string.IsNullOrWhiteSpace(activeQuest.questId)) continue;

                    if (!questSystem.QuestRegistry.TryGetValue(activeQuest.questId, out var questDef)) continue;
                    if (questDef == null) continue;

                    // Check action objectives for interactat/interactcount
                    var actionObjectives = questDef.GetActionObjectives(activeQuest.currentStageIndex);
                    if (actionObjectives != null)
                    {
                        foreach (var ao in actionObjectives)
                        {
                            if (ao != null && (ao.id == "interactat" || ao.id == "interactcount"))
                            {
                                return true;
                            }
                        }
                    }

                    // Check legacy interact objectives
                    var currentStage = questDef.GetStage(activeQuest.currentStageIndex);
                    var interactObjectives = currentStage?.interactObjectives ?? questDef.interactObjectives;
                    if (interactObjectives != null && interactObjectives.Count > 0)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public static void Clear()
        {
            lock (_lock)
            {
                _activeQuests.Clear();
            }
        }
    }
}
