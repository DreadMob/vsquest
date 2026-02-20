using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class EntityBehaviorBossSurroundedResponse : EntityBehavior
    {
        private ICoreServerAPI sapi;
        private int surroundedThreshold;
        private float detectionRange;
        private int shockwaveCooldownMs;
        private int slamCooldownMs;
        private float knockbackRange;
        private float slamDamage;
        private int stunMs;

        private long lastShockwaveMs;
        private long lastSlamMs;

        public EntityBehaviorBossSurroundedResponse(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bosssurroundedresponse";

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            sapi = entity?.Api as ICoreServerAPI;
            surroundedThreshold = attributes["surroundedThreshold"].AsInt(3);
            detectionRange = attributes["detectionRange"].AsFloat(6f);
            shockwaveCooldownMs = attributes["shockwaveCooldownSeconds"].AsInt(10) * 1000;
            slamCooldownMs = attributes["slamCooldownSeconds"].AsInt(8) * 1000;
            knockbackRange = attributes["knockbackRange"].AsFloat(8f);
            slamDamage = attributes["slamDamage"].AsFloat(22f);
            stunMs = attributes["stunMs"].AsInt(1000);

            lastShockwaveMs = 0;
            lastSlamMs = 0;
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            if (sapi == null || !entity.Alive) return;

            long now = sapi.World.ElapsedMilliseconds;

            // Count nearby players
            int nearbyCount = CountNearbyPlayers();
            if (nearbyCount < surroundedThreshold) return;

            // Check which ability to use
            bool canShockwave = now - lastShockwaveMs >= shockwaveCooldownMs;
            bool canSlam = now - lastSlamMs >= slamCooldownMs;

            if (canShockwave)
            {
                PerformShockwave(now);
            }
            else if (canSlam)
            {
                PerformSlam(now);
            }
        }

        private int CountNearbyPlayers()
        {
            int count = 0;
            foreach (var player in sapi.World.AllOnlinePlayers)
            {
                if (player.Entity?.ServerPos == null) continue;
                if (player.Entity.ServerPos.Dimension != entity.ServerPos.Dimension) continue;
                if (player.Entity.ServerPos.DistanceTo(entity.ServerPos) <= detectionRange)
                {
                    count++;
                }
            }
            return count;
        }

        private void PerformShockwave(long now)
        {
            lastShockwaveMs = now;

            // Push all nearby players back
            foreach (var player in sapi.World.AllOnlinePlayers)
            {
                if (player.Entity?.ServerPos == null) continue;
                if (player.Entity.ServerPos.Dimension != entity.ServerPos.Dimension) continue;

                double dist = player.Entity.ServerPos.DistanceTo(entity.ServerPos);
                if (dist > knockbackRange) continue;

                // Calculate knockback
                Vec3d dir = player.Entity.ServerPos.XYZ - entity.ServerPos.XYZ;
                dir.Normalize();
                dir.Y = 0.5;
                dir.Mul(0.6);

                player.Entity.ServerPos.Motion.Add(dir);
            }

            // Visual effect
            var entityPos = entity.ServerPos.XYZ;
            sapi.World.SpawnParticles(
                new SimpleParticleProperties(
                    60, 80,
                    ColorUtil.ToRgba(200, 200, 200, 255),
                    entityPos.Add(-knockbackRange, 0, -knockbackRange),
                    entityPos.Add(knockbackRange, 2, knockbackRange),
                    new Vec3f(-0.3f, 0.1f, -0.3f),
                    new Vec3f(0.3f, 0.3f, 0.3f),
                    0.8f,
                    0.1f,
                    0.5f,
                    0.5f,
                    EnumParticleModel.Quad
                )
            );

            // Sound
            sapi.World.PlaySoundAt(
                new AssetLocation("albase:shock-sound-effect-horror"),
                entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z,
                null, false, 32, 0.5f
            );
        }

        private void PerformSlam(long now)
        {
            lastSlamMs = now;

            // Damage and stun all nearby players
            foreach (var player in sapi.World.AllOnlinePlayers)
            {
                if (player.Entity?.ServerPos == null) continue;
                if (player.Entity.ServerPos.Dimension != entity.ServerPos.Dimension) continue;

                double dist = player.Entity.ServerPos.DistanceTo(entity.ServerPos);
                if (dist > detectionRange * 1.5) continue;

                // Damage
                player.Entity.ReceiveDamage(
                    new DamageSource
                    {
                        Source = EnumDamageSource.Entity,
                        SourceEntity = entity,
                        Type = EnumDamageType.BluntAttack
                    },
                    slamDamage
                );

                // Stun
                player.Entity.WatchedAttributes.SetLong(
                    "alegacyvsquest:stununtil",
                    sapi.World.ElapsedMilliseconds + stunMs
                );
            }

            // Visual effect
            var slamPos = entity.ServerPos.XYZ;
            sapi.World.SpawnParticles(
                new SimpleParticleProperties(
                    40, 60,
                    ColorUtil.ToRgba(255, 150, 100, 50),
                    slamPos.Add(-3, 0, -3),
                    slamPos.Add(3, 1, 3),
                    new Vec3f(-0.3f, 0.2f, -0.3f),
                    new Vec3f(0.3f, 0.5f, 0.3f),
                    0.6f,
                    0.3f,
                    0.5f,
                    0.5f,
                    EnumParticleModel.Quad
                )
            );
        }
    }
}
