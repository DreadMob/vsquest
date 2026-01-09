using System.Collections.Generic;
using Vintagestory.API.Server;

namespace VsQuest
{
    public static class QuestTickUtil
    {
        public static void HandleQuestTick(float dt, Dictionary<string, Quest> questRegistry, Dictionary<string, ActiveActionObjective> actionObjectiveRegistry, IServerPlayer[] players, System.Func<string, List<ActiveQuest>> getPlayerQuests, ICoreServerAPI sapi)
        {
            foreach (var serverPlayer in players)
            {
                var activeQuests = getPlayerQuests(serverPlayer.PlayerUID);
                foreach (var activeQuest in activeQuests)
                {
                    var quest = questRegistry[activeQuest.questId];
                    for (int i = 0; i < quest.actionObjectives.Count; i++)
                    {
                        var objective = quest.actionObjectives[i];
                        if (objective.id == "checkvariable")
                        {
                            var objectiveImplementation = actionObjectiveRegistry[objective.id] as CheckVariableObjective;
                            objectiveImplementation?.CheckAndFire(serverPlayer, quest, activeQuest, i, sapi);
                        }
                        else if (objective.id == "walkdistance")
                        {
                            var objectiveImplementation = actionObjectiveRegistry[objective.id] as WalkDistanceObjective;
                            objectiveImplementation?.OnTick(serverPlayer, activeQuest, i, objective.args, sapi, dt);
                        }
                    }
                }
            }
        }
    }
}
