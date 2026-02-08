using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace VsQuest
{
    public class BlockAshFloor : Block
    {
        public override float OnGettingBroken(IPlayer player, BlockSelection blockSel, ItemSlot itemslot, float remainingResistance, float dt, int counter)
        {
            if (player != null && player.WorldData?.CurrentGameMode != EnumGameMode.Creative)
            {
                return remainingResistance;
            }

            return base.OnGettingBroken(player, blockSel, itemslot, remainingResistance, dt, counter);
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            if (byPlayer != null && byPlayer.WorldData?.CurrentGameMode != EnumGameMode.Creative)
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

        public override void OnEntityInside(IWorldAccessor world, Entity entity, BlockPos pos)
        {
            // base.OnEntityInside(world, entity, pos); // Не вызываем базу, чтобы не было лишних расчетов

            if (world.Side != EnumAppSide.Server) return;

            // Наносим урон и дебаффы только на сервере
            if (entity is EntityPlayer player && player.Alive)
            {
                var be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityAshFloor;
                be?.OnEntityCollision(player);
            }
        }

        public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos)
        {
            base.OnBlockRemoved(world, pos);

            if (world?.BlockAccessor == null || EntityClass == null) return;
            world.BlockAccessor.RemoveBlockEntity(pos);
        }
    }
}
