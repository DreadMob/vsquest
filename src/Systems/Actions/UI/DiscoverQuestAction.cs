using Vintagestory.API.Server;

namespace VsQuest
{
    public class DiscoverQuestAction : IQuestAction
    {
        public void Execute(ICoreServerAPI api, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (args.Length < 1) throw new QuestException("The 'discover' action requires 1 argument: message.");
            api.Network.GetChannel("vsquest").SendPacket(new ShowDiscoveryMessage() { Notification = args[0] }, byPlayer);
        }
    }
}
