using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace VsQuest
{
    public class BlockQuestSpawner : Block
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (world == null || byPlayer == null || blockSel == null) return false;

            var be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityQuestSpawner;
            if (be == null) return false;

            // Optional quick toggle without opening UI
            if (world.Side == EnumAppSide.Server && byPlayer.Entity?.Controls?.Sneak == true)
            {
                be.ToggleEnabled();
                return true;
            }

            // Quick add from item (creative helper)
            var slot = byPlayer.InventoryManager?.ActiveHotbarSlot;
            var stack = slot?.Itemstack;
            if (world.Side == EnumAppSide.Server && stack?.Collectible?.Code != null && stack.Collectible.Code.ToShortString() == "alegacyvsquest:entityspawner")
            {
                bool added = be.TryAppendFromEntitySpawnerItem(stack);
                var sapi = world.Api as Vintagestory.API.Server.ICoreServerAPI;
                if (sapi != null)
                {
                    sapi.SendMessage(byPlayer as Vintagestory.API.Server.IServerPlayer, GlobalConstants.InfoLogChatGroup, added ? "Added spawn entry" : "Spawn entry already exists", EnumChatType.Notification);
                }
                return true;
            }

            be.OnInteract(byPlayer);
            return true;
        }

        public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos)
        {
            base.OnBlockRemoved(world, pos);

            if (world?.BlockAccessor == null || EntityClass == null) return;
            world.BlockAccessor.RemoveBlockEntity(pos);
        }
    }
}
