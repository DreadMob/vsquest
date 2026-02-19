using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace VsQuest.Systems.Performance
{
    /// <summary>
    /// Coalesces multiple stat updates within a time window into a single network sync.
    /// Reduces network spam during rapid stat changes (armor swaps, buff applications).
    /// </summary>
    public class StatCoalescingEngine : ModSystem
    {
        private ICoreServerAPI sapi;

        // Pending stat updates per player: entityId -> (statName -> value)
        private class CoalescedUpdate
        {
            public Dictionary<string, float> Stats = new();
            public long FirstUpdateTime;
            public long CallbackId;
            public bool IsFlushing;
        }

        private static readonly Dictionary<long, CoalescedUpdate> PendingUpdates = new();

        // Time window for coalescing (ms) - from config
        private static int CoalesceWindowMs => PerformanceConfig.StatCoalesceWindowMs;
        // Maximum delay before forced flush (ms) - from config
        private static int MaxDelayMs => PerformanceConfig.StatMaxDelayMs;

        /// <summary>
        /// Checks if coalescing is enabled in config.
        /// </summary>
        public static bool IsEnabled => PerformanceConfig.EnablePerformanceOptimizations
            && PerformanceConfig.EnableStatCoalescing;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            if (forSide != EnumAppSide.Server) return false;
            return PerformanceConfig.EnablePerformanceOptimizations
                && PerformanceConfig.EnableStatCoalescing;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;

            if (!PerformanceConfig.EnableStatCoalescing)
            {
                api.Logger.Notification("[vsquest] StatCoalescingEngine is disabled in config");
                return;
            }

            api.Event.PlayerDisconnect += OnPlayerDisconnect;
            api.Logger.Notification("[vsquest] StatCoalescingEngine started");
        }

        /// <summary>
        /// Queues a stat update for coalescing. May not apply immediately.
        /// </summary>
        public static void QueueStatUpdate(
            ICoreServerAPI api,
            EntityPlayer player,
            string stat,
            float value,
            string code = "vsquestmod")
        {
            // If disabled, apply immediately without coalescing
            if (!PerformanceConfig.EnablePerformanceOptimizations || !PerformanceConfig.EnableStatCoalescing)
            {
                player?.Stats.Set(stat, code, value, true);
                return;
            }

            if (player?.EntityId == null) return;

            long entityId = player.EntityId;

            // Get or create coalescing entry
            if (!PendingUpdates.TryGetValue(entityId, out var update))
            {
                update = new CoalescedUpdate();
                PendingUpdates[entityId] = update;
            }

            // First stat in this window? Schedule flush
            if (update.Stats.Count == 0 && !update.IsFlushing)
            {
                update.FirstUpdateTime = api.World.ElapsedMilliseconds;
                update.CallbackId = api.Event.RegisterCallback(
                    (dt) => FlushUpdates(api, entityId),
                    CoalesceWindowMs
                );
            }

            // Add/update stat value with code as prefix to stat name for tracking
            string statKey = code == "vsquestmod" ? stat : $"{code}:{stat}";
            update.Stats[statKey] = value;

            // Check if we've exceeded max delay
            long elapsed = api.World.ElapsedMilliseconds - update.FirstUpdateTime;
            if (elapsed > MaxDelayMs && !update.IsFlushing)
            {
                // Force immediate flush
                api.Event.UnregisterCallback(update.CallbackId);
                FlushUpdates(api, entityId);
            }
        }

        /// <summary>
        /// Queues multiple stat updates at once.
        /// </summary>
        public static void QueueStatUpdates(
            ICoreServerAPI api,
            EntityPlayer player,
            Dictionary<string, float> stats)
        {
            if (player?.EntityId == null) return;

            foreach (var stat in stats)
            {
                QueueStatUpdate(api, player, stat.Key, stat.Value);
            }
        }

        /// <summary>
        /// Forces immediate flush of all pending stats for a player.
        /// Call this when immediate sync is required (e.g., combat).
        /// </summary>
        public static void ForceFlush(ICoreServerAPI api, long entityId)
        {
            if (!PerformanceConfig.EnableStatCoalescing) return;

            if (!PendingUpdates.TryGetValue(entityId, out var update)) return;
            if (update.IsFlushing) return;

            // Cancel scheduled flush and do it now
            api.Event.UnregisterCallback(update.CallbackId);
            FlushUpdates(api, entityId);
        }

        /// <summary>
        /// Flushes all pending stat updates for a player.
        /// </summary>
        private static void FlushUpdates(ICoreServerAPI api, long entityId)
        {
            if (!PendingUpdates.TryGetValue(entityId, out var update)) return;
            if (update.IsFlushing) return;

            update.IsFlushing = true;

            var entity = api.World.GetEntityById(entityId) as EntityPlayer;
            if (entity?.EntityId == null)
            {
                // Entity no longer exists, clean up
                PendingUpdates.Remove(entityId);
                return;
            }

            // Apply all coalesced stats
            foreach (var stat in update.Stats)
            {
                // Parse code from stat key (format: "code:stat" or just "stat")
                string code = "vsquestmod";
                string statName = stat.Key;

                int colonIndex = stat.Key.IndexOf(':');
                if (colonIndex > 0)
                {
                    code = stat.Key.Substring(0, colonIndex);
                    statName = stat.Key.Substring(colonIndex + 1);
                }

                entity.Stats.Set(statName, code, stat.Value, false); // Don't sync individually
            }

            // Single MarkPathDirty for all stats
            if (update.Stats.Count > 0)
            {
                entity.WatchedAttributes.MarkPathDirty("vsquestmod");
            }

            // Clean up
            PendingUpdates.Remove(entityId);
        }

        /// <summary>
        /// Immediately applies a stat without coalescing.
        /// Use for critical combat stats that need instant sync.
        /// </summary>
        public static void ApplyStatImmediate(
            EntityPlayer player,
            string stat,
            float value)
        {
            if (player?.EntityId == null) return;

            // Apply directly, bypass coalescing
            player.Stats.Set(stat, "vsquestmod", value, true);
        }

        /// <summary>
        /// Checks if a player has pending stat updates.
        /// </summary>
        public static bool HasPendingUpdates(long entityId)
        {
            return PendingUpdates.TryGetValue(entityId, out var update) && update.Stats.Count > 0;
        }

        /// <summary>
        /// Gets the number of pending stat updates across all players.
        /// </summary>
        public static int GetPendingUpdateCount()
        {
            return PendingUpdates.Values.Sum(u => u.Stats.Count);
        }

        private void OnPlayerDisconnect(IServerPlayer player)
        {
            long entityId = player.Entity.EntityId;

            // Clean up pending updates
            if (PendingUpdates.TryGetValue(entityId, out var update))
            {
                sapi.Event.UnregisterCallback(update.CallbackId);
                PendingUpdates.Remove(entityId);
            }
        }

        /// <summary>
        /// Clears all pending updates. Call on shutdown or major state change.
        /// </summary>
        public static void ClearAllPending(ICoreServerAPI api)
        {
            foreach (var kvp in PendingUpdates)
            {
                api.Event.UnregisterCallback(kvp.Value.CallbackId);
            }
            PendingUpdates.Clear();
        }
    }
}
