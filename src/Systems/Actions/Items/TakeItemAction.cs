using System;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class TakeItemAction : IQuestAction
    {
        public void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (byPlayer?.InventoryManager?.Inventories == null) return;

            if (args == null || args.Length < 2)
            {
                throw new QuestException("The 'takeitem' action requires 2 arguments: itemCode and amount.");
            }

            string itemCode = args[0];
            if (string.IsNullOrWhiteSpace(itemCode))
            {
                throw new QuestException("The 'takeitem' action requires a non-empty itemCode.");
            }

            if (!int.TryParse(args[1], out int amount))
            {
                throw new QuestException($"Invalid amount '{args[1]}' for 'takeitem' action in quest {message?.questId}.");
            }

            if (amount <= 0) return;

            int have = CountItems(byPlayer, itemCode);
            if (have < amount)
            {
                sapi.SendMessage(byPlayer, GlobalConstants.GeneralChatGroup, "Not enough items.", EnumChatType.Notification);
                throw new QuestException($"Not enough items for 'takeitem' action. Need {amount} of '{itemCode}', but player has {have}. Quest '{message?.questId}'.");
            }

            int remaining = amount;

            foreach (var inv in byPlayer.InventoryManager.Inventories.Values)
            {
                if (inv == null) continue;
                if (inv.ClassName == GlobalConstants.creativeInvClassName) continue;

                int slotCount;
                try
                {
                    slotCount = inv.Count;
                }
                catch
                {
                    continue;
                }

                for (int i = 0; i < slotCount; i++)
                {
                    if (remaining <= 0) return;

                    var slot = inv[i];
                    if (slot?.Empty != false) continue;

                    var stack = slot.Itemstack;
                    if (stack?.Collectible?.Code == null) continue;

                    string code = stack.Collectible.Code.ToString();
                    if (!CodeMatches(itemCode, code)) continue;

                    int take = Math.Min(remaining, stack.StackSize);
                    slot.TakeOut(take);
                    slot.MarkDirty();

                    remaining -= take;
                }
            }
        }

        private static int CountItems(IServerPlayer byPlayer, string itemCode)
        {
            if (byPlayer?.InventoryManager?.Inventories == null) return 0;

            int itemsFound = 0;
            foreach (var inventory in byPlayer.InventoryManager.Inventories.Values)
            {
                if (inventory == null) continue;
                if (inventory.ClassName == GlobalConstants.creativeInvClassName) continue;

                foreach (var slot in inventory)
                {
                    if (slot?.Empty != false) continue;
                    var stack = slot.Itemstack;
                    if (stack?.Collectible?.Code == null) continue;

                    string code = stack.Collectible.Code.ToString();
                    if (CodeMatches(itemCode, code))
                    {
                        itemsFound += stack.StackSize;
                    }
                }
            }

            return itemsFound;
        }

        private static bool CodeMatches(string expected, string actual)
        {
            if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(actual)) return false;

            expected = expected.Trim();
            actual = actual.Trim();

            if (expected.EndsWith("*") && actual.StartsWith(expected.Substring(0, expected.Length - 1), StringComparison.OrdinalIgnoreCase)) return true;

            return string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
        }
    }
}
