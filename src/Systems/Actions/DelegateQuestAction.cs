using System;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class DelegateQuestAction : IQuestAction
    {
        private readonly Action<ICoreServerAPI, QuestMessage, IServerPlayer, string[]> action;

        public DelegateQuestAction(Action<ICoreServerAPI, QuestMessage, IServerPlayer, string[]> action)
        {
            this.action = action;
        }

        public void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer player, string[] args)
        {
            action?.Invoke(sapi, message, player, args);
        }
    }
}
