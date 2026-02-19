using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using VsQuest.Systems.Performance;

namespace VsQuest.Harmony.Items
{
    /// <summary>
    /// Inventory change detection patches - invalidate wearable stats cache when equipment changes.
    /// This ensures stats are recalculated only when needed, not on every damage tick.
    /// </summary>
    public class InventoryChangePatches
    {
        // Shared tracker instance
        private static InventoryChangeTracker _tracker;
        
        private static InventoryChangeTracker GetTracker(ICoreAPI api)
        {
            if (_tracker == null && api != null)
            {
                _tracker = new InventoryChangeTracker(api);
            }
            return _tracker;
        }

        /// <summary>
        /// Patches slot activation (equip/unequip) to invalidate cache.
        /// </summary>
        [HarmonyPatch(typeof(ItemSlot), nameof(ItemSlot.ActivateSlot))]
        public class ItemSlot_ActivateSlot_InvalidateCache_Patch
        {
            public static void Postfix(ItemSlot __instance)
            {
                if (!HarmonyPatchSwitches.ItemEnabled(HarmonyPatchSwitches.Item_InventoryChangeTracking)) return;
                
                // Only care about character inventory (equipment slots)
                var inv = __instance?.Inventory;
                if (inv?.ClassName != "character") return;
                
                // Check if it's a wearable item
                if (__instance.Itemstack?.Item is not ItemWearable) return;
                
                // Find player who owns this inventory
                var player = FindPlayerFromInventory(inv);
                if (player?.Entity is EntityPlayer entityPlayer)
                {
                    var tracker = GetTracker(player.Entity.Api);
                    tracker?.Invalidate(entityPlayer.EntityId);
                    
                    // Also invalidate wearable stats cache
                    WearableStatsCache.InvalidateCache(entityPlayer);
                }
            }
        }

        /// <summary>
        /// Patches slot marking dirty (item changes) to invalidate cache.
        /// </summary>
        [HarmonyPatch(typeof(ItemSlot), nameof(ItemSlot.MarkDirty))]
        public class ItemSlot_MarkDirty_InvalidateCache_Patch
        {
            public static void Postfix(ItemSlot __instance)
            {
                if (!HarmonyPatchSwitches.ItemEnabled(HarmonyPatchSwitches.Item_InventoryChangeTracking)) return;
                
                // Only care about character inventory
                var inv = __instance?.Inventory;
                if (inv?.ClassName != "character") return;
                
                // Check if it's a wearable item
                if (__instance.Itemstack?.Item is not ItemWearable) return;
                
                // Find player who owns this inventory
                var player = FindPlayerFromInventory(inv);
                if (player?.Entity is EntityPlayer entityPlayer)
                {
                    var tracker = GetTracker(player.Entity.Api);
                    tracker?.Invalidate(entityPlayer.EntityId);
                    WearableStatsCache.InvalidateCache(entityPlayer);
                }
            }
        }

        /// <summary>
        /// Patches item taking to invalidate cache when item removed.
        /// </summary>
        [HarmonyPatch(typeof(ItemSlot), "TryTakeOut")]
        public class ItemSlot_TryTakeOut_InvalidateCache_Patch
        {
            public static void Postfix(ItemSlot __instance)
            {
                if (!HarmonyPatchSwitches.ItemEnabled(HarmonyPatchSwitches.Item_InventoryChangeTracking)) return;
                
                var inv = __instance?.Inventory;
                if (inv?.ClassName != "character") return;
                
                // Find player who owns this inventory
                var player = FindPlayerFromInventory(inv);
                if (player?.Entity is EntityPlayer entityPlayer)
                {
                    var tracker = GetTracker(player.Entity.Api);
                    tracker?.Invalidate(entityPlayer.EntityId);
                    WearableStatsCache.InvalidateCache(entityPlayer);
                }
            }
        }
        
        /// <summary>
        /// Helper to find player from their character inventory.
        /// </summary>
        private static IPlayer FindPlayerFromInventory(IInventory inv)
        {
            // Cast to InventoryBase to access Api property
            var invBase = inv as InventoryBase;
            if (invBase?.Api?.Side != EnumAppSide.Server) return null;
            
            var sapi = invBase.Api as ICoreServerAPI;
            if (sapi?.World?.AllPlayers == null) return null;
            
            foreach (var plr in sapi.World.AllPlayers)
            {
                if (plr?.InventoryManager?.GetOwnInventory("character") == inv)
                {
                    return plr;
                }
            }
            return null;
        }
    }
}
