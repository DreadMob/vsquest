using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace VsQuest
{
    public class EntityBehaviorGuiOnClick : EntityBehavior
    {
        private string guiType;

        public EntityBehaviorGuiOnClick(Entity entity) : base(entity)
        {
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            guiType = attributes?["guiType"]?.AsString() ?? "serverinfo";
        }

        public override void OnInteract(EntityAgent byEntity, ItemSlot slot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
        {
            if (mode == EnumInteractMode.Interact && byEntity is EntityPlayer player)
            {
                var sapi = entity.World as ICoreServerAPI;
                if (sapi != null && player is IServerPlayer serverPlayer)
                {
                    sapi.Logger.Debug("GuiOnClick: Opening GUI type {0} for player {1}", guiType, player?.EntityId);
                    // Send message to client to open specified GUI
                    switch (guiType.ToLower())
                    {
                        case "serverinfo":
                            sapi.Network.GetChannel(VsQuestNetworkRegistry.QuestChannelName)
                                .SendPacket(new ShowServerInfoMessage(), serverPlayer);
                            break;
                        // Можно добавить другие типы GUI в будущем
                        default:
                            sapi.Network.GetChannel(VsQuestNetworkRegistry.QuestChannelName)
                                .SendPacket(new ShowServerInfoMessage(), serverPlayer);
                            break;
                    }
                    // Don't mark as handled to allow other behaviors to process
                }
            }
        }

        public override string PropertyName() => "alegacyvsquestguionclick";
    }
}
