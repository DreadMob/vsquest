using System;
using System.Linq;
using Vintagestory.API.Server;

namespace VsQuest
{
    public static class QuestInteractAtUtil
    {
        public static void ResetCompletedInteractAtObjectives(Quest quest, IServerPlayer serverPlayer)
        {
            if (quest?.actionObjectives == null || quest.actionObjectives.Count == 0) return;
            if (serverPlayer?.Entity?.WatchedAttributes == null) return;

            var wa = serverPlayer.Entity.WatchedAttributes;

            try
            {
                string completedInteractions = wa.GetString("completedInteractions", "");
                if (string.IsNullOrWhiteSpace(completedInteractions)) return;

                var completed = completedInteractions.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                bool changed = false;

                foreach (var ao in quest.actionObjectives)
                {
                    if (ao?.id != "interactat" || ao.args == null || ao.args.Length < 1) continue;
                    var coordString = ao.args[0];
                    if (string.IsNullOrWhiteSpace(coordString)) continue;

                    var coords = coordString.Split(',');
                    if (coords.Length != 3) continue;

                    if (!int.TryParse(coords[0], out int x) ||
                        !int.TryParse(coords[1], out int y) ||
                        !int.TryParse(coords[2], out int z))
                    {
                        continue;
                    }

                    string interactionKey = $"interactat_{x}_{y}_{z}";
                    if (completed.Remove(interactionKey)) changed = true;
                }

                if (changed)
                {
                    wa.SetString("completedInteractions", string.Join(",", completed));
                    wa.MarkPathDirty("completedInteractions");
                }
            }
            catch
            {
            }
        }

        public static void TryHandleInteractAtObjectives(Quest quest, ActiveQuest activeQuest, IServerPlayer serverPlayer, int[] position, ICoreServerAPI sapi)
        {
            if (quest?.actionObjectives == null || quest.actionObjectives.Count == 0) return;
            if (serverPlayer?.Entity?.WatchedAttributes == null) return;
            if (position == null || position.Length != 3) return;

            var wa = serverPlayer.Entity.WatchedAttributes;

            for (int i = 0; i < quest.actionObjectives.Count; i++)
            {
                var ao = quest.actionObjectives[i];
                if (ao?.id != "interactat" || ao.args == null || ao.args.Length < 1) continue;

                var coordString = ao.args[0];
                if (string.IsNullOrWhiteSpace(coordString)) continue;

                var coords = coordString.Split(',');
                if (coords.Length != 3) continue;

                if (!int.TryParse(coords[0], out int targetX) ||
                    !int.TryParse(coords[1], out int targetY) ||
                    !int.TryParse(coords[2], out int targetZ))
                {
                    continue;
                }

                if (position[0] != targetX || position[1] != targetY || position[2] != targetZ) continue;

                string interactionKey = $"interactat_{targetX}_{targetY}_{targetZ}";
                string completedInteractions = wa.GetString("completedInteractions", "");
                var completed = completedInteractions.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();

                if (!completed.Contains(interactionKey))
                {
                    completed.Add(interactionKey);
                    wa.SetString("completedInteractions", string.Join(",", completed));
                    wa.MarkPathDirty("completedInteractions");

                    if (ao.args.Length >= 2 && !string.IsNullOrWhiteSpace(ao.args[1]))
                    {
                        var message = new QuestAcceptedMessage { questGiverId = activeQuest.questGiverId, questId = activeQuest.questId };
                        ActionStringExecutor.Execute(sapi, message, serverPlayer, ao.args[1]);
                    }
                }
            }
        }
    }
}
