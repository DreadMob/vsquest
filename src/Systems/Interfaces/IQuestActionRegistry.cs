using System;
using Vintagestory.API.Server;

namespace VsQuest
{
    public interface IQuestActionRegistry
    {
        void RegisterActions(ICoreServerAPI sapi, Action<IServerPlayer, QuestAcceptedMessage, ICoreServerAPI> onQuestAcceptedCallback);
    }
}
