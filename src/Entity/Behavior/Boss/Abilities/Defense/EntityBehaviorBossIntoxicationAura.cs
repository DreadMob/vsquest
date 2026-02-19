using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class EntityBehaviorBossIntoxicationAura : EntityBehavior
    {
        private const string LastTickMsKey = "alegacyvsquest:bossintoxaura:lastTickMs";
        private const string IntoxUntilMsKey = "alegacyvsquest:bossintoxaura:until";

        private class AuraStage
        {
            public float whenHealthRelBelow;
            public float range;
            public float intoxication;
            public int intervalMs;
        }

        private ICoreServerAPI sapi;
        private readonly List<AuraStage> stages = new List<AuraStage>();
        private float maxRange;

        private long lastCleanupMs;

        public EntityBehaviorBossIntoxicationAura(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bossintoxaura";

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            sapi = entity?.Api as ICoreServerAPI;

            stages.Clear();
            maxRange = 0f;
            try
            {
                foreach (var stageObj in attributes["stages"].AsArray())
                {
                    if (stageObj == null || !stageObj.Exists) continue;

                    var stage = new AuraStage
                    {
                        whenHealthRelBelow = stageObj["whenHealthRelBelow"].AsFloat(1f),
                        range = stageObj["range"].AsFloat(24f),
                        intoxication = stageObj["intoxication"].AsFloat(0f),
                        intervalMs = stageObj["intervalMs"].AsInt(500)
                    };

                    if (stage.range <= 0f) stage.range = 24f;
                    if (stage.intervalMs < 100) stage.intervalMs = 100;

                    stages.Add(stage);
                    if (stage.range > maxRange) maxRange = stage.range;
                }
            }
            catch (Exception ex)
            {
                entity?.Api?.Logger?.Error($"[vsquest] Exception in parsing stages: {ex}");
            }
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);
            if (sapi == null || entity == null) return;
            if (stages.Count == 0) return;
            if (!entity.Alive) return;

            CleanupExpiredIntoxication();

            if (!BossBehaviorUtils.TryGetHealthFraction(entity, out float frac)) return;

            int stageIndex = -1;
            for (int i = 0; i < stages.Count; i++)
            {
                if (frac <= stages[i].whenHealthRelBelow)
                {
                    stageIndex = i;
                }
            }

            if (stageIndex < 0) return;

            var stage = stages[stageIndex];
            long nowMs = sapi.World.ElapsedMilliseconds;
            long lastTickMs = entity.WatchedAttributes.GetLong(LastTickMsKey, 0);
            if (nowMs - lastTickMs < stage.intervalMs) return;

            entity.WatchedAttributes.SetLong(LastTickMsKey, nowMs);
            entity.WatchedAttributes.MarkPathDirty(LastTickMsKey);

            // Avoid values > 1.0 as they can break client rendering/controls.
            float targetIntox = GameMath.Clamp(stage.intoxication, 0f, 1.0f);
            if (targetIntox <= 0f) return;

            var players = sapi.World.AllOnlinePlayers;
            if (players == null || players.Length == 0) return;

            double range = stage.range > 0 ? stage.range : 24f;
            double rangeSq = range * range;
            var selfPos = entity.ServerPos;

            for (int i = 0; i < players.Length; i++)
            {
                var player = players[i] as IServerPlayer;
                var playerEntity = player?.Entity;
                if (playerEntity == null) continue;
                if (playerEntity.ServerPos.Dimension != selfPos.Dimension) continue;

                double dx = playerEntity.ServerPos.X - selfPos.X;
                double dy = playerEntity.ServerPos.Y - selfPos.Y;
                double dz = playerEntity.ServerPos.Z - selfPos.Z;
                double distSq = dx * dx + dy * dy + dz * dz;
                if (distSq > rangeSq) continue;

                float current = playerEntity.WatchedAttributes.GetFloat("intoxication", 0f);
                if (current >= targetIntox) continue;

                // Apply as a timed effect, not a permanent attribute.
                playerEntity.WatchedAttributes.SetLong(IntoxUntilMsKey, nowMs + Math.Max(1000, stage.intervalMs * 6));
                playerEntity.WatchedAttributes.MarkPathDirty(IntoxUntilMsKey);

                playerEntity.WatchedAttributes.SetFloat("intoxication", targetIntox);
                playerEntity.WatchedAttributes.MarkPathDirty("intoxication");
            }
        }

        private void CleanupExpiredIntoxication()
        {
            if (sapi == null) return;

            long nowMs = sapi.World.ElapsedMilliseconds;
            if (lastCleanupMs != 0 && nowMs - lastCleanupMs < 500) return;
            lastCleanupMs = nowMs;

            var players = sapi.World.AllOnlinePlayers;
            if (players == null || players.Length == 0) return;

            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] is not IServerPlayer sp) continue;
                if (sp.Entity is not EntityPlayer plr) continue;

                long until = 0;
                try
                {
                    until = plr.WatchedAttributes.GetLong(IntoxUntilMsKey, 0);
                }
                catch (Exception ex)
                {
                    entity?.Api?.Logger?.Error($"[vsquest] Exception in CleanupExpiredIntoxication GetLong: {ex}");
                    until = 0;
                }

                if (until <= 0)
                {
                    // Hard clamp in case something else pushed intoxication to invalid values.
                    try
                    {
                        float cur = plr.WatchedAttributes.GetFloat("intoxication", 0f);
                        if (cur > 1.0f)
                        {
                            plr.WatchedAttributes.SetFloat("intoxication", 1.0f);
                            plr.WatchedAttributes.MarkPathDirty("intoxication");
                        }

                        // Legacy fail-safe: if intoxication is stuck near max without our timer key,
                        // clear it so players can recover (black screen / broken controls).
                        // This should only trigger for extreme values.
                        if (cur >= 0.95f)
                        {
                            plr.WatchedAttributes.SetFloat("intoxication", 0f);
                            plr.WatchedAttributes.MarkPathDirty("intoxication");
                        }
                    }
                    catch (Exception ex)
                    {
                        entity?.Api?.Logger?.Error($"[vsquest] Exception in CleanupExpiredIntoxication GetFloat: {ex}");
                    }

                    continue;
                }

                // World.ElapsedMilliseconds resets on relog/server restart, but WatchedAttributes persist.
                // If 'until' is far in the future compared to 'now', it is almost certainly stale data.
                if (nowMs > 0)
                {
                    const long MaxFutureMs = 10L * 60L * 1000L;
                    if (until - nowMs > MaxFutureMs)
                    {
                        try
                        {
                            plr.WatchedAttributes.SetLong(IntoxUntilMsKey, 0);
                            plr.WatchedAttributes.MarkPathDirty(IntoxUntilMsKey);
                        }
                        catch (Exception ex)
                        {
                            entity?.Api?.Logger?.Error($"[vsquest] Exception in CleanupExpiredIntoxication SetLong stale: {ex}");
                        }

                        try
                        {
                            plr.WatchedAttributes.SetFloat("intoxication", 0f);
                            plr.WatchedAttributes.MarkPathDirty("intoxication");
                        }
                        catch (Exception ex)
                        {
                            entity?.Api?.Logger?.Error($"[vsquest] Exception in CleanupExpiredIntoxication SetFloat stale: {ex}");
                        }

                        continue;
                    }
                }

                if (nowMs >= until)
                {
                    try
                    {
                        plr.WatchedAttributes.SetLong(IntoxUntilMsKey, 0);
                        plr.WatchedAttributes.MarkPathDirty(IntoxUntilMsKey);
                    }
                    catch (Exception ex)
                    {
                        entity?.Api?.Logger?.Error($"[vsquest] Exception in CleanupExpiredIntoxication SetLong expired: {ex}");
                    }

                    try
                    {
                        plr.WatchedAttributes.SetFloat("intoxication", 0f);
                        plr.WatchedAttributes.MarkPathDirty("intoxication");
                    }
                    catch (Exception ex)
                    {
                        entity?.Api?.Logger?.Error($"[vsquest] Exception in CleanupExpiredIntoxication SetFloat expired: {ex}");
                    }
                }
            }
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            base.OnEntityDeath(damageSourceForDeath);
            ClearIntoxication();
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            ClearIntoxication();
            base.OnEntityDespawn(despawn);
        }

        private void ClearIntoxication()
        {
            if (sapi == null || entity == null) return;
            if (maxRange <= 0f) maxRange = 24f;

            var players = sapi.World.AllOnlinePlayers;
            if (players == null || players.Length == 0) return;

            double range = maxRange * 1.1f;
            double rangeSq = range * range;
            var selfPos = entity.ServerPos;

            for (int i = 0; i < players.Length; i++)
            {
                var player = players[i] as IServerPlayer;
                var playerEntity = player?.Entity;
                if (playerEntity == null) continue;
                if (playerEntity.ServerPos.Dimension != selfPos.Dimension) continue;

                double dx = playerEntity.ServerPos.X - selfPos.X;
                double dy = playerEntity.ServerPos.Y - selfPos.Y;
                double dz = playerEntity.ServerPos.Z - selfPos.Z;
                double distSq = dx * dx + dy * dy + dz * dz;
                if (distSq > rangeSq) continue;

                playerEntity.WatchedAttributes.SetLong(IntoxUntilMsKey, 0);
                playerEntity.WatchedAttributes.MarkPathDirty(IntoxUntilMsKey);
                playerEntity.WatchedAttributes.SetFloat("intoxication", 0f);
                playerEntity.WatchedAttributes.MarkPathDirty("intoxication");
            }
        }
    }
}
