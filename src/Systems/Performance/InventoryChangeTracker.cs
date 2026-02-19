using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace VsQuest.Systems.Performance
{
    /// <summary>
    /// Tracks inventory changes to avoid recalculating wearable stats on every damage tick.
    /// Only triggers recalculation when character inventory actually changes.
    /// </summary>
    public class InventoryChangeTracker
    {
        private readonly ICoreAPI api;
        private readonly Dictionary<long, InventoryFingerprint> lastFingerprints = new();
        private readonly Dictionary<long, long> lastCheckTimes = new();
        private const int CheckIntervalMs = 500; // Check max twice per second per player
        
        public InventoryChangeTracker(ICoreAPI api)
        {
            this.api = api;
        }

        /// <summary>
        /// Returns true if inventory changed since last check and stats should be recalculated.
        /// Uses fingerprinting for fast comparison and throttling to reduce CPU usage.
        /// </summary>
        public bool ShouldRecalculate(EntityPlayer player)
        {
            if (player?.Player?.InventoryManager == null) return false;
            
            long entityId = player.EntityId;
            long now = api.World.ElapsedMilliseconds;
            
            // Throttle checks
            if (lastCheckTimes.TryGetValue(entityId, out long lastCheck))
            {
                if ((now - lastCheck) < CheckIntervalMs)
                {
                    return false; // Too soon, use cached
                }
            }
            lastCheckTimes[entityId] = now;
            
            // Get current fingerprint
            var currentFingerprint = GetInventoryFingerprint(player);
            
            // Compare with last
            if (lastFingerprints.TryGetValue(entityId, out var lastFp))
            {
                if (lastFp.Equals(currentFingerprint))
                {
                    return false; // No change
                }
            }
            
            // Store new fingerprint
            lastFingerprints[entityId] = currentFingerprint;
            return true; // Changed, needs recalc
        }

        /// <summary>
        /// Forces recalculation on next check by clearing fingerprint.
        /// Call this when you know equipment changed (e.g., item equipped/unequipped).
        /// </summary>
        public void Invalidate(long entityId)
        {
            lastFingerprints.Remove(entityId);
            lastCheckTimes.Remove(entityId);
        }

        /// <summary>
        /// Creates a fast fingerprint of character inventory equipment slots.
        /// Only checks armor/accessory slots, not entire inventory.
        /// </summary>
        private InventoryFingerprint GetInventoryFingerprint(EntityPlayer player)
        {
            var inv = player.Player.InventoryManager.GetOwnInventory("character");
            if (inv == null) return new InventoryFingerprint(0, 0);
            
            // Fast hash based on equipped items
            int hash = 0;
            int count = 0;
            
            foreach (var slot in inv)
            {
                if (slot?.Empty != false) continue;
                if (!(slot.Itemstack.Item is ItemWearable)) continue;
                
                // Combine item code hash and stack fingerprint
                var stack = slot.Itemstack;
                int itemHash = stack.Collectible?.Code?.GetHashCode() ?? 0;
                int attrHash = stack.Attributes?.GetHashCode() ?? 0;
                
                hash = hash * 31 + itemHash;
                hash = hash * 31 + attrHash;
                count++;
            }
            
            return new InventoryFingerprint(hash, count);
        }

        /// <summary>
        /// Lightweight struct for inventory fingerprint comparison.
        /// </summary>
        private readonly struct InventoryFingerprint : IEquatable<InventoryFingerprint>
        {
            public readonly int Hash;
            public readonly int Count;
            
            public InventoryFingerprint(int hash, int count)
            {
                Hash = hash;
                Count = count;
            }
            
            public bool Equals(InventoryFingerprint other)
            {
                return Hash == other.Hash && Count == other.Count;
            }
            
            public override bool Equals(object obj)
            {
                return obj is InventoryFingerprint other && Equals(other);
            }
            
            public override int GetHashCode()
            {
                return Hash * 31 + Count;
            }
        }
    }
}
