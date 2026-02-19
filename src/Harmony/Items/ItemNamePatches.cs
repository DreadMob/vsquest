using HarmonyLib;
using Vintagestory.API.Common;

namespace VsQuest.Harmony.Items
{
    /// <summary>
    /// Item name display patches - handles custom quest item names.
    /// </summary>
    public class ItemNamePatches
    {
        [HarmonyPatch(typeof(CollectibleObject), "GetHeldItemName")]
        public class CollectibleObject_GetHeldItemName_ActionItem_ItemizerName_Patch
        {
            public static void Postfix(ItemStack itemStack, ref string __result)
            {
                if (!VsQuest.HarmonyPatchSwitches.ItemEnabled(VsQuest.HarmonyPatchSwitches.Item_CollectibleObject_GetHeldItemName)) return;
                if (itemStack?.Attributes == null) return;

                string actions = itemStack.Attributes.GetString(ItemAttributeUtils.ActionItemActionsKey);
                if (string.IsNullOrWhiteSpace(actions)) return;

                string customName = itemStack.Attributes.GetString(ItemAttributeUtils.QuestNameKey);
                if (string.IsNullOrWhiteSpace(customName)) return;

                // Preserve VTML/color markup if the stored name already contains it.
                if (customName.IndexOf('<') >= 0)
                {
                    __result = customName;
                    return;
                }

                __result = $"<i>{customName}</i>";
            }
        }
    }
}
