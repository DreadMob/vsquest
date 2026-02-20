using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class EntityBehaviorBossCorpseExplosion : EntityBehavior
    {
        private ICoreServerAPI sapi;
        private float explosionRadius;
        private float explosionDamage;
        private float poisonPerSecond;
        private int poisonDurationMs;
        private int cooldownMs;

        private long lastExplosionMs;

        public EntityBehaviorBossCorpseExplosion(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bosscorpseexplosion";

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            sapi = entity?.Api as ICoreServerAPI;

            explosionRadius = attributes["explosionRadius"].AsFloat(12f);
            explosionDamage = attributes["explosionDamage"].AsFloat(30f);
            poisonPerSecond = attributes["poisonPerSecond"].AsFloat(8f);
            poisonDurationMs = attributes["poisonDurationSeconds"].AsInt(10) * 1000;
            cooldownMs = attributes["cooldownBetweenExplosionsMs"].AsInt(2500);

            lastExplosionMs = 0;
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            base.OnEntityDespawn(despawn);

            if (sapi == null) return;
            if (despawn.Reason != EnumDespawnReason.Death) return;

            long now = sapi.World.ElapsedMilliseconds;
            if (now - lastExplosionMs < cooldownMs) return;

            lastExplosionMs = now;
            PerformExplosion();
        }

        private void PerformExplosion()
        {
            var pos = entity.ServerPos.XYZ;

            // Damage players in radius
            foreach (var player in sapi.World.AllOnlinePlayers)
            {
                if (player.Entity?.ServerPos == null) continue;
                if (player.Entity.ServerPos.Dimension != entity.ServerPos.Dimension) continue;

                double dist = player.Entity.ServerPos.DistanceTo(pos);
                if (dist > explosionRadius) continue;

                // Apply explosion damage
                player.Entity.ReceiveDamage(
                    new DamageSource
                    {
                        Source = EnumDamageSource.Entity,
                        SourceEntity = entity,
                        Type = EnumDamageType.PiercingAttack
                    },
                    explosionDamage
                );

                // Apply poison
                player.Entity.WatchedAttributes.SetFloat(
                    "intoxication",
                    player.Entity.WatchedAttributes.GetFloat("intoxication") + poisonPerSecond
                );
                player.Entity.WatchedAttributes.SetLong(
                    "alegacyvsquest:poisonuntil",
                    sapi.World.ElapsedMilliseconds + poisonDurationMs
                );
            }

            // Visual explosion
            sapi.World.SpawnParticles(
                new SimpleParticleProperties(
                    60, 80,
                    ColorUtil.ToRgba(255, 100, 50, 50),
                    pos.Add(-explosionRadius, 0, -explosionRadius),
                    pos.Add(explosionRadius, explosionRadius / 2, explosionRadius),
                    new Vec3f(-0.5f, 0.2f, -0.5f),
                    new Vec3f(0.5f, 0.8f, 0.5f),
                    1.0f,
                    0.3f,
                    0.5f,
                    0.5f,
                    EnumParticleModel.Quad
                )
            );

            // Green poison particles
            sapi.World.SpawnParticles(
                new SimpleParticleProperties(
                    30, 40,
                    ColorUtil.ToRgba(200, 50, 200, 50),
                    pos.Add(-explosionRadius, 0, -explosionRadius),
                    pos.Add(explosionRadius, 1, explosionRadius),
                    new Vec3f(-0.2f, 0.05f, -0.2f),
                    new Vec3f(0.2f, 0.15f, 0.2f),
                    2.0f,
                    0.1f,
                    0.5f,
                    0.5f,
                    EnumParticleModel.Quad
                )
            );

            // Sound
            sapi.World.PlaySoundAt(
                new AssetLocation("effect/smallexplosion"),
                pos.X, pos.Y, pos.Z,
                null, false, 32, 0.5f
            );
        }
    }
}
