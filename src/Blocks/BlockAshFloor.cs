using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VsQuest
{
    public class BlockAshFloor : Block
    {
        private static bool IsCreative(IPlayer byPlayer)
        {
            try
            {
                return byPlayer?.WorldData?.CurrentGameMode == EnumGameMode.Creative;
            }
            catch
            {
                return false;
            }
        }

        public override float OnGettingBroken(IPlayer player, BlockSelection blockSel, ItemSlot itemslot, float remainingResistance, float dt, int counter)
        {
            if (player != null && !IsCreative(player))
            {
                return remainingResistance;
            }

            return base.OnGettingBroken(player, blockSel, itemslot, remainingResistance, dt, counter);
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            if (byPlayer != null && !IsCreative(byPlayer))
            {
                return;
            }

            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
        }

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
