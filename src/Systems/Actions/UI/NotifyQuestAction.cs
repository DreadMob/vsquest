using Vintagestory.API.Server;

namespace VsQuest
{
    public class NotifyQuestAction : IQuestAction
    {
        public void Execute(ICoreServerAPI api, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (args.Length < 1) throw new QuestException("The 'notify' action requires 1 argument: message.");
            api.Network.GetChannel("alegacyvsquest").SendPacket(new ShowNotificationMessage() { Notification = args[0] }, byPlayer);
        }
    }
}
