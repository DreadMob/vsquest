using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace VsQuest.Harmony.Items
{
    /// <summary>
    /// Item name display patches - handles custom quest item names.
    /// </summary>
    public class ItemNamePatches
    {
        [HarmonyPatch(typeof(CollectibleObject), "GetHeldItemName")]
        public class CollectibleObject_GetHeldItemName_Localization_Patch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);
                var getSafeMatchingParamsMethod = typeof(LocalizationUtils).GetMethod("GetSafeMatchingStrictDomains", new[] { typeof(string), typeof(object[]) });

                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode != OpCodes.Call && codes[i].opcode != OpCodes.Callvirt) continue;
                    if (codes[i].operand is not MethodInfo called || called.DeclaringType != typeof(Lang)) continue;
                    if (called.Name != "GetMatching") continue;

                    codes[i] = new CodeInstruction(OpCodes.Call, getSafeMatchingParamsMethod);
                }

                return codes;
            }
        }

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
