using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class EntityBehaviorBossStillnessMark : EntityBehavior
    {
        private const string MarkUntilKey = "alegacyvsquest:bossstillnessmark:until";
        private const string MarkDamageKey = "alegacyvsquest:bossstillnessmark:damage";

        private const int DamageTickIntervalMs = 3000;
        private const int ProcessTickIntervalMs = 500; // Only process every 500ms instead of every tick

        private long lastProcessTickMs;

        private ICoreServerAPI sapi;
        private int durationMs;
        private float damagePerSecond;
        private int cooldownMs;
        private int maxTargets;
        private float detectionRange;
        private DamageSource damageSource;

        private long lastCastMs;
        private Dictionary<long, long> markedPlayers = new Dictionary<long, long>();
        private Dictionary<long, long> lastDamageTickMsByPlayer = new Dictionary<long, long>();
        private Dictionary<long, EntityPlayer> cachedPlayers = new Dictionary<long, EntityPlayer>(); // Cache player references

        public EntityBehaviorBossStillnessMark(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bossstillnessmark";

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            sapi = entity?.Api as ICoreServerAPI;
            durationMs = attributes["duration"].AsInt(8) * 1000;
            damagePerSecond = attributes["damagePerSecond"].AsFloat(15f);
            cooldownMs = attributes["cooldownSeconds"].AsInt(14) * 1000;
            maxTargets = attributes["maxTargets"].AsInt(2);
            detectionRange = attributes["range"].AsFloat(30f);

            damageSource = new DamageSource
            {
                Source = EnumDamageSource.Entity,
                SourceEntity = entity,
                Type = EnumDamageType.PiercingAttack
            };

            lastCastMs = 0;
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            if (sapi == null || !entity.Alive) return;

            long now = sapi.World.ElapsedMilliseconds;

            // Try to apply mark
            if (now - lastCastMs >= cooldownMs)
            {
                TryApplyMark(now);
            }

            // Process marked players - only every 500ms instead of every tick
            if (now - lastProcessTickMs >= ProcessTickIntervalMs)
            {
                lastProcessTickMs = now;
                ProcessMarkedPlayers(dt, now);
            }
        }

        private void TryApplyMark(long now)
        {
            List<EntityPlayer> targets = new List<EntityPlayer>();

            foreach (var player in sapi.World.AllOnlinePlayers)
            {
                if (player.Entity?.ServerPos == null) continue;
                if (player.Entity.ServerPos.Dimension != entity.ServerPos.Dimension) continue;
                if (player.Entity.ServerPos.DistanceTo(entity.ServerPos) > detectionRange) continue;

                // Check if already marked
                if (markedPlayers.ContainsKey(player.Entity.EntityId)) continue;

                targets.Add(player.Entity);
            }

            if (targets.Count == 0) return;

            // Select random targets up to maxTargets
            Random rand = new Random((int)now);
            int toMark = Math.Min(maxTargets, targets.Count);

            for (int i = 0; i < toMark; i++)
            {
                int idx = rand.Next(targets.Count);
                var target = targets[idx];
                targets.RemoveAt(idx);

                long until = now + durationMs;
                markedPlayers[target.EntityId] = until;

                // Visual effect - red chains
                var targetPos = target.ServerPos.XYZ;
                sapi.World.SpawnParticles(
                    new SimpleParticleProperties(
                        20, 30,
                        ColorUtil.ToRgba(255, 200, 50, 50),
                        targetPos.Add(-0.5, 0, -0.5),
                        targetPos.Add(0.5, 2, 0.5),
                        new Vec3f(-0.1f, -0.1f, -0.1f),
                        new Vec3f(0.1f, 0.1f, 0.1f),
                        1.0f,
                        0.1f,
                        0.5f,
                        0.5f,
                        EnumParticleModel.Quad
                    )
                );
            }

            lastCastMs = now;
        }

        private void ProcessMarkedPlayers(float dt, long now)
        {
            List<long> toRemove = new List<long>();

            foreach (var kvp in markedPlayers)
            {
                if (now > kvp.Value)
                {
                    toRemove.Add(kvp.Key);
                    continue;
                }

                // Find player by EntityId - use cached lookup
                EntityPlayer player = null;
                if (!cachedPlayers.TryGetValue(kvp.Key, out player) || player == null || !player.Alive)
                {
                    // Cache miss or invalid - find player
                    foreach (var onlinePlayer in sapi.World.AllOnlinePlayers)
                    {
                        if (onlinePlayer.Entity?.EntityId == kvp.Key)
                        {
                            player = onlinePlayer.Entity;
                            cachedPlayers[kvp.Key] = player;
                            break;
                        }
                    }
                }
                
                if (player == null || !player.Alive || player.ServerPos?.Dimension != entity.ServerPos.Dimension)
                {
                    toRemove.Add(kvp.Key);
                    cachedPlayers.Remove(kvp.Key);
                    continue;
                }

                // Check if moving - apply damage if they try to move
                float walkSpeed = player.Stats.GetBlended("walkspeed");
                if (walkSpeed > 0.05f)
                {
                    // Apply damage at fixed intervals (avoid per-tick spam)
                    if (!lastDamageTickMsByPlayer.TryGetValue(kvp.Key, out long lastDmgMs))
                    {
                        lastDmgMs = 0;
                    }

                    if (lastDmgMs == 0 || now - lastDmgMs >= DamageTickIntervalMs)
                    {
                        float damage = damagePerSecond * (DamageTickIntervalMs / 1000f);
                        player.ReceiveDamage(damageSource, damage);
                        lastDamageTickMsByPlayer[kvp.Key] = now;
                    }

                    // Visual feedback
                    if (sapi.World.ElapsedMilliseconds % DamageTickIntervalMs < 50)
                    {
                        var playerPos = player.ServerPos.XYZ;
                        sapi.World.SpawnParticles(
                            new SimpleParticleProperties(
                                2, 4,
                                ColorUtil.ToRgba(255, 255, 100, 100),
                                playerPos,
                                playerPos.Add(0.3, 1.5, 0.3),
                                new Vec3f(-0.1f, 0.1f, -0.1f),
                                new Vec3f(0.1f, 0.2f, 0.1f),
                                0.3f,
                                0.2f,
                                0.5f,
                                0.5f,
                                EnumParticleModel.Quad
                            )
                        );
                    }
                }
            }

            foreach (var id in toRemove)
            {
                markedPlayers.Remove(id);
                lastDamageTickMsByPlayer.Remove(id);
                cachedPlayers.Remove(id);
            }
        }
    }
}
