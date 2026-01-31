using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class LandClaimMaxAreasAction : IQuestAction
    {
        public void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (byPlayer?.Entity?.WatchedAttributes == null) return;

            var key = "landclaimmaxareas";

            if (args == null || args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
            {
                return;
            }

            if (!int.TryParse(args[0], out int value))
            {
                sapi.Logger.Error($"[vsquest] 'landclaimmaxareas' action argument 'value' must be an int, but got '{args[0]}' in quest '{message?.questId}'.");
                return;
            }

            byPlayer.Entity.WatchedAttributes.SetInt(key, value);
            byPlayer.Entity.WatchedAttributes.MarkPathDirty(key);
        }
    }
}
