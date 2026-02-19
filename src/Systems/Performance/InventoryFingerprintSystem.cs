using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace VsQuest.Systems.Performance
{
    /// <summary>
    /// Fast inventory fingerprinting system to avoid recalculating wearable stats
    /// when inventory hasn't actually changed in a meaningful way.
    /// </summary>
    public class InventoryFingerprintSystem
    {
        // Last computed hash per player
        private static readonly Dictionary<long, long> LastInventoryHash = new();

        // Timestamp of last hash check (rate limiting)
        private static readonly Dictionary<long, long> LastHashCheckTime = new();

        // Minimum time between hash checks (ms) - loaded from config
        private static int MinHashCheckIntervalMs => PerformanceConfig.FingerprintCheckIntervalMs;

        /// <summary>
        /// Checks if fingerprinting is enabled in config.
        /// </summary>
        public static bool IsEnabled => PerformanceConfig.EnablePerformanceOptimizations
            && PerformanceConfig.EnableInventoryFingerprinting;

        /// <summary>
        /// Computes a fast hash of the wearable items in an inventory.
        /// Only considers items that affect wearable stats.
        /// </summary>
        public static long ComputeInventoryHash(IInventory inv)
        {
            if (inv == null) return 0;

            long hash = 17; // Prime seed
            int slotIndex = 0;

            foreach (ItemSlot slot in inv)
            {
                if (slot?.Empty != false)
                {
                    slotIndex++;
                    continue;
                }

                var stack = slot.Itemstack;
                if (stack?.Item is not Vintagestory.GameContent.ItemWearable)
                {
                    slotIndex++;
                    continue;
                }

                // Combine multiple factors into hash
                // Item code hash
                hash = hash * 31 + (stack.Collectible?.Code?.ToString()?.GetHashCode() ?? 0);

                // Durability (affects stats if broken)
                hash = hash * 31 + stack.Collectible.GetRemainingDurability(stack);

                // Slot position (order matters for some calculations)
                hash = hash * 31 + slotIndex;

                // Key attributes that affect wearable stats
                if (stack.Attributes != null)
                {
                    // Uranium mask charge
                    if (stack.Attributes.HasAttribute("uraniumMaskChargeHours"))
                    {
                        hash = hash * 31 + stack.Attributes.GetFloat("uraniumMaskChargeHours").GetHashCode();
                    }

                    // Second chance charges
                    if (stack.Attributes.HasAttribute("secondChanceCharges"))
                    {
                        hash = hash * 31 + stack.Attributes.GetFloat("secondChanceCharges").GetHashCode();
                    }
                }

                slotIndex++;
            }

            return hash;
        }

        /// <summary>
        /// Checks if wearable stats should be recalculated for this player.
        /// Uses hash comparison with rate limiting for performance.
        /// </summary>
        /// <returns>True if inventory changed and recalculation is needed</returns>
        public static bool ShouldRecalculateWearableStats(EntityPlayer player)
        {
            // If disabled, always allow recalculation
            if (!IsEnabled) return true;

            // If rapid-call skipping is enabled, check rate limit first
            if (PerformanceConfig.SkipFingerprintOnRapidCalls)
            {
                if (!ShouldCheckFingerprint(player)) return false;
            }

            if (player?.EntityId == null) return false;

            long entityId = player.EntityId;

            // Get character inventory
            var inv = player.Player?.InventoryManager?.GetOwnInventory("character");
            if (inv == null) return false;

            // Compute current hash
            long currentHash = ComputeInventoryHash(inv);

            // Compare with last hash
            if (LastInventoryHash.TryGetValue(entityId, out long lastHash))
            {
                if (lastHash == currentHash)
                {
                    // No meaningful changes detected
                    return false;
                }
            }

            // Hash changed - update stored hash and signal recalculation needed
            LastInventoryHash[entityId] = currentHash;
            return true;
        }

        /// <summary>
        /// Checks if enough time has passed to compute fingerprint.
        /// </summary>
        private static bool ShouldCheckFingerprint(EntityPlayer player)
        {
            if (player?.EntityId == null) return false;

            long entityId = player.EntityId;
            long now = player.World?.ElapsedMilliseconds ?? 0;

            // Rate limiting: don't check more than once every X ms
            if (LastHashCheckTime.TryGetValue(entityId, out long lastCheck))
            {
                if (now - lastCheck < MinHashCheckIntervalMs)
                {
                    return false; // Too soon, skip check
                }
            }

            LastHashCheckTime[entityId] = now;
            return true;
        }

        /// <summary>
        /// Forces a recalculation on next check by clearing the stored hash.
        /// Call this when you know stats need refresh (e.g., manual equip/unequip).
        /// </summary>
        public static void InvalidateCache(long entityId)
        {
            LastInventoryHash.Remove(entityId);
            LastHashCheckTime.Remove(entityId);
        }

        /// <summary>
        /// Clears all cached hashes. Call on server restart or major state change.
        /// </summary>
        public static void ClearAllCaches()
        {
            LastInventoryHash.Clear();
            LastHashCheckTime.Clear();
        }

        /// <summary>
        /// Gets debug info about cache state.
        /// </summary>
        public static (int cachedPlayers, int totalHashes) GetCacheStats()
        {
            return (LastInventoryHash.Count, LastInventoryHash.Count + LastHashCheckTime.Count);
        }
    }
}
