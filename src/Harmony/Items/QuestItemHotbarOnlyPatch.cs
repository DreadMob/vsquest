using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace VsQuest.Harmony
{
    public class QuestItemHotbarOnlyPatch
    {
        private static bool IsHotbarSlot(ItemSlot slot)
        {
            return slot?.Inventory?.ClassName == GlobalConstants.hotBarInvClassName;
        }

        private static bool IsMouseSlot(ItemSlot slot)
        {
            return slot?.Inventory?.ClassName == GlobalConstants.mousecursorInvClassName;
        }

        private static bool IsAllowedSlot(ItemSlot slot)
        {
            return IsHotbarSlot(slot) || IsMouseSlot(slot);
        }

        private static bool IsRestrictedMoveItem(ItemSlot sourceSlot)
        {
            if (sourceSlot?.Itemstack == null) return false;
            return ItemAttributeUtils.IsActionItemBlockedMove(sourceSlot.Itemstack);
        }

        [HarmonyPatch(typeof(ItemSlot), nameof(ItemSlot.CanTakeFrom))]
        public class ItemSlot_CanTakeFrom_Patch
        {
            public static bool Prefix(ItemSlot __instance, ItemSlot sourceSlot, EnumMergePriority priority, ref bool __result)
            {
                if (IsRestrictedMoveItem(sourceSlot) && !IsAllowedSlot(__instance))
                {
                    __result = false;
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(ItemSlot), nameof(ItemSlot.CanHold))]
        public class ItemSlot_CanHold_Patch
        {
            public static bool Prefix(ItemSlot __instance, ItemSlot sourceSlot, ref bool __result)
            {
                if (IsRestrictedMoveItem(sourceSlot) && !IsAllowedSlot(__instance))
                {
                    __result = false;
                    return false;
                }

                return true;
            }
        }
    }
}
