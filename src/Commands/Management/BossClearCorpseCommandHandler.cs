using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class BossClearCorpseCommandHandler
    {
        private readonly ICoreServerAPI sapi;

        public BossClearCorpseCommandHandler(ICoreServerAPI sapi)
        {
            this.sapi = sapi;
        }

        public TextCommandResult Handle(TextCommandCallingArgs args)
        {
            int radiusBlocks = 0;
            try
            {
                if (args?.Parsers != null && args.Parsers.Count > 0)
                {
                    radiusBlocks = (int)args.Parsers[0].GetValue();
                }
            }
            catch
            {
                radiusBlocks = 0;
            }

            if (sapi?.World?.LoadedEntities == null)
            {
                return TextCommandResult.Error("World entities not available.");
            }

            Vec3d callerPos = null;
            int callerDim = 0;
            if (args.Caller?.Entity?.ServerPos != null)
            {
                callerPos = args.Caller.Entity.ServerPos.XYZ;
                callerDim = args.Caller.Entity.ServerPos.Dimension;
            }

            int clearedCount = 0;
            double radiusSq = radiusBlocks > 0 ? radiusBlocks * (double)radiusBlocks : 0;

            try
            {
                foreach (var entity in sapi.World.LoadedEntities.Values)
                {
                    if (entity == null) continue;
                    if (entity.Alive) continue;

                    var bossMarker = entity.GetBehavior<EntityBehaviorBossCombatMarker>();
                    if (bossMarker == null) continue;

                    if (radiusBlocks > 0 && callerPos != null)
                    {
                        if (entity.ServerPos?.Dimension != callerDim) continue;

                        double dx = entity.ServerPos.X - callerPos.X;
                        double dy = entity.ServerPos.Y - callerPos.Y;
                        double dz = entity.ServerPos.Z - callerPos.Z;

                        if (dx * dx + dy * dy + dz * dz > radiusSq) continue;
                    }

                    try
                    {
                        sapi.World.DespawnEntity(entity, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                        clearedCount++;
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }

            string radiusMsg = radiusBlocks > 0 ? $" within {radiusBlocks} blocks" : " in all loaded chunks";
            return TextCommandResult.Success($"Cleared {clearedCount} boss corpse(s){radiusMsg}.");
        }
    }
}
