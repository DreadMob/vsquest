using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VsQuest
{
    public class EntityBehaviorQuestBoss : EntityBehavior
    {
        private const string AnchorKeyPrefix = "vsquest:spawner:";

        private string bossId;
        private float? maxHealthOverride;
        private float leashRange;
        private float returnMoveSpeed;
        private int leashCheckMs;
        private long lastLeashCheckMs;

        public string BossId => bossId;

        public EntityBehaviorQuestBoss(Entity entity) : base(entity)
        {
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            bossId = attributes["bossId"].AsString(null);
            maxHealthOverride = attributes.KeyExists("maxHealth") ? attributes["maxHealth"].AsFloat() : (float?)null;

            leashRange = attributes["leashRange"].AsFloat(0);
            returnMoveSpeed = attributes["returnMoveSpeed"].AsFloat(0.04f);
            leashCheckMs = attributes["leashCheckMs"].AsInt(1000);

            if (entity?.WatchedAttributes != null)
            {
                string waBossId = entity.WatchedAttributes.GetString("vsquest:killaction:bossid", null);
                if (!string.IsNullOrWhiteSpace(waBossId)) bossId = waBossId;
            }

            if (entity?.Api?.Side == EnumAppSide.Server)
            {
                TryApplyHealthOverride();
            }
        }

        public override void OnGameTick(float deltaTime)
        {
            base.OnGameTick(deltaTime);

            if (entity?.Api?.Side != EnumAppSide.Server) return;
            if (!(entity is EntityAgent agent)) return;
            if (!agent.Alive) return;
            if (leashRange <= 0) return;

            long nowMs = agent.World.ElapsedMilliseconds;
            if (nowMs - lastLeashCheckMs < leashCheckMs) return;
            lastLeashCheckMs = nowMs;

            if (!TryGetAnchor(out var anchor)) return;

            double dx = agent.ServerPos.X - anchor.X;
            double dy = agent.ServerPos.Y - anchor.Y;
            double dz = agent.ServerPos.Z - anchor.Z;
            if ((dx * dx + dy * dy + dz * dz) <= leashRange * leashRange) return;

            var taskAi = agent.GetBehavior<EntityBehaviorTaskAI>();
            if (taskAi?.PathTraverser != null && taskAi.PathTraverser.Ready)
            {
                taskAi.PathTraverser.NavigateTo_Async(anchor, returnMoveSpeed, 0.5f, null, null, null, 1000, 1, null);
            }
        }

        private bool TryGetAnchor(out Vec3d anchor)
        {
            anchor = null;
            var wa = entity?.WatchedAttributes;
            if (wa == null) return false;

            int dim = wa.GetInt(AnchorKeyPrefix + "dim", int.MinValue);
            if (dim == int.MinValue) return false;
            if (entity.Pos.Dimension != dim) return false;

            int x = wa.GetInt(AnchorKeyPrefix + "x", int.MinValue);
            int y = wa.GetInt(AnchorKeyPrefix + "y", int.MinValue);
            int z = wa.GetInt(AnchorKeyPrefix + "z", int.MinValue);
            if (x == int.MinValue || y == int.MinValue || z == int.MinValue) return false;

            anchor = new Vec3d(x + 0.5, y, z + 0.5);
            return true;
        }

        private void TryApplyHealthOverride()
        {
            if (!maxHealthOverride.HasValue) return;
            var healthBh = entity.GetBehavior<EntityBehaviorHealth>();
            if (healthBh == null) return;

            healthBh.BaseMaxHealth = maxHealthOverride.Value;
            healthBh.UpdateMaxHealth();
            healthBh.Health = healthBh.MaxHealth;
        }

        public override string PropertyName() => "questboss";

        public static void SetSpawnerAnchor(Entity entity, BlockPos pos)
        {
            if (entity?.WatchedAttributes == null || pos == null) return;

            entity.WatchedAttributes.SetInt(AnchorKeyPrefix + "x", pos.X);
            entity.WatchedAttributes.SetInt(AnchorKeyPrefix + "y", pos.Y);
            entity.WatchedAttributes.SetInt(AnchorKeyPrefix + "z", pos.Z);
            entity.WatchedAttributes.SetInt(AnchorKeyPrefix + "dim", pos.dimension);

            entity.WatchedAttributes.MarkPathDirty(AnchorKeyPrefix + "x");
            entity.WatchedAttributes.MarkPathDirty(AnchorKeyPrefix + "y");
            entity.WatchedAttributes.MarkPathDirty(AnchorKeyPrefix + "z");
            entity.WatchedAttributes.MarkPathDirty(AnchorKeyPrefix + "dim");

            entity.WatchedAttributes.SetInt("vsquest:spawner:x", pos.X);
            entity.WatchedAttributes.SetInt("vsquest:spawner:y", pos.Y);
            entity.WatchedAttributes.SetInt("vsquest:spawner:z", pos.Z);
            entity.WatchedAttributes.SetInt("vsquest:spawner:dim", pos.dimension);

            entity.WatchedAttributes.MarkPathDirty("vsquest:spawner:x");
            entity.WatchedAttributes.MarkPathDirty("vsquest:spawner:y");
            entity.WatchedAttributes.MarkPathDirty("vsquest:spawner:z");
            entity.WatchedAttributes.MarkPathDirty("vsquest:spawner:dim");
        }
    }
}
