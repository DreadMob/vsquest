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
            public float damage;
            public float healMult;
            public int waves;
        }

        private ICoreServerAPI sapi;
        private List<Stage> stages = new List<Stage>();
        private EntityBehaviorHealth healthBehavior;

        private long lastCastMs;
        private int currentStageIndex;

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
                    damage = stageObj["damage"].AsFloat(12f),
                    healMult = stageObj["healMult"].AsFloat(2f),
                    waves = stageObj["waves"].AsInt(2)
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

            // Perform nova
            PerformNova(stage, now);
            lastCastMs = now;
        }

        private void PerformNova(Stage stage, long now)
        {
            // White glow effect on boss during nova
            entity.WatchedAttributes.SetFloat("entityBrightness", 1.0f);
            entity.WatchedAttributes.MarkPathDirty("entityBrightness");
            
            // Reset glow after nova duration
            sapi.Event.RegisterCallback((_) =>
            {
                entity.WatchedAttributes.SetFloat("entityBrightness", 0);
                entity.WatchedAttributes.MarkPathDirty("entityBrightness");
            }, stage.waves * 800 + 500);

            // Launch waves
            for (int wave = 0; wave < stage.waves; wave++)
            {
                sapi.Event.RegisterCallback((_) =>
                {
                    LaunchNovaWave(stage);
                }, wave * 800);
            }
        }

        private void LaunchNovaWave(Stage stage)
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

                player.Entity.ReceiveDamage(
                    new DamageSource
                    {
                        Source = EnumDamageSource.Entity,
                        SourceEntity = entity,
                        Type = EnumDamageType.PiercingAttack
                    },
                    stage.damage
                );

                totalDamage += stage.damage;
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
                            ColorUtil.ToRgba(200, 255, 50, 50),
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
            for (int i = 0; i < 36; i++)
            {
                double angle = i * 10 * GameMath.DEG2RAD;
                float x = (float)Math.Cos(angle) * stage.range;
                float z = (float)Math.Sin(angle) * stage.range;

                sapi.World.SpawnParticles(
                    new SimpleParticleProperties(
                        2, 3,
                        ColorUtil.ToRgba(200, 255, 100, 100),
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

            // Sound
            sapi.World.PlaySoundAt(
                new AssetLocation("albase:magic-sparkling"),
                entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z,
                null, false, 32, 0.4f
            );
        }
    }
}
