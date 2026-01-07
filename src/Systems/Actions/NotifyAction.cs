using Vintagestory.API.Common;
using Vintagestory.API.Server;
using vsquest.src.Systems.Actions;

namespace VsQuest
{
    public static class NotifyAction
    {
        public static void Execute(ICoreServerAPI api, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (args.Length < 1) throw new QuestException("The 'notify' action requires 1 argument: message.");
            api.Network.GetChannel("vsquest").SendPacket(new ShowNotificationMessage() { Notification = args[0] }, byPlayer);
        }
    }
}
