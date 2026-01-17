using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public static class GlobalChatBroadcastUtil
    {
        public static void Broadcast(ICoreServerAPI sapi, int chatGroupId, string message, EnumChatType chatType)
        {
            if (sapi == null || string.IsNullOrWhiteSpace(message)) return;

            foreach (var p in sapi.World.AllOnlinePlayers)
            {
                if (p is IServerPlayer sp)
                {
                    sapi.SendMessage(sp, chatGroupId, message, chatType);
                }
            }
        }

        public static void BroadcastGeneralChat(ICoreServerAPI sapi, string message, EnumChatType chatType = EnumChatType.Notification)
        {
            Broadcast(sapi, Vintagestory.API.Config.GlobalConstants.GeneralChatGroup, message, chatType);
        }
    }
}
