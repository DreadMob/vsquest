using HarmonyLib;
using System;
using Vintagestory.API.Common;
using VsQuest;

namespace VsQuest.Harmony
{
    public static class ItemMoveActingPlayerContext
    {
        [ThreadStatic]
        public static IPlayer ActingPlayer;

        public static void Set(IPlayer player)
        {
            ActingPlayer = player;
        }

        public static void Clear()
        {
            ActingPlayer = null;
        }
    }

    public class ItemMoveActingPlayerContextPatch
    {
        [HarmonyPatch(typeof(InventoryBase), nameof(InventoryBase.ActivateSlot))]
        public class InventoryBase_ActivateSlot_Patch
        {
            public static void Prefix(ref ItemStackMoveOperation op)
            {
                if (!HarmonyPatchSwitches.ItemMoveActingPlayerContextEnabled(HarmonyPatchSwitches.ItemMoveActingPlayerContext_InventoryBase_ActivateSlot)) return;
                ItemMoveActingPlayerContext.Set(op?.ActingPlayer);
            }

            public static void Postfix()
            {
                if (!HarmonyPatchSwitches.ItemMoveActingPlayerContextEnabled(HarmonyPatchSwitches.ItemMoveActingPlayerContext_InventoryBase_ActivateSlot)) return;
                ItemMoveActingPlayerContext.Clear();
            }
        }

        [HarmonyPatch(typeof(ItemSlot), nameof(ItemSlot.ActivateSlot))]
        public class ItemSlot_ActivateSlot_Patch
        {
            public static void Prefix(ref ItemStackMoveOperation op)
            {
                if (!HarmonyPatchSwitches.ItemMoveActingPlayerContextEnabled(HarmonyPatchSwitches.ItemMoveActingPlayerContext_ItemSlot_ActivateSlot)) return;
                ItemMoveActingPlayerContext.Set(op?.ActingPlayer);
            }

            public static void Postfix()
            {
                if (!HarmonyPatchSwitches.ItemMoveActingPlayerContextEnabled(HarmonyPatchSwitches.ItemMoveActingPlayerContext_ItemSlot_ActivateSlot)) return;
                ItemMoveActingPlayerContext.Clear();
            }
        }
    }
}
