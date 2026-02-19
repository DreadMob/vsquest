using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace VsQuest.Harmony.Items
{
    /// <summary>
    /// Item merge charging patches - handles Second Chance and Uranium Mask charging mechanics.
    /// </summary>
    public class ItemMergeChargePatches
    {
        [HarmonyPatch(typeof(CollectibleObject), "TryMergeStacks")]
        public class CollectibleObject_TryMergeStacks_SecondChanceCharge_Patch
        {
            public static bool Prefix(CollectibleObject __instance, ItemStackMergeOperation op)
            {
                if (!VsQuest.HarmonyPatchSwitches.ItemEnabled(VsQuest.HarmonyPatchSwitches.Item_CollectibleObject_TryMergeStacks_SecondChanceCharge)) return true;
                if (TryHandleSecondChanceCharge(op)) return false;
                if (TryHandleUraniumMaskCharge(op)) return false;
                return true;
            }
        }

        [HarmonyPatch(typeof(ItemWearable), "TryMergeStacks")]
        public class ItemWearable_TryMergeStacks_SecondChanceCharge_Patch
        {
            public static bool Prefix(ItemWearable __instance, ItemStackMergeOperation op)
            {
                if (!VsQuest.HarmonyPatchSwitches.ItemEnabled(VsQuest.HarmonyPatchSwitches.Item_ItemWearable_TryMergeStacks_SecondChanceCharge)) return true;
                if (TryHandleSecondChanceCharge(op)) return false;
                if (TryHandleUraniumMaskCharge(op)) return false;
                return true;
            }
        }

        [HarmonyPatch(typeof(ItemWearable), "GetMergableQuantity")]
        public class ItemWearable_GetMergableQuantity_SecondChanceCharge_Patch
        {
            public static bool Prefix(ItemWearable __instance, ItemStack sinkStack, ItemStack sourceStack, EnumMergePriority priority, ref int __result)
            {
                if (!VsQuest.HarmonyPatchSwitches.ItemEnabled(VsQuest.HarmonyPatchSwitches.Item_ItemWearable_GetMergableQuantity_SecondChanceCharge)) return true;
                if (CanChargeSecondChance(sinkStack, sourceStack) || CanChargeUraniumMask(sinkStack, sourceStack))
                {
                    __result = 1;
                    return false;
                }

                return true;
            }
        }

        private static bool TryHandleSecondChanceCharge(ItemStackMergeOperation op)
        {
            if (op?.SinkSlot?.Itemstack == null || op.SourceSlot?.Itemstack == null) return false;

            var sinkStack = op.SinkSlot.Itemstack;
            if (!CanChargeSecondChance(sinkStack, op.SourceSlot.Itemstack)) return false;

            string chargeKey = ItemAttributeUtils.GetKey(ItemAttributeUtils.AttrSecondChanceCharges);
            sinkStack.Attributes.SetFloat(chargeKey, 1f);
            op.MovedQuantity = 1;
            op.SourceSlot.TakeOut(1);
            op.SinkSlot.MarkDirty();
            return true;
        }

        private static bool TryHandleUraniumMaskCharge(ItemStackMergeOperation op)
        {
            if (op?.SinkSlot?.Itemstack == null || op.SourceSlot?.Itemstack == null) return false;

            var sinkStack = op.SinkSlot.Itemstack;
            if (!CanChargeUraniumMask(sinkStack, op.SourceSlot.Itemstack)) return false;

            string chargeKey = ItemAttributeUtils.GetKey(ItemAttributeUtils.AttrUraniumMaskChargeHours);
            float hours = sinkStack.Attributes.GetFloat(chargeKey, 0f);
            hours = System.Math.Min(100f, hours + 8f);
            sinkStack.Attributes.SetFloat(chargeKey, hours);
            op.MovedQuantity = 1;
            op.SourceSlot.TakeOut(1);
            op.SinkSlot.MarkDirty();
            return true;
        }

        private static bool CanChargeSecondChance(ItemStack sinkStack, ItemStack sourceStack)
        {
            if (sinkStack?.Attributes == null || sourceStack?.Collectible?.Code == null) return false;

            string chargeKey = ItemAttributeUtils.GetKey(ItemAttributeUtils.AttrSecondChanceCharges);
            if (!sinkStack.Attributes.HasAttribute(chargeKey)) return false;

            if (!IsDiamondRough(sourceStack.Collectible.Code)) return false;
            if (!HasHighPotential(sourceStack)) return false;

            float charges = ItemAttributeUtils.GetAttributeFloat(sinkStack, ItemAttributeUtils.AttrSecondChanceCharges, 0f);
            return charges < 0.5f;
        }

        private static bool CanChargeUraniumMask(ItemStack sinkStack, ItemStack sourceStack)
        {
            if (sinkStack?.Attributes == null || sourceStack?.Collectible?.Code == null) return false;

            string chargeKey = ItemAttributeUtils.GetKey(ItemAttributeUtils.AttrUraniumMaskChargeHours);
            if (!sinkStack.Attributes.HasAttribute(chargeKey)) return false;

            if (!IsUraniumChunk(sourceStack.Collectible.Code)) return false;

            float hours = ItemAttributeUtils.GetAttributeFloat(sinkStack, ItemAttributeUtils.AttrUraniumMaskChargeHours, 0f);
            return hours < 100f;
        }

        private static bool IsDiamondRough(AssetLocation code)
        {
            return code.Path.Contains("diamond-rough") || code.Path.Contains("rough-diamond");
        }

        private static bool HasHighPotential(ItemStack stack)
        {
            // Check if the diamond has high potential (for second chance charging)
            string potential = stack.Attributes?.GetString("potential");
            if (!string.IsNullOrEmpty(potential))
            {
                return potential == "high" || potential == "veryhigh" || potential == "excellent";
            }
            // If no potential attribute, assume it's valid (for backward compatibility)
            return true;
        }

        private static bool IsUraniumChunk(AssetLocation code)
        {
            return code.Path.Contains("uranium") && (code.Path.Contains("chunk") || code.Path.Contains("ore"));
        }
    }
}
