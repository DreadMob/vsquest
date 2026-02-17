using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace VsQuest
{
    public class EntityBehaviorAshFloorControls : EntityBehavior
    {
        public EntityBehaviorAshFloorControls(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "ashfloorcontrols";
    }
}
