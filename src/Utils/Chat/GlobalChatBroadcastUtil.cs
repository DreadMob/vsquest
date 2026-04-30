using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public static class GlobalChatBroadcastUtil
    {
        private static event System.Action<string> DiscordBroadcast;

        public static void SubscribeDiscordBroadcast(System.Action<string> handler)
        {
            if (handler == null) return;
            DiscordBroadcast += handler;
        }

        public static void UnsubscribeDiscordBroadcast(System.Action<string> handler)
        {
            if (handler == null) return;
            DiscordBroadcast -= handler;
        }

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

        public static void BroadcastGeneralChatWithDiscord(ICoreServerAPI sapi, string message, string discordMessage, EnumChatType chatType = EnumChatType.Notification)
        {
            // Broadcast to in-game chat
            BroadcastGeneralChat(sapi, message, chatType);

            // Send to Discord via callback if registered
            try
            {
                DiscordBroadcast?.Invoke(discordMessage);
            }
            catch (System.Exception ex)
            {
                sapi.Logger.Warning($"[alegacyvsquest] Failed to send Discord broadcast: {ex.Message}");
            }
        }
    }
}
