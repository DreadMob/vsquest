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
    public class EntityBehaviorBossLifeDrainNova : EntityBehavior
    {
        private class Stage
        {
            public float whenHealthRelBelow;
            public int cooldownMs;
            public float range;
            public int durationMs;
            public int tickIntervalMs;
            public float drainPerSecond;
            public float healMult;
        }

        private ICoreServerAPI sapi;
        private List<Stage> stages = new List<Stage>();
        private EntityBehaviorHealth healthBehavior;

        private long lastCastMs;
        private int currentStageIndex;
        private long novaEndMs;
        private long lastTickMs;
        private bool isNovaActive;

        public EntityBehaviorBossLifeDrainNova(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bosslifedrainnova";

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            sapi = entity?.Api as ICoreServerAPI;
            healthBehavior = entity.GetBehavior<EntityBehaviorHealth>();

            stages.Clear();
            foreach (var stageObj in attributes["stages"].AsArray())
            {
                if (stageObj == null || !stageObj.Exists) continue;

                stages.Add(new Stage
                {
                    whenHealthRelBelow = stageObj["whenHealthRelBelow"].AsFloat(1f),
                    cooldownMs = stageObj["cooldownSeconds"].AsInt(18) * 1000,
                    range = stageObj["range"].AsFloat(22f),
                    durationMs = stageObj["durationMs"].AsInt(8000),
                    tickIntervalMs = stageObj["tickIntervalMs"].AsInt(1000),
                    drainPerSecond = stageObj["drainPerSecond"].AsFloat(stageObj["drainPerTick"].AsFloat(1.5f)),
                    healMult = stageObj["healMult"].AsFloat(stageObj["healMultiplier"].AsFloat(0.5f))
                });
            }

            lastCastMs = 0;
            currentStageIndex = 0;
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            if (sapi == null || !entity.Alive || healthBehavior == null) return;

            long now = sapi.World.ElapsedMilliseconds;

            // Determine current stage based on HP
            float hpPercent = healthBehavior.Health / healthBehavior.MaxHealth;
            for (int i = stages.Count - 1; i >= 0; i--)
            {
                if (hpPercent <= stages[i].whenHealthRelBelow)
                {
                    currentStageIndex = i;
                    break;
                }
            }

            var stage = stages[currentStageIndex];

            // Check cooldown
            if (now - lastCastMs < stage.cooldownMs) return;

            // Start nova
            PerformNova(stage, now);
            lastCastMs = now;
            novaEndMs = now + stage.durationMs;
            lastTickMs = now;
            isNovaActive = true;

            // Process active nova ticks
            ProcessActiveNova(stage, now);
        }

        private void ProcessActiveNova(Stage stage, long now)
        {
            if (!isNovaActive || now > novaEndMs)
            {
                isNovaActive = false;
                return;
            }

            // Process damage tick
            if (now - lastTickMs >= stage.tickIntervalMs)
            {
                lastTickMs = now;
                LaunchNovaTick(stage);
            }
        }

        private void PerformNova(Stage stage, long now)
        {
            // Visual nova ring at start
            SpawnNovaRing(stage);
            // First tick immediately
            LaunchNovaTick(stage);
        }

        private void LaunchNovaTick(Stage stage)
        {
            float totalDamage = 0;
            var damagedPlayers = new List<EntityPlayer>();

            // Find and damage players in range
            foreach (var player in sapi.World.AllOnlinePlayers)
            {
                if (player.Entity?.ServerPos == null) continue;
                if (player.Entity.ServerPos.Dimension != entity.ServerPos.Dimension) continue;

                double dist = player.Entity.ServerPos.DistanceTo(entity.ServerPos);
                if (dist > stage.range) continue;

                float damage = stage.drainPerSecond * (stage.tickIntervalMs / 1000f);
                player.Entity.ReceiveDamage(
                    new DamageSource
                    {
                        Source = EnumDamageSource.Entity,
                        SourceEntity = entity,
                        Type = EnumDamageType.PiercingAttack
                    },
                    damage
                );

                totalDamage += damage;
                damagedPlayers.Add(player.Entity);
            }

            // Heal boss
            if (totalDamage > 0 && healthBehavior != null)
            {
                float healAmount = totalDamage * stage.healMult;
                healthBehavior.Health = Math.Min(healthBehavior.MaxHealth, healthBehavior.Health + healAmount);

                // Visual heal effect - particles flying to boss
                foreach (var player in damagedPlayers)
                {
                    Vec3d startPos = player.ServerPos.XYZ.Add(0, 1, 0);
                    Vec3d endPos = entity.ServerPos.XYZ.Add(0, 1, 0);

                    sapi.World.SpawnParticles(
                        new SimpleParticleProperties(
                            5, 8,
                            ColorUtil.ToRgba(200, 180, 180, 190),
                            startPos,
                            startPos,
                            new Vec3f(
                                (float)(endPos.X - startPos.X) * 2,
                                (float)(endPos.Y - startPos.Y) * 2,
                                (float)(endPos.Z - startPos.Z) * 2
                            ),
                            new Vec3f(
                                (float)(endPos.X - startPos.X) * 2,
                                (float)(endPos.Y - startPos.Y) * 2,
                                (float)(endPos.Z - startPos.Z) * 2
                            ),
                            0.5f,
                            0,
                            0.5f,
                            0.5f,
                            EnumParticleModel.Quad
                        )
                    );
                }
            }

            // Visual nova ring
            SpawnNovaRing(stage);

            // Sound
            sapi.World.PlaySoundAt(
                new AssetLocation("albase:sounds/mechanical/mecha switch power"),
                entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z,
                null, true, 32, 0.133f
            );
        }

        private void SpawnNovaRing(Stage stage)
        {
            for (int i = 0; i < 36; i++)
            {
                double angle = i * 10 * GameMath.DEG2RAD;
                float x = (float)Math.Cos(angle) * stage.range;
                float z = (float)Math.Sin(angle) * stage.range;

                sapi.World.SpawnParticles(
                    new SimpleParticleProperties(
                        2, 3,
                        ColorUtil.ToRgba(200, 160, 165, 175),
                        entity.ServerPos.XYZ.Add(x, 0.5, z),
                        entity.ServerPos.XYZ.Add(x, 1.5, z),
                        new Vec3f(-0.1f, 0.05f, -0.1f),
                        new Vec3f(0.1f, 0.15f, 0.1f),
                        0.4f,
                        0.1f,
                        0.5f,
                        0.5f,
                        EnumParticleModel.Quad
                    )
                );
            }
        }
    }
}
