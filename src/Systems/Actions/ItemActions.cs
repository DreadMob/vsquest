using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public static class ItemActions
    {
        public static void GiveItem(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (args.Length < 2)
            {
                throw new QuestException("The 'giveitem' action requires at least 2 arguments: itemCode and amount.");
            }

            string code = args[0];
            int amount = int.Parse(args[1]);

            CollectibleObject item = sapi.World.GetItem(new AssetLocation(code));
            if (item == null)
            {
                item = sapi.World.GetBlock(new AssetLocation(code));
            }
            if (item == null)
            {
                throw new QuestException(string.Format("Could not find item {0} for quest {1}!", code, message.questId));
            }

            var stack = new ItemStack(item, amount);

            // Itemizer integration
            if (args.Length > 2)
            {
                stack.Attributes.SetString("itemizerName", args[2]);
            }
            if (args.Length > 3)
            {
                string desc = string.Join(" ", args, 3, args.Length - 3);
                stack.Attributes.SetString("itemizerDesc", desc);
            }

            if (!byPlayer.InventoryManager.TryGiveItemstack(stack))
            {
                sapi.World.SpawnItemEntity(stack, byPlayer.Entity.ServerPos.XYZ);
            }
        }

        public static void GiveActionItem(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {

            if (args.Length < 1)
            {
                throw new QuestException("The 'questitem' action requires at least 1 argument: actionItemId.");
            }

            var itemSystem = sapi.ModLoader.GetModSystem<ItemSystem>();
            if (itemSystem.ActionItemRegistry.TryGetValue(args[0], out var actionItem))
            {
                CollectibleObject collectible = sapi.World.GetItem(new AssetLocation(actionItem.itemCode));
                if (collectible == null)
                {
                    collectible = sapi.World.GetBlock(new AssetLocation(actionItem.itemCode));
                }
                if (collectible != null)
                {
                    var stack = new ItemStack(collectible);
                    VsQuest.Util.ItemAttributeUtils.ApplyActionItemAttributes(stack, actionItem);
                    if (!byPlayer.InventoryManager.TryGiveItemstack(stack))
                    {
                        sapi.World.SpawnItemEntity(stack, byPlayer.Entity.ServerPos.XYZ);
                    }
                }
            }
        }
    }
}
