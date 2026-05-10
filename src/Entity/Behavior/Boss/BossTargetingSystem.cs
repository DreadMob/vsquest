using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// System for managing boss target finding and validation.
    /// Provides centralized target selection logic.
    /// </summary>
    public class BossTargetingSystem
    {
        private readonly ICoreServerAPI sapi;
        private readonly Entity bossEntity;

        public BossTargetingSystem(ICoreServerAPI sapi, Entity bossEntity)
        {
            this.sapi = sapi;
            this.bossEntity = bossEntity;
        }

        /// <summary>
        /// Try to find a valid target within specified range.
        /// Uses squared distance to avoid sqrt in the hot path.
        /// </summary>
        public bool TryFindTarget(float maxRange, float minRange, out EntityPlayer target, out float distance)
        {
            target = null;
            distance = 0f;

            if (sapi == null || bossEntity == null) return false;

            var bossPos = bossEntity.Pos.XYZ;
            var entities = sapi.World.GetEntitiesAround(bossPos, maxRange, maxRange, e => e is EntityPlayer);

            if (entities == null || entities.Length == 0)
            {
                sapi.Logger.Debug("[BossTargeting] TryFindTarget: No entities found around {0} in range {1}", bossPos, maxRange);
                return false;
            }

            // Find nearest valid player using squared distance
            EntityPlayer nearestPlayer = null;
            float nearestDistSq = float.MaxValue;
            float minRangeSq = minRange * minRange;
            float maxRangeSq = maxRange * maxRange;

            int playersFound = 0;
            int playersFilteredMinRange = 0;
            int playersFilteredInvalid = 0;

            for (int i = 0; i < entities.Length; i++)
            {
                if (!(entities[i] is EntityPlayer player)) continue;
                playersFound++;

                if (!player.Alive) continue;
                if (player.Pos.Dimension != bossEntity.Pos.Dimension) continue;

                float dx = (float)(player.Pos.X - bossPos.X);
                float dy = (float)(player.Pos.Y - bossPos.Y);
                float dz = (float)(player.Pos.Z - bossPos.Z);
                float distSq = dx * dx + dy * dy + dz * dz;

                if (distSq > maxRangeSq) continue; // 3D distance check (GetEntitiesAround uses cylinder)
                if (distSq < minRangeSq) { playersFilteredMinRange++; continue; }
                if (distSq >= nearestDistSq) continue;

                // Additional validation
                if (!IsValidTarget(player)) { playersFilteredInvalid++; continue; }

                nearestPlayer = player;
                nearestDistSq = distSq;
            }

            if (nearestPlayer != null)
            {
                target = nearestPlayer;
                distance = (float)Math.Sqrt(nearestDistSq);
                sapi.Logger.Debug("[BossTargeting] TryFindTarget: Found valid target {0} at distance {1:F1}", target.Player?.PlayerName, distance);
                return true;
            }

            sapi.Logger.Debug("[BossTargeting] TryFindTarget: No valid target. Players found: {0}, filtered by minRange: {1}, filtered by IsValidTarget: {2}",
                playersFound, playersFilteredMinRange, playersFilteredInvalid);
            return false;
        }

        /// <summary>
        /// Find all valid targets within specified range.
        /// </summary>
        public List<EntityPlayer> FindAllTargets(float maxRange, float minRange = 0f)
        {
            var targets = new List<EntityPlayer>();

            if (sapi == null || bossEntity == null) return targets;

            var bossPos = bossEntity.Pos.XYZ;
            var entities = sapi.World.GetEntitiesAround(bossPos, maxRange, maxRange, e => e is EntityPlayer);

            if (entities == null) return targets;

            float minRangeSq = minRange * minRange;
            float maxRangeSq = maxRange * maxRange;

            for (int i = 0; i < entities.Length; i++)
            {
                if (!(entities[i] is EntityPlayer player)) continue;
                if (!player.Alive) continue;
                if (player.Pos.Dimension != bossEntity.Pos.Dimension) continue;

                float dx = (float)(player.Pos.X - bossPos.X);
                float dy = (float)(player.Pos.Y - bossPos.Y);
                float dz = (float)(player.Pos.Z - bossPos.Z);
                float distSq = dx * dx + dy * dy + dz * dz;

                if (distSq < minRangeSq) continue;
                if (distSq > maxRangeSq) continue;

                if (!IsValidTarget(player)) continue;

                targets.Add(player);
            }

            return targets;
        }

        /// <summary>
        /// Find random target within specified range.
        /// </summary>
        public EntityPlayer FindRandomTarget(float maxRange, float minRange = 0f)
        {
            var targets = FindAllTargets(maxRange, minRange);
            if (targets.Count == 0) return null;

            int randomIndex = sapi.World.Rand.Next(targets.Count);
            return targets[randomIndex];
        }

        /// <summary>
        /// Find target with lowest health within range.
        /// </summary>
        public EntityPlayer FindLowestHealthTarget(float maxRange, float minRange = 0f)
        {
            var targets = FindAllTargets(maxRange, minRange);
            if (targets.Count == 0) return null;

            EntityPlayer lowestHealthTarget = null;
            float lowestHealthPercent = float.MaxValue;

            foreach (var player in targets)
            {
                var health = player.GetBehavior<Vintagestory.GameContent.EntityBehaviorHealth>();
                if (health == null) continue;

                float healthPercent = (float)health.Health / health.MaxHealth;
                if (healthPercent < lowestHealthPercent)
                {
                    lowestHealthPercent = healthPercent;
                    lowestHealthTarget = player;
                }
            }

            return lowestHealthTarget;
        }

        /// <summary>
        /// Find target with highest health within range.
        /// </summary>
        public EntityPlayer FindHighestHealthTarget(float maxRange, float minRange = 0f)
        {
            var targets = FindAllTargets(maxRange, minRange);
            if (targets.Count == 0) return null;

            EntityPlayer highestHealthTarget = null;
            float highestHealthPercent = 0f;

            foreach (var player in targets)
            {
                var health = player.GetBehavior<Vintagestory.GameContent.EntityBehaviorHealth>();
                if (health == null) continue;

                float healthPercent = (float)health.Health / health.MaxHealth;
                if (healthPercent > highestHealthPercent)
                {
                    highestHealthPercent = healthPercent;
                    highestHealthTarget = player;
                }
            }

            return highestHealthTarget;
        }

        /// <summary>
        /// Check if player is a valid target.
        /// Override for custom validation logic.
        /// </summary>
        public virtual bool IsValidTarget(EntityPlayer player)
        {
            if (player == null || !player.Alive)
            {
                sapi?.Logger?.Debug("[BossTargeting] IsValidTarget: player null or dead");
                return false;
            }

            // Check if player is in creative mode
            if (player.Player?.WorldData?.CurrentGameMode == EnumGameMode.Creative)
            {
                sapi?.Logger?.Debug("[BossTargeting] IsValidTarget: player {0} in creative mode", player.Player?.PlayerName);
                return false;
            }

            // Check if player is too far above or below (25 blocks for boss arenas with elevation)
            double yDiff = Math.Abs(player.Pos.Y - bossEntity.Pos.Y);
            if (yDiff > 25f)
            {
                sapi?.Logger?.Debug("[BossTargeting] IsValidTarget: player {0} Y diff too large: {1}", player.Player?.PlayerName, yDiff);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Get angle to target in degrees.
        /// </summary>
        public float GetAngleToTarget(EntityPlayer target)
        {
            if (target == null || bossEntity == null) return 0f;

            var bossPos = bossEntity.Pos.XYZ;
            var targetPos = target.Pos.XYZ;

            var direction = (targetPos - bossPos).Normalize();
            return (float)Math.Atan2(direction.X, direction.Z) * (180f / (float)Math.PI);
        }

        /// <summary>
        /// Check if target is in line of sight.
        /// </summary>
        public bool IsTargetInLineOfSight(EntityPlayer target, float maxDistance = 30f)
        {
            if (target == null || bossEntity == null) return false;

            try
            {
                var bossPos = bossEntity.Pos.XYZ.Add(0, bossEntity.LocalEyePos.Y, 0);
                var targetPos = target.Pos.XYZ.Add(0, target.LocalEyePos.Y, 0);

                // Simple raycast check
                var direction = (targetPos - bossPos).Normalize();
                float distance = (float)bossPos.DistanceTo(targetPos);

                if (distance > maxDistance) return false;

                // Check for blocks in between (simplified)
                var blockAccessor = sapi.World.BlockAccessor;
                int steps = Math.Min(50, (int)(distance * 2));

                for (int i = 1; i < steps; i++)
                {
                    float t = (float)i / steps;
                    var checkPos = bossPos + direction * distance * t;
                    var blockPos = new BlockPos((int)checkPos.X, (int)checkPos.Y, (int)checkPos.Z);
                    
                    var block = blockAccessor.GetBlock(blockPos);
                    if (block != null && block.BlockId != 0 && block.SideSolid[BlockFacing.UP.Index])
                    {
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                sapi.Logger.Error($"[vsquest] Exception in IsTargetInLineOfSight: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Sort targets by distance from boss.
        /// </summary>
        public List<EntityPlayer> SortTargetsByDistance(List<EntityPlayer> targets)
        {
            if (targets == null || targets.Count == 0) return targets;

            var bossPos = bossEntity.Pos.XYZ;
            return targets.OrderBy(p => (float)p.Pos.DistanceTo(bossPos)).ToList();
        }
    }
}
