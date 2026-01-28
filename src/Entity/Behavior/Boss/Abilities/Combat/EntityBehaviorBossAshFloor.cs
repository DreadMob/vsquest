using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class EntityBehaviorBossAshFloor : EntityBehavior
    {
        private const string LastStartMsKey = "alegacyvsquest:bossashfloor:lastStartMs";

        private class Stage
        {
            public float whenHealthRelBelow;
            public float cooldownSeconds;

            public float minTargetRange;
            public float maxTargetRange;

            public float minRadius;
            public float maxRadius;
            public int tries;

            public int blockCount;
            public int durationMs;

            public int tickIntervalMs;
            public float damage;
            public int damageTier;
            public string damageType;

            public float victimWalkSpeedMult;
            public bool disableJump;
            public bool disableShift;

            public int windupMs;
            public string windupAnimation;

            public string sound;
            public float soundRange;
            public int soundStartMs;
            public float soundVolume;
        }

        private ICoreServerAPI sapi;
        private readonly List<Stage> stages = new List<Stage>();

        private long callbackId;
        private bool pending;

        public EntityBehaviorBossAshFloor(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bossashfloor";

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
                        maxTargetRange = stageObj["maxTargetRange"].AsFloat(40f),

                        minRadius = stageObj["minRadius"].AsFloat(0f),
                        maxRadius = stageObj["maxRadius"].AsFloat(6f),
                        tries = stageObj["tries"].AsInt(14),

                        blockCount = stageObj["blockCount"].AsInt(8),
                        durationMs = stageObj["durationMs"].AsInt(6000),

                        tickIntervalMs = stageObj["tickIntervalMs"].AsInt(350),
                        damage = stageObj["damage"].AsFloat(0f),
                        damageTier = stageObj["damageTier"].AsInt(0),
                        damageType = stageObj["damageType"].AsString("Acid"),

                        victimWalkSpeedMult = stageObj["victimWalkSpeedMult"].AsFloat(0.35f),
                        disableJump = stageObj["disableJump"].AsBool(true),
                        disableShift = stageObj["disableShift"].AsBool(true),

                        windupMs = stageObj["windupMs"].AsInt(0),
                        windupAnimation = stageObj["windupAnimation"].AsString(null),

                        sound = stageObj["sound"].AsString(null),
                        soundRange = stageObj["soundRange"].AsFloat(24f),
                        soundStartMs = stageObj["soundStartMs"].AsInt(0),
                        soundVolume = stageObj["soundVolume"].AsFloat(1f)
                    };

                    if (stage.cooldownSeconds < 0f) stage.cooldownSeconds = 0f;
                    if (stage.minTargetRange < 0f) stage.minTargetRange = 0f;
                    if (stage.maxTargetRange < stage.minTargetRange) stage.maxTargetRange = stage.minTargetRange;

                    if (stage.minRadius < 0f) stage.minRadius = 0f;
                    if (stage.maxRadius < stage.minRadius) stage.maxRadius = stage.minRadius;
                    if (stage.tries <= 0) stage.tries = 1;

                    if (stage.blockCount <= 0) stage.blockCount = 1;
                    if (stage.durationMs <= 0) stage.durationMs = 500;

                    if (stage.tickIntervalMs < 50) stage.tickIntervalMs = 50;
                    if (stage.damage < 0f) stage.damage = 0f;
                    if (stage.damageTier < 0) stage.damageTier = 0;

                    stage.victimWalkSpeedMult = GameMath.Clamp(stage.victimWalkSpeedMult, 0f, 1f);

                    if (stage.windupMs < 0) stage.windupMs = 0;
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
            if (entity.Api?.Side != EnumAppSide.Server) return;

            if (!entity.Alive)
            {
                CancelPending();
                return;
            }

            if (pending) return;

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
            if (dist < stage.minTargetRange) return;
            if (dist > stage.maxTargetRange) return;

            Start(stage, target);
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            CancelPending();
            base.OnEntityDeath(damageSourceForDeath);
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            CancelPending();
            base.OnEntityDespawn(despawn);
        }

        private void Start(Stage stage, EntityPlayer target)
        {
            if (sapi == null || entity == null || stage == null || target == null) return;

            BossBehaviorUtils.MarkCooldownStart(sapi, entity, LastStartMsKey);

            pending = true;

            BossBehaviorUtils.StopAiAndFreeze(entity);
            TryPlaySound(stage);
            TryPlayAnimation(stage.windupAnimation);

            int delay = Math.Max(0, stage.windupMs);
            BossBehaviorUtils.UnregisterCallbackSafe(sapi, ref callbackId);
            callbackId = sapi.Event.RegisterCallback(_ =>
            {
                try
                {
                    PlaceAsh(stage, target);
                }
                catch
                {
                }

                pending = false;
                callbackId = 0;

            }, delay);
        }

        private void PlaceAsh(Stage stage, EntityPlayer target)
        {
            if (sapi == null || entity == null || stage == null || target == null) return;

            var ba = sapi.World?.BlockAccessor;
            if (ba == null) return;

            Block ashBlock = sapi.World.GetBlock(new AssetLocation("alegacyvsquest:ashfloor"));
            if (ashBlock == null || ashBlock.IsMissing) return;

            int dim = entity.ServerPos.Dimension;
            var center = target.ServerPos.XYZ;

            long now = sapi.World.ElapsedMilliseconds;
            long despawnAt = now + Math.Max(250, stage.durationMs);

            var used = new HashSet<string>(StringComparer.Ordinal);

            int tries = Math.Max(1, stage.tries);
            int count = Math.Max(1, stage.blockCount);

            for (int i = 0; i < count; i++)
            {
                if (TryFindPlacePos(ba, center, dim, stage.minRadius, stage.maxRadius, tries, used, out var pos))
                {
                    TryPlaceOne(ba, ashBlock, pos, despawnAt, stage);
                    TryPlaceOne(ba, ashBlock, pos.AddCopy(1, 0, 0), despawnAt, stage);
                    TryPlaceOne(ba, ashBlock, pos.AddCopy(0, 0, 1), despawnAt, stage);
                    TryPlaceOne(ba, ashBlock, pos.AddCopy(1, 0, 1), despawnAt, stage);
                }
            }
        }

        private void TryPlaceOne(IBlockAccessor ba, Block ashBlock, BlockPos pos, long despawnAtMs, Stage stage)
        {
            if (sapi == null || ba == null || ashBlock == null || pos == null || stage == null) return;

            try
            {
                if (!ba.IsValidPos(pos)) return;
            }
            catch
            {
                return;
            }

            try
            {
                var at = ba.GetBlock(pos);
                var below = ba.GetBlock(pos.DownCopy());

                if (at == null || below == null) return;
                if (at.Replaceable < 6000) return;
                if (!below.SideSolid[BlockFacing.UP.Index]) return;
            }
            catch
            {
                return;
            }

            try
            {
                ba.RemoveBlockEntity(pos);
            }
            catch
            {
            }

            try
            {
                ba.SetBlock(ashBlock.BlockId, pos);
            }
            catch
            {
                return;
            }

            if (ashBlock.EntityClass != null)
            {
                try
                {
                    ba.SpawnBlockEntity(ashBlock.EntityClass, pos);
                    var be = ba.GetBlockEntity(pos) as BlockEntityAshFloor;
                    be?.Arm(entity.EntityId, despawnAtMs, stage.tickIntervalMs, stage.damage, stage.damageTier, stage.damageType, stage.victimWalkSpeedMult, stage.disableJump, stage.disableShift);
                }
                catch
                {
                }
            }
        }

        private bool TryFindPlacePos(IBlockAccessor ba, Vec3d center, int dim, float minRadius, float maxRadius, int tries, HashSet<string> used, out BlockPos pos)
        {
            pos = null;
            if (sapi == null || ba == null || center == null) return false;

            double minR = Math.Max(0.0, minRadius);
            double maxR = Math.Max(minR, maxRadius);
            if (maxR <= 0.01) maxR = 0.01;

            int baseY = (int)Math.Round(center.Y);

            for (int attempt = 0; attempt < tries; attempt++)
            {
                double ang = sapi.World.Rand.NextDouble() * Math.PI * 2.0;
                double dist = minR + sapi.World.Rand.NextDouble() * (maxR - minR);

                int x = (int)Math.Floor(center.X + Math.Cos(ang) * dist);
                int z = (int)Math.Floor(center.Z + Math.Sin(ang) * dist);

                for (int dy = 3; dy >= -3; dy--)
                {
                    int y = baseY + dy;
                    if (y < 1) continue;

                    var p = new BlockPos(x, y, z, dim);
                    if (!ba.IsValidPos(p)) continue;

                    Block at;
                    Block below;
                    try
                    {
                        at = ba.GetBlock(p);
                        below = ba.GetBlock(new BlockPos(x, y - 1, z, dim));
                    }
                    catch
                    {
                        continue;
                    }

                    if (at == null || below == null) continue;
                    if (at.Replaceable < 6000) continue;
                    if (!below.SideSolid[BlockFacing.UP.Index]) continue;

                    string key = $"{x},{y},{z},{dim}";
                    if (used != null && used.Contains(key)) continue;
                    used?.Add(key);

                    pos = p;
                    return true;
                }
            }

            return false;
        }

        private bool TryFindTarget(Stage stage, out EntityPlayer target, out float dist)
        {
            target = null;
            dist = 0f;

            if (sapi == null || entity == null || stage == null) return false;

            double range = Math.Max(2.0, stage.maxTargetRange > 0 ? stage.maxTargetRange : 40f);
            try
            {
                var own = entity.ServerPos.XYZ;
                float frange = (float)range;
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

        private void CancelPending()
        {
            if (sapi != null)
            {
                BossBehaviorUtils.UnregisterCallbackSafe(sapi, ref callbackId);
            }

            pending = false;
            callbackId = 0;
        }

        private void TryPlayAnimation(string animation)
        {
            if (string.IsNullOrWhiteSpace(animation)) return;

            try
            {
                entity?.AnimManager?.StartAnimation(animation);
            }
            catch
            {
            }
        }

        private void TryPlaySound(Stage stage)
        {
            if (sapi == null || stage == null) return;
            if (string.IsNullOrWhiteSpace(stage.sound)) return;

            if (!BossBehaviorUtils.ShouldPlaySoundLimited(entity, stage.sound, 500)) return;

            AssetLocation soundLoc = AssetLocation.Create(stage.sound, "game").WithPathPrefixOnce("sounds/");
            if (soundLoc == null) return;

            float volume = stage.soundVolume;
            if (volume <= 0f) volume = 1f;

            if (stage.soundStartMs > 0)
            {
                sapi.Event.RegisterCallback(_ =>
                {
                    try
                    {
                        sapi.World.PlaySoundAt(soundLoc, entity, null, randomizePitch: true, stage.soundRange, volume);
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
                    sapi.World.PlaySoundAt(soundLoc, entity, null, randomizePitch: true, stage.soundRange, volume);
                }
                catch
                {
                }
            }
        }
    }
}
