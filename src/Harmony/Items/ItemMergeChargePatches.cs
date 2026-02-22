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
                if (TryHandleCryptSightMaskCharge(op)) return false;
                if (TryHandleCustomItemRepair(op)) return false;
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
                if (TryHandleCryptSightMaskCharge(op)) return false;
                if (TryHandleCustomItemRepair(op)) return false;
                return true;
            }
        }

        [HarmonyPatch(typeof(ItemWearable), "GetMergableQuantity")]
        public class ItemWearable_GetMergableQuantity_SecondChanceCharge_Patch
        {
            public static bool Prefix(ItemWearable __instance, ItemStack sinkStack, ItemStack sourceStack, EnumMergePriority priority, ref int __result)
            {
                if (!VsQuest.HarmonyPatchSwitches.ItemEnabled(VsQuest.HarmonyPatchSwitches.Item_ItemWearable_GetMergableQuantity_SecondChanceCharge)) return true;
                if (CanChargeSecondChance(sinkStack, sourceStack) || CanChargeCryptSightMask(sinkStack, sourceStack) || CanRepairWithCustomItem(sinkStack, sourceStack))
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

        private static bool TryHandleCryptSightMaskCharge(ItemStackMergeOperation op)
        {
            if (op?.SinkSlot?.Itemstack == null || op.SourceSlot?.Itemstack == null) return false;

            var sinkStack = op.SinkSlot.Itemstack;
            if (!CanChargeCryptSightMask(sinkStack, op.SourceSlot.Itemstack)) return false;

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

            // Check if this is a Second Chance mask by item code
            if (!IsSecondChanceMask(sinkStack.Collectible.Code)) return false;

            string chargeKey = ItemAttributeUtils.GetKey(ItemAttributeUtils.AttrSecondChanceCharges);
            // Initialize attribute if missing (for items from creative inventory)
            if (!sinkStack.Attributes.HasAttribute(chargeKey))
            {
                sinkStack.Attributes.SetFloat(chargeKey, 0f);
            }

            if (!IsDiamondRough(sourceStack.Collectible.Code)) return false;
            if (!HasHighPotential(sourceStack)) return false;

            float charges = ItemAttributeUtils.GetAttributeFloat(sinkStack, ItemAttributeUtils.AttrSecondChanceCharges, 0f);
            return charges < 0.5f;
        }

        private static bool CanChargeCryptSightMask(ItemStack sinkStack, ItemStack sourceStack)
        {
            if (sinkStack?.Attributes == null || sourceStack?.Collectible?.Code == null) return false;

            // Check if this is a Crypt Sight Mask by item code
            if (!IsCryptSightMask(sinkStack.Collectible.Code)) return false;

            string chargeKey = ItemAttributeUtils.GetKey(ItemAttributeUtils.AttrUraniumMaskChargeHours);
            // Initialize attribute if missing (for items from creative inventory)
            if (!sinkStack.Attributes.HasAttribute(chargeKey))
            {
                sinkStack.Attributes.SetFloat(chargeKey, 0f);
            }

            if (!IsPhosphoriteChunk(sourceStack.Collectible.Code)) return false;

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

        private static bool IsPhosphoriteChunk(AssetLocation code)
        {
            // Accept: ore-*-phosphorite-*, chunk-phosphorite, gem-phosphorite-*
            string path = code.Path;
            if (!path.Contains("phosphorite")) return false;
            return path.Contains("chunk") || path.Contains("ore") || path.Contains("nugget") || path.Contains("gem");
        }

        private static bool IsCryptSightMask(AssetLocation code)
        {
            return code.Path.Contains("uranium-mask");
        }

        private static bool IsSecondChanceMask(AssetLocation code)
        {
            return code.Path.Contains("2cnah-mask");
        }

        /// <summary>
        /// Handles custom item repair based on repairItemCode attribute.
        /// Items can specify repairItemCode to define what item repairs them.
        /// repairItemCode can be:
        ///   - string: "gear-temporal" (repairs 100%)
        ///   - object: { "code": "gear-temporal", "strength": 0.4 } (repairs 40%)
        /// </summary>
        private static bool TryHandleCustomItemRepair(ItemStackMergeOperation op)
        {
            if (op?.SinkSlot?.Itemstack == null || op.SourceSlot?.Itemstack == null) return false;

            var sinkStack = op.SinkSlot.Itemstack;
            var sourceStack = op.SourceSlot.Itemstack;

            if (!CanRepairWithCustomItem(sinkStack, sourceStack, out float repairStrength)) return false;

            // Repair the item
            float currentCondition = sinkStack.Attributes.GetFloat("condition", 1f);
            float newCondition = System.Math.Min(1f, currentCondition + repairStrength);
            sinkStack.Attributes.SetFloat("condition", newCondition);
            op.MovedQuantity = 1;
            op.SourceSlot.TakeOut(1);
            op.SinkSlot.MarkDirty();
            return true;
        }

        private static bool CanRepairWithCustomItem(ItemStack sinkStack, ItemStack sourceStack, out float repairStrength)
        {
            repairStrength = 1f;
            if (sinkStack?.Collectible?.Code == null || sourceStack?.Collectible?.Code == null) return false;

            // Check if sink item has repairItemCode attribute
            var repairItemAttr = sinkStack.ItemAttributes?["repairItemCode"];
            if (repairItemAttr == null || !repairItemAttr.Exists) return false;

            string repairItemCode;
            
            // Check if repairItemCode is an object with code and strength
            if (repairItemAttr.Token is Newtonsoft.Json.Linq.JObject obj)
            {
                repairItemCode = obj["code"]?.ToString();
                if (obj["strength"] != null)
                {
                    repairStrength = (float)obj["strength"];
                }
            }
            else
            {
                // Simple string format
                repairItemCode = repairItemAttr.AsString(null);
            }
            
            if (string.IsNullOrEmpty(repairItemCode)) return false;

            // Check if source item matches the repair item code
            string sourceCode = sourceStack.Collectible.Code.ToString();
            if (!string.Equals(sourceCode, repairItemCode, System.StringComparison.OrdinalIgnoreCase) &&
                !sourceCode.EndsWith(repairItemCode, System.StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Check if item needs repair (condition < 1.0)
            float condition = sinkStack.Attributes.GetFloat("condition", 1f);
            return condition < 1f;
        }

        private static bool CanRepairWithCustomItem(ItemStack sinkStack, ItemStack sourceStack)
        {
            return CanRepairWithCustomItem(sinkStack, sourceStack, out _);
        }
    }
}
