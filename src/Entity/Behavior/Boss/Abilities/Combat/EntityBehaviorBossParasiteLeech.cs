using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class EntityBehaviorBossParasiteLeech : EntityBehavior
    {
        private const string LastStartMsKey = "alegacyvsquest:bossparasiteleech:lastStartMs";
        private const string DebuffUntilKey = "alegacyvsquest:bossparasiteleech:until";

        private const string DebuffStatKey = "alegacyvsquest:bossparasiteleech:stat";

        private class Stage
        {
            public float whenHealthRelBelow;
            public float cooldownSeconds;

            public float minTargetRange;
            public float maxTargetRange;

            public int durationMs;
            public int tickIntervalMs;

            public float damagePerTick;
            public int damageTier;
            public string damageType;

            public float healBossPerTick;
            public float healBossRelPerTick;

            public float victimWalkSpeedDelta;
            public float victimHealingDelta;
            public float victimHungerRateDelta;

            public string sound;
            public float soundRange;
            public int soundStartMs;

            public string loopSound;
            public float loopSoundRange;
            public int loopSoundIntervalMs;
        }

        private ICoreServerAPI sapi;
        private readonly List<Stage> stages = new List<Stage>();

        private readonly BossBehaviorUtils.LoopSound loopSoundPlayer = new BossBehaviorUtils.LoopSound();

        private long nextTickAtMs;
        private EntityPlayer target;
        private int activeStageIndex = -1;

        public EntityBehaviorBossParasiteLeech(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bossparasiteleech";

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

                        minTargetRange = stageObj["minTargetRange"].AsFloat(0f),
                        maxTargetRange = stageObj["maxTargetRange"].AsFloat(30f),

                        durationMs = stageObj["durationMs"].AsInt(6000),
                        tickIntervalMs = stageObj["tickIntervalMs"].AsInt(750),

                        damagePerTick = stageObj["damagePerTick"].AsFloat(0f),
                        damageTier = stageObj["damageTier"].AsInt(2),
                        damageType = stageObj["damageType"].AsString("PiercingAttack"),

                        healBossPerTick = stageObj["healBossPerTick"].AsFloat(0f),
                        healBossRelPerTick = stageObj["healBossRelPerTick"].AsFloat(0f),

                        victimWalkSpeedDelta = stageObj["victimWalkSpeedDelta"].AsFloat(-0.1f),
                        victimHealingDelta = stageObj["victimHealingDelta"].AsFloat(-0.2f),
                        victimHungerRateDelta = stageObj["victimHungerRateDelta"].AsFloat(0.15f),

                        sound = stageObj["sound"].AsString(null),
                        soundRange = stageObj["soundRange"].AsFloat(24f),
                        soundStartMs = stageObj["soundStartMs"].AsInt(0),

                        loopSound = stageObj["loopSound"].AsString(null),
                        loopSoundRange = stageObj["loopSoundRange"].AsFloat(24f),
                        loopSoundIntervalMs = stageObj["loopSoundIntervalMs"].AsInt(900),
                    };

                    if (stage.cooldownSeconds < 0f) stage.cooldownSeconds = 0f;
                    if (stage.minTargetRange < 0f) stage.minTargetRange = 0f;
                    if (stage.maxTargetRange < stage.minTargetRange) stage.maxTargetRange = stage.minTargetRange;

                    if (stage.durationMs <= 0) stage.durationMs = 500;
                    if (stage.tickIntervalMs <= 0) stage.tickIntervalMs = 250;

                    if (stage.damagePerTick < 0f) stage.damagePerTick = 0f;
                    if (stage.damageTier < 0) stage.damageTier = 0;

                    stages.Add(stage);
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

            if (!entity.Alive)
            {
                StopDebuff();
                return;
            }

            if (target != null)
            {
                TickDebuff();
                return;
            }

            if (!BossBehaviorUtils.TryGetHealthFraction(entity, out float frac)) return;

            int stageIndex = -1;
            for (int i = 0; i < stages.Count; i++)
            {
                var stage = stages[i];
                if (frac <= stage.whenHealthRelBelow)
                {
                    stageIndex = i;
                }
            }

            if (stageIndex < 0 || stageIndex >= stages.Count) return;

            var activeStage = stages[stageIndex];
            if (!BossBehaviorUtils.IsCooldownReady(sapi, entity, LastStartMsKey, activeStage.cooldownSeconds)) return;

            if (!TryFindTarget(activeStage, out var candidate, out float dist)) return;
            if (dist < activeStage.minTargetRange) return;
            if (dist > activeStage.maxTargetRange) return;

            StartDebuff(activeStage, stageIndex, candidate);
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            StopDebuff();
            base.OnEntityDeath(damageSourceForDeath);
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            StopDebuff();
            base.OnEntityDespawn(despawn);
        }

        private void StartDebuff(Stage stage, int stageIndex, EntityPlayer targetPlayer)
        {
            if (sapi == null || entity == null || stage == null || targetPlayer == null) return;

            BossBehaviorUtils.MarkCooldownStart(sapi, entity, LastStartMsKey);

            activeStageIndex = stageIndex;
            target = targetPlayer;

            try
            {
                target.WatchedAttributes.SetLong(DebuffUntilKey, sapi.World.ElapsedMilliseconds + Math.Max(250, stage.durationMs));
                target.WatchedAttributes.MarkPathDirty(DebuffUntilKey);
            }
            catch (Exception ex)
            {
                entity?.Api?.Logger?.Error($"[vsquest] Exception in StartDebuff DebuffUntil: {ex}");
            }

            ApplyDebuffStats(stage, target);

            TryPlaySound(stage);
            TryStartLoopSound(stage);

            nextTickAtMs = sapi.World.ElapsedMilliseconds;
        }

        private void TickDebuff()
        {
            if (sapi == null || entity == null) return;
            if (target == null || !target.Alive)
            {
                StopDebuff();
                return;
            }

            if (target.ServerPos.Dimension != entity.ServerPos.Dimension)
            {
                StopDebuff();
                return;
            }

            if (activeStageIndex < 0 || activeStageIndex >= stages.Count)
            {
                StopDebuff();
                return;
            }

            long until = 0;
            try
            {
                until = target.WatchedAttributes.GetLong(DebuffUntilKey, 0);
            }
            catch (Exception ex)
            {
                entity?.Api?.Logger?.Error($"[vsquest] Exception in TickDebuff GetLong: {ex}");
                until = 0;
            }

            if (until <= 0 || sapi.World.ElapsedMilliseconds >= until)
            {
                StopDebuff();
                return;
            }

            var stage = stages[activeStageIndex];

            ApplyDebuffStats(stage, target);

            long now = sapi.World.ElapsedMilliseconds;
            if (now < nextTickAtMs) return;

            nextTickAtMs = now + Math.Max(250, stage.tickIntervalMs);

            if (stage.damagePerTick > 0f)
            {
                DealDamage(stage);
            }

            if (stage.healBossPerTick > 0f || stage.healBossRelPerTick > 0f)
            {
                HealBoss(stage);
            }
        }

        private void StopDebuff()
        {
            loopSoundPlayer.Stop();

            if (target != null)
            {
                try
                {
                    target.WatchedAttributes.SetLong(DebuffUntilKey, 0);
                    target.WatchedAttributes.MarkPathDirty(DebuffUntilKey);
                }
                catch (Exception ex)
                {
                    entity?.Api?.Logger?.Error($"[vsquest] Exception in StopDebuff DebuffUntil: {ex}");
                }

                ClearDebuffStats(target);
            }

            target = null;
            activeStageIndex = -1;
            nextTickAtMs = 0;
        }

        private void ApplyDebuffStats(Stage stage, EntityPlayer player)
        {
            if (stage == null || player == null) return;
            if (player.Stats == null) return;

            try
            {
                player.Stats.Set("walkspeed", DebuffStatKey, stage.victimWalkSpeedDelta, true);
                player.Stats.Set("healingeffectivness", DebuffStatKey, stage.victimHealingDelta, true);
                player.Stats.Set("hungerrate", DebuffStatKey, stage.victimHungerRateDelta, true);
                BossBehaviorUtils.UpdatePlayerWalkSpeed(player);
            }
            catch (Exception ex)
            {
                entity?.Api?.Logger?.Error($"[vsquest] Exception in ApplyDebuffStats: {ex}");
            }
        }

        private void ClearDebuffStats(EntityPlayer player)
        {
            if (player?.Stats == null) return;

            try
            {
                player.Stats.Set("walkspeed", DebuffStatKey, 0f, true);
                player.Stats.Set("healingeffectivness", DebuffStatKey, 0f, true);
                player.Stats.Set("hungerrate", DebuffStatKey, 0f, true);
                BossBehaviorUtils.UpdatePlayerWalkSpeed(player);
            }
            catch (Exception ex)
            {
                entity?.Api?.Logger?.Error($"[vsquest] Exception in ClearDebuffStats: {ex}");
            }
        }

        private void DealDamage(Stage stage)
        {
            if (stage == null || target == null) return;

            EnumDamageType dmgType = EnumDamageType.PiercingAttack;
            try
            {
                if (!string.IsNullOrWhiteSpace(stage.damageType) && Enum.TryParse(stage.damageType, ignoreCase: true, out EnumDamageType parsed))
                {
                    dmgType = parsed;
                }
            }
            catch (Exception ex)
            {
                entity?.Api?.Logger?.Error($"[vsquest] Exception in DealDamage EnumParse: {ex}");
            }

            try
            {
                target.ReceiveDamage(new DamageSource()
                {
                    Source = EnumDamageSource.Entity,
                    SourceEntity = entity,
                    Type = dmgType,
                    DamageTier = stage.damageTier,
                    KnockbackStrength = 0f
                }, stage.damagePerTick);
            }
            catch (Exception ex)
            {
                entity?.Api?.Logger?.Error($"[vsquest] Exception in DealDamage ReceiveDamage: {ex}");
            }
        }

        private void HealBoss(Stage stage)
        {
            if (stage == null) return;
            if (!BossBehaviorUtils.TryGetHealth(entity, out var healthTree, out float curHealth, out float maxHealth)) return;

            float abs = stage.healBossPerTick > 0f ? stage.healBossPerTick : 0f;
            float rel = stage.healBossRelPerTick > 0f ? stage.healBossRelPerTick * maxHealth : 0f;
            float heal = abs + rel;
            if (heal <= 0f) return;

            try
            {
                float newHealth = Math.Min(maxHealth, curHealth + heal);
                healthTree.SetFloat("currenthealth", newHealth);
                entity.WatchedAttributes.MarkPathDirty("health");
            }
            catch (Exception ex)
            {
                entity?.Api?.Logger?.Error($"[vsquest] Exception in HealBoss: {ex}");
            }
        }

        private bool TryFindTarget(Stage stage, out EntityPlayer targetPlayer, out float dist)
        {
            targetPlayer = null;
            dist = 0f;

            if (sapi == null || entity == null) return false;

            double range = Math.Max(2.0, stage.maxTargetRange > 0 ? stage.maxTargetRange : 30f);
            try
            {
                var own = entity.ServerPos.XYZ;
                float frange = (float)range;
                var found = sapi.World.GetNearestEntity(own, frange, frange, e => e is EntityPlayer) as EntityPlayer;
                if (found == null || !found.Alive) return false;

                if (found.ServerPos.Dimension != entity.ServerPos.Dimension) return false;

                targetPlayer = found;
                dist = (float)found.ServerPos.DistanceTo(entity.ServerPos);
                return true;
            }
            catch (Exception ex)
            {
                entity?.Api?.Logger?.Error($"[vsquest] Exception in TryFindTarget: {ex}");
                return false;
            }
        }

        private void TryPlaySound(Stage stage)
        {
            if (sapi == null || stage == null) return;
            if (string.IsNullOrWhiteSpace(stage.sound)) return;

            AssetLocation soundLoc = AssetLocation.Create(stage.sound, "game").WithPathPrefixOnce("sounds/");
            if (soundLoc == null) return;

            if (stage.soundStartMs > 0)
            {
                sapi.Event.RegisterCallback(_ =>
                {
                    try
                    {
                        sapi.World.PlaySoundAt(soundLoc, entity, null, randomizePitch: true, stage.soundRange);
                    }
                    catch
                    {
                    }
                }, stage.soundStartMs);
            }
            else
            {
                try
                {
                    sapi.World.PlaySoundAt(soundLoc, entity, null, randomizePitch: true, stage.soundRange);
                }
                catch
                {
                }
            }
        }

        private void TryStartLoopSound(Stage stage)
        {
            if (sapi == null || stage == null) return;
            if (string.IsNullOrWhiteSpace(stage.loopSound)) return;

            loopSoundPlayer.Start(sapi, entity, stage.loopSound, stage.loopSoundRange, stage.loopSoundIntervalMs);
        }
    }
}
