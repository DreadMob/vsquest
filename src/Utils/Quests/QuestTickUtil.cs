using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public static class QuestTickUtil
    {
        private static readonly Dictionary<string, double> lastMissingQuestLogHoursByKey = new Dictionary<string, double>(System.StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, bool> questHasTickObjectivesByQuestId = new Dictionary<string, bool>(System.StringComparer.OrdinalIgnoreCase);

        private static double lastPassiveCheckHours = -1.0;

        public static void HandleQuestTick(float dt, Dictionary<string, Quest> questRegistry, Dictionary<string, ActionObjectiveBase> actionObjectiveRegistry, IPlayer[] players, System.Func<string, List<ActiveQuest>> getPlayerQuests, System.Action<string> markPlayerDirty, ICoreServerAPI sapi, double missingQuestLogThrottleHours = (1.0 / 60.0), double passiveCompletionThrottleHours = (1.0 / 60.0))
        {
            if (players == null || players.Length == 0) return;

			double missingLogThrottle = missingQuestLogThrottleHours;
			if (missingLogThrottle <= 0) missingLogThrottle = 1.0 / 60.0;

			double passiveThrottle = passiveCompletionThrottleHours;
			if (passiveThrottle <= 0) passiveThrottle = 1.0 / 60.0;

            double nowHours = sapi?.World?.Calendar?.TotalHours ?? 0;
            bool shouldCheckPassive = lastPassiveCheckHours < 0 || (nowHours - lastPassiveCheckHours) >= (1.0 / 60.0);
            if (shouldCheckPassive) lastPassiveCheckHours = nowHours;

            for (int p = 0; p < players.Length; p++)
            {
                if (players[p] is not IServerPlayer serverPlayer) continue;

                var activeQuests = getPlayerQuests(serverPlayer.PlayerUID);
                if (activeQuests == null || activeQuests.Count == 0) continue;

                bool playerDirty = false;
                var registry = questRegistry; // Локальная переменная для чуть более быстрого доступа

                for (int aq = 0; aq < activeQuests.Count; aq++)
                {
                    var activeQuest = activeQuests[aq];
                    if (activeQuest == null || string.IsNullOrWhiteSpace(activeQuest.questId)) continue;

                    if (!registry.TryGetValue(activeQuest.questId, out var questDef) || questDef == null)
                    {
                        // Throttle log to avoid spamming every tick for every player.
                        // Keyed by player uid + quest id.
                        try
                        {
                            double logNowHours = sapi?.World?.Calendar?.TotalHours ?? 0;
                            string logKey = serverPlayer.PlayerUID + ":" + activeQuest.questId;
                            if (!lastMissingQuestLogHoursByKey.TryGetValue(logKey, out var lastHours) || (logNowHours - lastHours) > missingLogThrottle)
                            {
                                lastMissingQuestLogHoursByKey[logKey] = logNowHours;
                                sapi.Logger.Error($"[alegacyvsquest] Active quest with id '{activeQuest.questId}' for player '{serverPlayer.PlayerUID}' not found in QuestRegistry. Skipping tick update. This might happen if a quest was removed but player data was not updated.");
                            }
                        }
                        catch
                        {
                        }

                        // Auto-heal: remove missing quest from active quests so we don't spam logs forever.
                        // Safe iteration: adjust index after removal.
                        try
                        {
                            activeQuests.RemoveAt(aq);
                            aq--;
                            playerDirty = true;
                        }
                        catch
                        {
                        }

                        continue;
                    }

                    bool hasTickObjectives;
                    if (!questHasTickObjectivesByQuestId.TryGetValue(activeQuest.questId, out hasTickObjectives))
                    {
                        hasTickObjectives = false;
                        try
                        {
                            if (questDef.actionObjectives != null)
                            {
                                for (int i = 0; i < questDef.actionObjectives.Count; i++)
                                {
                                    var objective = questDef.actionObjectives[i];
                                    if (objective == null) continue;
                                    if (objective.id == "walkdistance" || objective.id == "temporalstorm")
                                    {
                                        hasTickObjectives = true;
                                        break;
                                    }
                                }
                            }
                        }
                        catch
                        {
                            hasTickObjectives = true;
                        }

                        questHasTickObjectivesByQuestId[activeQuest.questId] = hasTickObjectives;
                    }

                    if (hasTickObjectives && questDef.actionObjectives != null)
                    {
                        for (int i = 0; i < questDef.actionObjectives.Count; i++)
                        {
                            var objective = questDef.actionObjectives[i];
                            if (objective == null) continue;

                            if (objective.id == "walkdistance")
                            {
                                if (!QuestTimeGateUtil.AllowsProgress(serverPlayer, questDef, actionObjectiveRegistry, "tick", objective.objectiveId)) continue;

                                if (!actionObjectiveRegistry.TryGetValue(objective.id, out var impl) || impl == null) continue;
                                var objectiveImplementation = impl as WalkDistanceObjective;
                                objectiveImplementation?.OnTick(serverPlayer, activeQuest, i, objective.args, sapi, dt);
                            }
                            else if (objective.id == "temporalstorm")
                            {
                                if (!QuestTimeGateUtil.AllowsProgress(serverPlayer, questDef, actionObjectiveRegistry, "tick", objective.objectiveId)) continue;

                                if (!actionObjectiveRegistry.TryGetValue(objective.id, out var impl) || impl == null) continue;
                                var objectiveImplementation = impl as TemporalStormObjective;
                                objectiveImplementation?.OnTick(serverPlayer, activeQuest, objective, sapi);
                            }
                        }
                    }

                    if (shouldCheckPassive) TryFirePassiveActionObjectiveCompletions(serverPlayer, activeQuest, questDef, actionObjectiveRegistry, sapi, passiveThrottle);
                }

                // Mark dirty once per player per tick if needed
                if (playerDirty)
                {
                    markPlayerDirty?.Invoke(serverPlayer.PlayerUID);
                }
            }
        }

        private static void TryFirePassiveActionObjectiveCompletions(IServerPlayer player, ActiveQuest activeQuest, Quest questDef, Dictionary<string, ActionObjectiveBase> actionObjectiveRegistry, ICoreServerAPI sapi, double passiveCompletionThrottleHours)
        {
            if (player == null || activeQuest == null || questDef?.actionObjectives == null) return;
            if (sapi == null || actionObjectiveRegistry == null) return;

            var wa = player.Entity?.WatchedAttributes;
            if (wa == null) return;

            string throttleKey = $"alegacyvsquest:ao:lastcheck:{activeQuest.questId}";
            double now = sapi.World.Calendar.TotalHours;
            double last = wa.GetDouble(throttleKey, -999999);
			double throttle = passiveCompletionThrottleHours;
			if (throttle <= 0) throttle = 1.0 / 3600.0;
			if (now - last < throttle) return;
            wa.SetDouble(throttleKey, now);
            // Only mark dirty if value actually changed to avoid unnecessary network sync
            if (Math.Abs(now - last) > 0.001)
            {
                wa.MarkPathDirty(throttleKey);
            }

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
