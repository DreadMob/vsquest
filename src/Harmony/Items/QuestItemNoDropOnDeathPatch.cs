using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace VsQuest.Harmony
{
    public class QuestItemNoDropOnDeathPatch
    {
        private static readonly Dictionary<string, List<(IInventory inv, int slotIndex, ItemStack stack)>> savedByPlayer = new();
        private static readonly HashSet<string> keepInventoryOnceByPlayer = new(StringComparer.Ordinal);

        public static void SetKeepInventoryOnce(string playerUid)
        {
            if (string.IsNullOrWhiteSpace(playerUid)) return;
            keepInventoryOnceByPlayer.Add(playerUid);
        }

        private static bool IsProtected(ItemStack stack)
        {
            if (stack == null) return false;
            return ItemAttributeUtils.IsActionItemBlockedDeath(stack);
        }

        [HarmonyPatch]
        public class PlayerInventoryManager_OnDeath_Patch
        {
            public static MethodBase TargetMethod()
            {
                var t = AccessTools.TypeByName("Vintagestory.Common.PlayerInventoryManager");
                if (t == null) return null;
                return AccessTools.Method(t, "OnDeath");
            }

            public static bool Prefix(object __instance)
            {
                try
                {
                    var playerField = __instance.GetType().GetField("player", BindingFlags.Instance | BindingFlags.Public);
                    if (playerField == null) return true;
                    var player = playerField.GetValue(__instance) as IPlayer;
                    if (player == null) return true;

                    string uid = player.PlayerUID;
                    if (string.IsNullOrWhiteSpace(uid)) return true;

                    if (keepInventoryOnceByPlayer.Remove(uid))
                    {
                        return false;
                    }

                    var invMgr = player.InventoryManager;
                    if (invMgr?.Inventories == null) return true;

                    var saved = new List<(IInventory inv, int slotIndex, ItemStack stack)>();

                    foreach (var inv in invMgr.Inventories.Values)
                    {
                        if (inv == null) continue;
                        if (inv.ClassName == GlobalConstants.creativeInvClassName) continue;

                        for (int i = 0; i < inv.Count; i++)
                        {
                            var slot = inv[i];
                            if (slot?.Empty != false) continue;

                            var stack = slot.Itemstack;
                            if (!IsProtected(stack)) continue;

                            saved.Add((inv, i, stack));
                            slot.Itemstack = null;
                            slot.MarkDirty();
                        }
                    }

                    if (saved.Count > 0)
                    {
                        savedByPlayer[uid] = saved;
                    }
                }
                catch
                {
                }

                return true;
            }

            public static void Postfix(object __instance)
            {
                try
                {
                    var playerField = __instance.GetType().GetField("player", BindingFlags.Instance | BindingFlags.Public);
                    if (playerField == null) return;
                    var player = playerField.GetValue(__instance) as IPlayer;
                    if (player == null) return;

                    string uid = player.PlayerUID;
                    if (string.IsNullOrWhiteSpace(uid)) return;

                    if (!savedByPlayer.TryGetValue(uid, out var saved) || saved == null || saved.Count == 0) return;
                    savedByPlayer.Remove(uid);

                    // Restore: try original slot first, otherwise try give back to player inventory.
                    var invMgr = player.InventoryManager;
                    if (invMgr == null) return;

                    foreach (var entry in saved)
                    {
                        if (entry.inv != null && entry.slotIndex >= 0 && entry.slotIndex < entry.inv.Count)
                        {
                            var slot = entry.inv[entry.slotIndex];
                            if (slot?.Empty == true)
                            {
                                slot.Itemstack = entry.stack;
                                slot.MarkDirty();
                                continue;
                            }
                        }

                        try
                        {
                            invMgr.TryGiveItemstack(entry.stack);
                        }
                        catch
                        {
                        }
                    }
                }
                catch
                {
                }
            }
        }
    }
}
