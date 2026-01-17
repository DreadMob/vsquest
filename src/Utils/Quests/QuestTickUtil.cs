using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public static class QuestTickUtil
    {
        public static void HandleQuestTick(float dt, Dictionary<string, Quest> questRegistry, Dictionary<string, ActionObjectiveBase> actionObjectiveRegistry, IPlayer[] players, System.Func<string, List<ActiveQuest>> getPlayerQuests, ICoreServerAPI sapi)
        {
            if (players == null || players.Length == 0) return;

            for (int p = 0; p < players.Length; p++)
            {
                if (players[p] is not IServerPlayer serverPlayer) continue;

                var activeQuests = getPlayerQuests(serverPlayer.PlayerUID);
                if (activeQuests == null || activeQuests.Count == 0) continue;

                for (int aq = 0; aq < activeQuests.Count; aq++)
                {
                    var activeQuest = activeQuests[aq];
                    if (activeQuest == null || string.IsNullOrWhiteSpace(activeQuest.questId)) continue;

                    if (!questRegistry.TryGetValue(activeQuest.questId, out var quest) || quest == null)
                    {
                        sapi.Logger.Error($"[alegacyvsquest] Active quest with id '{activeQuest.questId}' for player '{serverPlayer.PlayerUID}' not found in QuestRegistry. Skipping tick update. This might happen if a quest was removed but player data was not updated.");
                        continue;
                    }

                    for (int i = 0; i < quest.actionObjectives.Count; i++)
                    {
                        var objective = quest.actionObjectives[i];
                        if (objective.id == "walkdistance")
                        {
                            if (!QuestTimeGateUtil.AllowsProgress(serverPlayer, quest, actionObjectiveRegistry, "tick", objective.objectiveId)) continue;

                            var objectiveImplementation = actionObjectiveRegistry[objective.id] as WalkDistanceObjective;
                            objectiveImplementation?.OnTick(serverPlayer, activeQuest, i, objective.args, sapi, dt);
                        }
                        else if (objective.id == "temporalstorm")
                        {
                            if (!QuestTimeGateUtil.AllowsProgress(serverPlayer, quest, actionObjectiveRegistry, "tick", objective.objectiveId)) continue;

                            var objectiveImplementation = actionObjectiveRegistry[objective.id] as TemporalStormObjective;
                            objectiveImplementation?.OnTick(serverPlayer, activeQuest, objective, sapi);
                        }
                    }

                    TryFirePassiveActionObjectiveCompletions(serverPlayer, activeQuest, quest, actionObjectiveRegistry, sapi);
                }
            }
        }

        private static void TryFirePassiveActionObjectiveCompletions(IServerPlayer player, ActiveQuest activeQuest, Quest questDef, Dictionary<string, ActionObjectiveBase> actionObjectiveRegistry, ICoreServerAPI sapi)
        {
            if (player == null || activeQuest == null || questDef?.actionObjectives == null) return;
            if (sapi == null || actionObjectiveRegistry == null) return;

            var wa = player.Entity?.WatchedAttributes;
            if (wa == null) return;

            string throttleKey = $"alegacyvsquest:ao:lastcheck:{activeQuest.questId}";
            double now = sapi.World.Calendar.TotalHours;
            double last = wa.GetDouble(throttleKey, -999999);
            if (now - last < (1.0 / 3600.0)) return;
            wa.SetDouble(throttleKey, now);
            wa.MarkPathDirty(throttleKey);

            for (int i = 0; i < questDef.actionObjectives.Count; i++)
            {
                var ao = questDef.actionObjectives[i];
                if (ao == null) continue;
                if (string.IsNullOrWhiteSpace(ao.onCompleteActions)) continue;

                // Skip event-driven objectives; they fire completion in their own hooks.
                if (ao.id == "walkdistance") continue;
                if (ao.id == "temporalstorm") continue;
                if (ao.id == "killnear") continue;
                if (ao.id == "randomkill") continue;
                if (ao.id == "interactwithentity") continue;
                if (ao.id == "interactat") continue;
                if (ao.id == "interactcount") continue;

                if (!actionObjectiveRegistry.TryGetValue(ao.id, out var impl) || impl == null) continue;

                bool ok;
                try
                {
                    ok = impl.IsCompletable(player, ao.args);
                }
                catch
                {
                    continue;
                }

                QuestActionObjectiveCompletionUtil.TryFireOnComplete(sapi, player, activeQuest, ao, ao.objectiveId, ok);
            }
        }
    }
}
