using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VsQuest
{
    public class EntityBehaviorQuestBoss : EntityBehaviorQuestTarget
    {
        public string BossId => id;

        public EntityBehaviorQuestBoss(Entity entity) : base(entity)
        {
        }

        protected override string JsonIdKey => "bossId";
        protected override string WatchedIdKey => "alegacyvsquest:killaction:bossid";

        public override string PropertyName() => "questboss";

        public new static void SetSpawnerAnchor(Entity entity, BlockPos pos)
        {
            SetSpawnerAnchorStatic(entity, pos);
        }
    }
}
