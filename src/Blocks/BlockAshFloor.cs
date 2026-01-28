using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VsQuest
{
    public class BlockAshFloor : Block
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            return false;
        }

        public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            return false;
        }

        public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
        }

        public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos)
        {
            base.OnBlockRemoved(world, pos);

            if (world?.BlockAccessor == null || EntityClass == null) return;
            world.BlockAccessor.RemoveBlockEntity(pos);
        }
    }
}
