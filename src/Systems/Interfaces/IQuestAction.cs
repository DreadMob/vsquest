using Vintagestory.API.Server;

namespace VsQuest
{
    public interface IQuestAction
    {
        void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer player, string[] args);
    }
}
