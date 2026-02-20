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
    public class EntityBehaviorBossRequiemChains : EntityBehavior
    {
        private class Stage
        {
            public float whenHealthRelBelow;
            public int cooldownMs;
            public float range;
            public int maxTargets;
            public int durationMs;
            public float pullSpeed;
            public float damagePerSecond;
        }

        private ICoreServerAPI sapi;
        private List<Stage> stages = new List<Stage>();
        private EntityBehaviorHealth healthBehavior;

        private long lastCastMs;
        private int currentStageIndex;
        private Dictionary<long, long> chainedPlayers = new Dictionary<long, long>();

        public EntityBehaviorBossRequiemChains(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bossrequiemchains";

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
                    cooldownMs = stageObj["cooldownSeconds"].AsInt(10) * 1000,
                    range = stageObj["range"].AsFloat(8f),
                    maxTargets = stageObj["maxTargets"].AsInt(2),
                    durationMs = stageObj["duration"].AsInt(5) * 1000,
                    pullSpeed = stageObj["pullSpeed"].AsFloat(0.08f),
                    damagePerSecond = stageObj["damagePerSecond"].AsFloat(5f)
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
            if (now - lastCastMs >= stage.cooldownMs)
            {
                TryApplyChains(stage, now);
            }

            // Process chained players
            ProcessChainedPlayers(dt, now);
        }

        private void TryApplyChains(Stage stage, long now)
        {
            List<EntityPlayer> targets = new List<EntityPlayer>();

            foreach (var player in sapi.World.AllOnlinePlayers)
            {
                if (player.Entity?.ServerPos == null) continue;
                if (player.Entity.ServerPos.Dimension != entity.ServerPos.Dimension) continue;
                if (player.Entity.ServerPos.DistanceTo(entity.ServerPos) > stage.range) continue;
                if (chainedPlayers.ContainsKey(player.Entity.EntityId)) continue;

                targets.Add(player.Entity);
            }

            if (targets.Count == 0) return;

            // Select random targets up to maxTargets
            Random rand = new Random((int)now);
            int toChain = Math.Min(stage.maxTargets, targets.Count);

            for (int i = 0; i < toChain; i++)
            {
                int idx = rand.Next(targets.Count);
                var target = targets[idx];
                targets.RemoveAt(idx);

                long until = now + stage.durationMs;
                chainedPlayers[target.EntityId] = until;

                // Visual - chains on player
                var targetPos = target.ServerPos.XYZ;
                sapi.World.SpawnParticles(
                    new SimpleParticleProperties(
                        25, 35,
                        ColorUtil.ToRgba(255, 100, 100, 150),
                        targetPos.Add(-0.5, 0, -0.5),
                        targetPos.Add(0.5, 2, 0.5),
                        new Vec3f(-0.1f, -0.1f, -0.1f),
                        new Vec3f(0.1f, 0.1f, 0.1f),
                        1.0f,
                        0,
                        0.5f,
                        0.5f,
                        EnumParticleModel.Quad
                    )
                );
            }

            lastCastMs = now;
        }

        private void ProcessChainedPlayers(float dt, long now)
        {
            List<long> toRemove = new List<long>();

            foreach (var kvp in chainedPlayers)
            {
                if (now > kvp.Value)
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

                var stage = stages[currentStageIndex];

                // Pull towards boss
                Vec3d dir = entity.ServerPos.XYZ - player.ServerPos.XYZ;
                dir.Normalize();
                dir.Mul(stage.pullSpeed * dt);
                player.ServerPos.Motion.Add(dir);

                // Disable abilities
                player.WatchedAttributes.SetBool("alegacyvsquest:canshift", false);
                player.WatchedAttributes.SetBool("alegacyvsquest:canjump", false);
                player.WatchedAttributes.SetBool("alegacyvsquest:canuseitems", false);

                // Apply damage
                float damage = stage.damagePerSecond * dt;
                player.ReceiveDamage(
                    new DamageSource
                    {
                        Source = EnumDamageSource.Entity,
                        SourceEntity = entity,
                        Type = EnumDamageType.PiercingAttack
                    },
                    damage
                );

                // Visual chain line
                if (sapi.World.ElapsedMilliseconds % 200 < 50)
                {
                    Vec3d chainPos = player.ServerPos.XYZ.Add(0, 1, 0);
                    sapi.World.SpawnParticles(
                        new SimpleParticleProperties(
                            1, 2,
                            ColorUtil.ToRgba(200, 150, 100, 200),
                            chainPos,
                            chainPos,
                            new Vec3f(-0.05f, -0.05f, -0.05f),
                            new Vec3f(0.05f, 0.05f, 0.05f),
                            0.1f,
                            0,
                            0.5f,
                            0.5f,
                            EnumParticleModel.Quad
                        )
                    );
                }
            }

            // Cleanup expired chains
            foreach (var id in toRemove)
            {
                chainedPlayers.Remove(id);
                var player = sapi.World.PlayerByUid(id.ToString())?.Entity;
                if (player != null)
                {
                    player.WatchedAttributes.SetBool("alegacyvsquest:canshift", true);
                    player.WatchedAttributes.SetBool("alegacyvsquest:canjump", true);
                    player.WatchedAttributes.SetBool("alegacyvsquest:canuseitems", true);
                }
            }
        }
    }
}
