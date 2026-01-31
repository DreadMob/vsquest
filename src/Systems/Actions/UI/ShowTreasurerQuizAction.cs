using Vintagestory.API.Server;

namespace VsQuest
{
    public class ShowTreasurerQuizAction : IQuestAction
    {
        public const string QuizId = "albase:treasurer-survey";

        public void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (sapi == null || byPlayer == null) return;

            var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();
            questSystem?.QuizSystem?.StartQuiz(sapi, byPlayer, QuizId, reset: true);
        }
    }
}
