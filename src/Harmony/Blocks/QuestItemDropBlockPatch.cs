using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using Vintagestory.API.Common;

namespace VsQuest.Harmony
{
    public class QuestItemDropBlockPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            // Patch both server and client inventory managers. Using reflection to avoid hard dependency issues.
            var serverType = AccessTools.TypeByName("Vintagestory.Server.ServerPlayerInventoryManager");
            if (serverType != null)
            {
                var m = AccessTools.Method(serverType, "DropItem");
                if (m != null) yield return m;
            }

            var clientType = AccessTools.TypeByName("Vintagestory.Client.NoObf.ClientPlayerInventoryManager");
            if (clientType != null)
            {
                var m = AccessTools.Method(clientType, "DropItem");
                if (m != null) yield return m;
            }
        }

        [HarmonyPatch]
        public class DropItem_Patch
        {
            public static IEnumerable<MethodBase> TargetMethods()
            {
                return QuestItemDropBlockPatch.TargetMethods();
            }

            public static bool Prefix(ItemSlot slot, ref bool __result)
            {
                if (slot?.Itemstack == null) return true;

                if (ItemAttributeUtils.IsActionItemBlockedDrop(slot.Itemstack))
                {
                    __result = false;
                    return false;
                }
                return true;
            }
        }
    }
}
