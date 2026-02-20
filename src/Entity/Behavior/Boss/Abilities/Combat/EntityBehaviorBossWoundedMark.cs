using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsQuest
{
    public class EntityBehaviorBossWoundedMark : EntityBehavior
    {
        private const string WoundedMarkKey = "alegacyvsquest:bosswoundedmark:marked";
        private const string LastExplosionKey = "alegacyvsquest:bosswoundedmark:lastExplosion";

        private ICoreServerAPI sapi;
        private float triggerHpPercent;
        private float explosionDamage;
        private float explosionRange;
        private int cooldownMs;
        private int maxTargets;
        private float detectionRange;

        private long lastCastMs;
        private Dictionary<long, bool> woundedPlayers = new Dictionary<long, bool>();
        private HashSet<long> playersWhoHealed = new HashSet<long>();

        public EntityBehaviorBossWoundedMark(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bosswoundedmark";

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            sapi = entity?.Api as ICoreServerAPI;
            triggerHpPercent = attributes["triggerHp"].AsFloat(0.45f);
            explosionDamage = attributes["explosionDamage"].AsFloat(35f);
            explosionRange = attributes["range"].AsFloat(10f);
            cooldownMs = attributes["cooldownSeconds"].AsInt(18) * 1000;
            maxTargets = attributes["maxTargets"].AsInt(2);
            detectionRange = attributes["detectionRange"].AsFloat(40f);

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

            // Check for healing explosions
            CheckForHealingExplosions();
        }

        private void TryApplyMark(long now)
        {
            List<EntityPlayer> targets = new List<EntityPlayer>();

            foreach (var player in sapi.World.AllOnlinePlayers)
            {
                if (player.Entity?.ServerPos == null) continue;
                if (player.Entity.ServerPos.Dimension != entity.ServerPos.Dimension) continue;
                if (player.Entity.ServerPos.DistanceTo(entity.ServerPos) > detectionRange) continue;

                // Check HP threshold
                var health = player.Entity.GetBehavior<EntityBehaviorHealth>();
                if (health == null) continue;

                float hpPercent = health.Health / health.MaxHealth;
                if (hpPercent > triggerHpPercent) continue;

                // Check if already marked
                if (woundedPlayers.ContainsKey(player.Entity.EntityId)) continue;

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

                woundedPlayers[target.EntityId] = false; // Not exploded yet

                // Visual effect - pulsing red aura
                var targetPos = target.ServerPos.XYZ;
                sapi.World.SpawnParticles(
                    new SimpleParticleProperties(
                        30, 40,
                        ColorUtil.ToRgba(200, 255, 50, 50),
                        targetPos.Add(-0.3, 0, -0.3),
                        targetPos.Add(0.3, 2, 0.3),
                        new Vec3f(-0.2f, -0.2f, -0.2f),
                        new Vec3f(0.2f, 0.2f, 0.2f),
                        1.5f,
                        -0.2f,
                        0.5f,
                        0.5f,
                        EnumParticleModel.Quad
                    )
                );

                entity.Api.Logger.Debug($"[BossWoundedMark] Applied mark to player {target.EntityId}");
            }

            lastCastMs = now;
        }

        private void CheckForHealingExplosions()
        {
            List<long> toRemove = new List<long>();

            foreach (var kvp in woundedPlayers)
            {
                if (kvp.Value) // Already exploded
                {
                    toRemove.Add(kvp.Key);
                    continue;
                }

                var player = sapi.World.PlayerByUid(kvp.Key.ToString())?.Entity;
                if (player == null)
                {
                    toRemove.Add(kvp.Key);
                    continue;
                }

                // Check if player healed (health increased)
                // This is a simplified check - in practice you'd track health changes
                // For now, we check if player used healing items recently via stats
                float healRate = player.Stats.GetBlended("healrate");
                if (healRate > 0 && player.WatchedAttributes.GetLong("lastHealMs", 0) > sapi.World.ElapsedMilliseconds - 1000)
                {
                    // Explosion!
                    ExplodeOnPlayer(player);
                    woundedPlayers[kvp.Key] = true;
                }
            }

            foreach (var id in toRemove)
            {
                woundedPlayers.Remove(id);
            }
        }

        private void ExplodeOnPlayer(EntityPlayer player)
        {
            var pos = player.ServerPos.XYZ;

            // Damage in AoE
            var nearbyEntities = sapi.World.GetEntitiesAround(pos, explosionRange, explosionRange, (e) => e is EntityPlayer);
            foreach (var e in nearbyEntities)
            {
                if (e is EntityPlayer p)
                {
                    p.ReceiveDamage(
                        new DamageSource
                        {
                            Source = EnumDamageSource.Entity,
                            SourceEntity = entity,
                            Type = EnumDamageType.PiercingAttack
                        },
                        explosionDamage
                    );
                }
            }

            // Visual explosion effect
            sapi.World.SpawnParticles(
                new SimpleParticleProperties(
                    50, 80,
                    ColorUtil.ToRgba(255, 255, 100, 50),
                    pos.Add(-explosionRange, 0, -explosionRange),
                    pos.Add(explosionRange, explosionRange, explosionRange),
                    new Vec3f(-0.5f, 0.3f, -0.5f),
                    new Vec3f(0.5f, 0.8f, 0.5f),
                    1.0f,
                    0.5f,
                    0.5f,
                    0.5f,
                    EnumParticleModel.Quad
                )
            );

            // Sound effect
            sapi.World.PlaySoundAt(
                new AssetLocation("effect/smallexplosion"),
                pos.X, pos.Y, pos.Z,
                null, false, 32, 0.5f
            );
        }
    }
}
