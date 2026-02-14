using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class EntityBehaviorBossRepulseStun : EntityBehavior
    {
        private const string LastStartMsKey = "alegacyvsquest:bossrepulsestun:lastStartMs";

        private const string StunUntilKey = "alegacyvsquest:bossrepulsestun:until";
        private const string StunMultKey = "alegacyvsquest:bossrepulsestun:mult";
        private const string StunStatKey = "alegacyvsquest:bossrepulsestun:stat";

        private class Stage
        {
            public float whenHealthRelBelow;
            public float cooldownSeconds;

            public float range;

            public float knockbackStrength;
            public float maxPlayerMotion;

            public int stunMs;
            public float victimWalkSpeedMult;

            public string sound;
            public float soundRange;
            public int soundStartMs;
            public float soundVolume;
        }

        private ICoreServerAPI sapi;
        private readonly List<Stage> stages = new List<Stage>();

        private long lastCleanupMs;

        public EntityBehaviorBossRepulseStun(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bossrepulsestun";

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            sapi = entity?.Api as ICoreServerAPI;

            stages.Clear();
            try
            {
                foreach (var stageObj in attributes["stages"].AsArray())
                {
                    if (stageObj == null || !stageObj.Exists) continue;

                    var stage = new Stage
                    {
                        whenHealthRelBelow = stageObj["whenHealthRelBelow"].AsFloat(1f),
                        cooldownSeconds = stageObj["cooldownSeconds"].AsFloat(0f),

                        range = stageObj["range"].AsFloat(4.5f),

                        knockbackStrength = stageObj["knockbackStrength"].AsFloat(0.30f),
                        maxPlayerMotion = stageObj["maxPlayerMotion"].AsFloat(0.55f),

                        stunMs = stageObj["stunMs"].AsInt(900),
                        victimWalkSpeedMult = stageObj["victimWalkSpeedMult"].AsFloat(0.0f),

                        sound = stageObj["sound"].AsString(null),
                        soundRange = stageObj["soundRange"].AsFloat(24f),
                        soundStartMs = stageObj["soundStartMs"].AsInt(0),
                        soundVolume = stageObj["soundVolume"].AsFloat(1f),
                    };

                    if (stage.cooldownSeconds < 0f) stage.cooldownSeconds = 0f;
                    if (stage.range <= 0f) stage.range = 0.5f;

                    if (stage.knockbackStrength < 0f) stage.knockbackStrength = 0f;
                    if (stage.maxPlayerMotion <= 0f) stage.maxPlayerMotion = 0.35f;

                    if (stage.stunMs < 0) stage.stunMs = 0;
                    stage.victimWalkSpeedMult = GameMath.Clamp(stage.victimWalkSpeedMult, 0f, 1f);

                    if (stage.soundVolume <= 0f) stage.soundVolume = 1f;

                    stages.Add(stage);
                }
            }
            catch
            {
            }
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            if (sapi == null || entity == null) return;
            if (stages.Count == 0) return;

            CleanupExpiredStuns();

            if (!entity.Alive) return;

            if (!BossBehaviorUtils.TryGetHealthFraction(entity, out float frac)) return;

            int stageIndex = -1;
            for (int i = 0; i < stages.Count; i++)
            {
                if (frac <= stages[i].whenHealthRelBelow)
                {
                    stageIndex = i;
                }
            }

            if (stageIndex < 0 || stageIndex >= stages.Count) return;

            var stage = stages[stageIndex];
            if (!BossBehaviorUtils.IsCooldownReady(sapi, entity, LastStartMsKey, stage.cooldownSeconds)) return;

            if (!TryFindTarget(stage, out var target, out float dist)) return;
            if (dist > stage.range) return;
            if (dist < 0.75f) return;

            BossBehaviorUtils.MarkCooldownStart(sapi, entity, LastStartMsKey);

            TryApplyKnockback(stage, target);
            TryApplyStun(stage, target);
            TryPlaySound(stage);
        }

        private void CleanupExpiredStuns()
        {
            if (sapi == null) return;

            long now = sapi.World.ElapsedMilliseconds;
            if (lastCleanupMs != 0 && now - lastCleanupMs < 350) return;
            lastCleanupMs = now;

            var players = sapi.World.AllOnlinePlayers;
            if (players == null || players.Length == 0) return;

            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] is not IServerPlayer sp) continue;
                if (sp.Entity is not EntityPlayer plr) continue;
                if (plr.Stats == null) continue;

                long until = 0;
                try
                {
                    until = plr.WatchedAttributes.GetLong(StunUntilKey, 0);
                }
                catch
                {
                    until = 0;
                }

                if (until <= 0) continue;

                // World.ElapsedMilliseconds resets on relog/server restart, but WatchedAttributes persist.
                // If 'until' is far in the future compared to 'now', it is almost certainly stale data.
                // In that case, clear the effect so players don't get stuck with permanent 0 walkspeed.
                if (now > 0)
                {
                    const long MaxFutureMs = 5L * 60L * 1000L;
                    if (until - now > MaxFutureMs)
                    {
                        try
                        {
                            plr.WatchedAttributes.SetLong(StunUntilKey, 0);
                            plr.WatchedAttributes.MarkPathDirty(StunUntilKey);
                        }
                        catch
                        {
                        }

                        try
                        {
                            plr.WatchedAttributes.SetFloat(StunMultKey, 1f);
                            plr.WatchedAttributes.MarkPathDirty(StunMultKey);
                        }
                        catch
                        {
                        }

                        try
                        {
                            plr.Stats.Set("walkspeed", StunStatKey, 0f, true);
                            plr.walkSpeed = plr.Stats.GetBlended("walkspeed");
                        }
                        catch
                        {
                        }

                        continue;
                    }
                }

                if (now >= until)
                {
                    try
                    {
                        plr.WatchedAttributes.SetLong(StunUntilKey, 0);
                        plr.WatchedAttributes.MarkPathDirty(StunUntilKey);
                    }
                    catch
                    {
                    }

                    try
                    {
                        plr.WatchedAttributes.SetFloat(StunMultKey, 1f);
                        plr.WatchedAttributes.MarkPathDirty(StunMultKey);
                    }
                    catch
                    {
                    }

                    try
                    {
                        plr.Stats.Set("walkspeed", StunStatKey, 0f, true);
                        plr.walkSpeed = plr.Stats.GetBlended("walkspeed");
                    }
                    catch
                    {
                    }

                    continue;
                }

                float mult = 0f;
                try
                {
                    mult = GameMath.Clamp(plr.WatchedAttributes.GetFloat(StunMultKey, 0f), 0f, 1f);
                }
                catch
                {
                    mult = 0f;
                }

                try
                {
                    float modifier = mult - 1f;
                    plr.Stats.Set("walkspeed", StunStatKey, modifier, true);
                    plr.walkSpeed = plr.Stats.GetBlended("walkspeed");
                }
                catch
                {
                }
            }
        }

        private bool TryFindTarget(Stage stage, out EntityPlayer target, out float dist)
        {
            target = null;
            dist = 0f;

            if (sapi == null || entity == null || stage == null) return false;

            double search = Math.Max(1.0, stage.range);
            try
            {
                var own = entity.ServerPos.XYZ;
                float frange = (float)search;
                var found = sapi.World.GetNearestEntity(own, frange, frange, e => e is EntityPlayer) as EntityPlayer;
                if (found == null || !found.Alive) return false;
                if (found.ServerPos.Dimension != entity.ServerPos.Dimension) return false;

                target = found;
                dist = (float)found.ServerPos.DistanceTo(entity.ServerPos);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void TryApplyKnockback(Stage stage, EntityPlayer target)
        {
            if (stage == null || target == null || entity == null) return;

            try
            {
                var dir = new Vec3d(target.ServerPos.X - entity.ServerPos.X, 0, target.ServerPos.Z - entity.ServerPos.Z);
                if (dir.Length() < 0.001) return;
                dir.Normalize();

                double kb = stage.knockbackStrength;
                if (kb <= 0.0001) kb = 0.20;

                double max = stage.maxPlayerMotion;
                if (max <= 0.0001) max = 0.35;

                double kbX = GameMath.Clamp(dir.X * kb, -max, max);
                double kbZ = GameMath.Clamp(dir.Z * kb, -max, max);

                // Only update if values changed significantly to reduce network sync spam
                double prevKbX = target.WatchedAttributes.GetDouble("kbdirX", 0.0);
                double prevKbZ = target.WatchedAttributes.GetDouble("kbdirZ", 0.0);
                
                if (Math.Abs(prevKbX - kbX) > 0.001 || Math.Abs(prevKbZ - kbZ) > 0.001)
                {
                    target.WatchedAttributes.SetDouble("kbdirX", kbX);
                    target.WatchedAttributes.SetDouble("kbdirY", 0.0);
                    target.WatchedAttributes.SetDouble("kbdirZ", kbZ);

                    target.WatchedAttributes.MarkPathDirty("kbdirX");
                    target.WatchedAttributes.MarkPathDirty("kbdirY");
                    target.WatchedAttributes.MarkPathDirty("kbdirZ");
                }

                // Entity.Attributes is not synced to the client. Vanilla sets Attributes["dmgkb"] client-side
                // via a WatchedAttributes["onHurt"] modified listener. We replicate that trigger with a tiny value.
                float prevOnHurt = target.WatchedAttributes.GetFloat("onHurt", 0f);
                if (Math.Abs(prevOnHurt - 0.01f) > 0.001f)
                {
                    target.WatchedAttributes.SetFloat("onHurt", 0.01f);
                    target.WatchedAttributes.MarkPathDirty("onHurt");
                }
                target.WatchedAttributes.SetInt("onHurtCounter", target.WatchedAttributes.GetInt("onHurtCounter") + 1);
                target.WatchedAttributes.MarkPathDirty("onHurtCounter");
            }
            catch
            {
            }
        }

        private void TryApplyStun(Stage stage, EntityPlayer target)
        {
            if (stage == null || target == null) return;
            if (stage.stunMs <= 0) return;
            if (target.Stats == null) return;

            try
            {
                long until = sapi.World.ElapsedMilliseconds + Math.Max(50, stage.stunMs);
                target.WatchedAttributes.SetLong(StunUntilKey, until);
                target.WatchedAttributes.MarkPathDirty(StunUntilKey);
            }
            catch
            {
            }

            try
            {
                float mult = GameMath.Clamp(stage.victimWalkSpeedMult, 0f, 1f);
                target.WatchedAttributes.SetFloat(StunMultKey, mult);
                target.WatchedAttributes.MarkPathDirty(StunMultKey);
            }
            catch
            {
            }

            try
            {
                float mult = GameMath.Clamp(stage.victimWalkSpeedMult, 0f, 1f);
                float modifier = mult - 1f;
                target.Stats.Set("walkspeed", StunStatKey, modifier, true);
                BossBehaviorUtils.UpdatePlayerWalkSpeed(target);
            }
            catch
            {
            }
        }

        private void TryPlaySound(Stage stage)
        {
            if (sapi == null || stage == null) return;
            if (string.IsNullOrWhiteSpace(stage.sound)) return;

            try
            {
                var soundLoc = AssetLocation.Create(stage.sound, "game").WithPathPrefixOnce("sounds/");
                if (soundLoc == null) return;

                float range = stage.soundRange > 0f ? stage.soundRange : 24f;
                float volume = stage.soundVolume;
                if (volume <= 0f) volume = 1f;

                if (stage.soundStartMs > 0)
                {
                    sapi.Event.RegisterCallback(_ =>
                    {
                        try
                        {
                            sapi.World.PlaySoundAt(soundLoc, entity, null, randomizePitch: true, range, volume);
                        }
                        catch
                        {
                        }
                    }, stage.soundStartMs);
                }
                else
                {
                    sapi.World.PlaySoundAt(soundLoc, entity, null, randomizePitch: true, range, volume);
                }
            }
            catch
            {
            }
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            base.OnEntityDeath(damageSourceForDeath);
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            base.OnEntityDespawn(despawn);
        }
    }
}
