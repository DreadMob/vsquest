using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class AddReputationAction : IQuestAction
    {
        public void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (byPlayer?.Entity?.WatchedAttributes == null) return;

            if (args.Length < 3)
            {
                sapi.Logger.Error($"[vsquest] 'addreputation' action requires at least 3 arguments (scope, id, delta) but got {args.Length} in quest '{message?.questId}'.");
                return;
            }

            string scopeRaw = args[0]?.ToLowerInvariant();
            string id = args[1];
            if (string.IsNullOrWhiteSpace(scopeRaw) || string.IsNullOrWhiteSpace(id)) return;

            var repSystem = sapi.ModLoader.GetModSystem<ReputationSystem>();
            if (repSystem == null)
            {
                return;
            }

            if (!repSystem.TryParseScope(scopeRaw, out var scope))
            {
                sapi.Logger.Error($"[vsquest] 'addreputation' action scope must be 'npc' or 'faction', got '{scopeRaw}' in quest '{message?.questId}'.");
                return;
            }

            if (!int.TryParse(args[2], out int delta)) delta = 0;

            int? max = null;
            if (args.Length >= 4 && int.TryParse(args[3], out int parsedMax))
            {
                max = parsedMax;
            }

            string onceKey = null;
            if (args.Length >= 5 && !string.IsNullOrWhiteSpace(args[4]))
            {
                onceKey = args[4];
            }

            var wa = byPlayer.Entity.WatchedAttributes;
            if (!string.IsNullOrWhiteSpace(onceKey) && wa.GetBool(onceKey, false))
            {
                return;
            }

            int current = repSystem.GetReputationValue(byPlayer as IPlayer, scope, id);
            int next = current + delta;

            if (max.HasValue && next > max.Value) next = max.Value;

            repSystem.ApplyReputationChange(sapi, byPlayer, scope, id, next, false);

            if (!string.IsNullOrWhiteSpace(onceKey))
            {
                wa.SetBool(onceKey, true);
                wa.MarkPathDirty(onceKey);
            }
        }
    }
}
