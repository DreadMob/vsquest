using System;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class BossHuntReloadCommandHandler
    {
        private readonly ICoreServerAPI sapi;

        public BossHuntReloadCommandHandler(ICoreServerAPI sapi)
        {
            this.sapi = sapi;
        }

        public TextCommandResult Handle(TextCommandCallingArgs args)
        {
            var bossSystem = sapi?.ModLoader?.GetModSystem<BossHuntSystem>();
            if (bossSystem == null)
            {
                return TextCommandResult.Error("BossHuntSystem not available.");
            }

            int radiusBlocks = 512;
            try
            {
                if (args?.Parsers != null && args.Parsers.Count > 0)
                {
                    radiusBlocks = (int)args.Parsers[0].GetValue();
                }
            }
            catch
            {
                radiusBlocks = 512;
            }

            try
            {
                if (!bossSystem.TryReloadAnchors(radiusBlocks, out int clearedAnchors, out int reRegisteredAnchors, out string details))
                {
                    return TextCommandResult.Error(details ?? "Failed to reload bosshunt anchors.");
                }

                string msg = $"Bosshunt anchors reloaded. Cleared: {clearedAnchors}, re-registered (loaded chunks scan): {reRegisteredAnchors}.";
                if (!string.IsNullOrWhiteSpace(details)) msg += $" {details}";

                return TextCommandResult.Success(msg);
            }
            catch (Exception e)
            {
                try
                {
                    sapi?.Logger?.Error("[alegacyvsquest] /avq bosshunt reload failed: {0}", e);
                }
                catch
                {
                }

                return TextCommandResult.Error($"Bosshunt reload failed: {e.Message}");
            }
        }
    }
}
