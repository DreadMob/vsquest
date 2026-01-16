using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VsQuest
{
    public class EntityBehaviorQuestMob : EntityBehaviorQuestTarget
    {
        public string MobId => id;

        public EntityBehaviorQuestMob(Entity entity) : base(entity)
        {
        }

        protected override string JsonIdKey => "mobId";
        protected override string WatchedIdKey => "alegacyvsquest:killaction:mobid";

        public override string PropertyName() => "questmob";

        public new static void SetSpawnerAnchor(Entity entity, BlockPos pos)
        {
            SetSpawnerAnchorStatic(entity, pos);
        }
    }
}
