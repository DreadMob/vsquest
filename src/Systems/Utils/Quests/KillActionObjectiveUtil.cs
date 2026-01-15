using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace VsQuest
{
    public static class KillActionObjectiveUtil
    {
        public static void TryHandleKill(ICoreServerAPI sapi, IServerPlayer player, ActiveQuest activeQuest, Entity killedEntity)
        {
            if (sapi == null || player == null || activeQuest == null || killedEntity == null) return;

            var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();
            if (questSystem?.QuestRegistry == null || questSystem.ActionObjectiveRegistry == null) return;

            if (!questSystem.QuestRegistry.TryGetValue(activeQuest.questId, out var questDef) || questDef?.actionObjectives == null) return;

            string killedTargetId = killedEntity.GetBehavior<EntityBehaviorQuestTarget>()?.TargetId;
            if (string.IsNullOrWhiteSpace(killedTargetId)) return;

            for (int i = 0; i < questDef.actionObjectives.Count; i++)
            {
                var ao = questDef.actionObjectives[i];
                if (ao == null || ao.args == null) continue;

                if (ao.id != "killactiontarget") continue;
                if (string.IsNullOrWhiteSpace(ao.objectiveId)) continue;

                if (!TryParseArgs(ao.args, out string questId, out string objectiveId, out string requiredTargetId, out int needed)) continue;
                if (!string.Equals(questId, activeQuest.questId, StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.Equals(objectiveId, ao.objectiveId, StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.Equals(requiredTargetId, killedTargetId, StringComparison.OrdinalIgnoreCase)) continue;

                if (!QuestTimeGateUtil.AllowsProgress(player, questDef, questSystem.ActionObjectiveRegistry, "kill", ao.objectiveId)) continue;

                if (questSystem.ActionObjectiveRegistry.TryGetValue(ao.id, out var impl) && impl is KillActionTargetObjective ko)
                {
                    string key = ko.CountKey(questId, objectiveId);
                    var wa = player.Entity?.WatchedAttributes;
                    if (wa == null) continue;

                    int have = wa.GetInt(key, 0);
                    if (have < needed)
                    {
                        have++;
                        wa.SetInt(key, have);
                        wa.MarkPathDirty(key);
                    }

                    bool ok;
                    try
                    {
                        ok = impl.IsCompletable(player, ao.args);
                    }
                    catch
                    {
                        ok = false;
                    }

                    QuestActionObjectiveCompletionUtil.TryFireOnComplete(sapi, player, activeQuest, ao, ao.objectiveId, ok);
                }

                break;
            }
        }

        private static bool TryParseArgs(string[] args, out string questId, out string objectiveId, out string targetId, out int needed)
        {
            questId = null;
            objectiveId = null;
            targetId = null;
            needed = 0;

            if (args == null || args.Length < 4) return false;

            questId = args[0];
            objectiveId = args[1];
            targetId = args[2];

            if (string.IsNullOrWhiteSpace(questId) || string.IsNullOrWhiteSpace(objectiveId) || string.IsNullOrWhiteSpace(targetId)) return false;

            if (!int.TryParse(args[3], out needed)) needed = 0;
            if (needed < 0) needed = 0;

            return true;
        }
    }
}
