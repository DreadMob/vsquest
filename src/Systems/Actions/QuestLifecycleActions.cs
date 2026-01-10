using Vintagestory.API.Server;

namespace VsQuest
{
    public static class QuestLifecycleActions
    {
        public static void CompleteQuest(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();
            QuestCompletedMessage questCompletedMessage;
            if (args != null && args.Length >= 1)
            {
                long questGiverId = message.questGiverId;
                if (args.Length >= 2)
                {
                    questGiverId = long.Parse(args[1]);
                }

                questCompletedMessage = new QuestCompletedMessage() { questGiverId = questGiverId, questId = args[0] };
            }
            else
            {
                questCompletedMessage = new QuestCompletedMessage() { questGiverId = message.questGiverId, questId = message.questId };
            }
            questSystem.OnQuestCompleted(byPlayer, questCompletedMessage, sapi);
        }
    }
}
