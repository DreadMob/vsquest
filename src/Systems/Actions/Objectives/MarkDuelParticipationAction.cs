using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Marks duel participation in watched attributes for quest tracking
    /// Args: [questId]
    /// Increments alegacy_duel_participations counter
    /// </summary>
    public class MarkDuelParticipationAction : IQuestAction
    {
        public void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (sapi == null || byPlayer?.Entity?.WatchedAttributes == null) return;

            var wa = byPlayer.Entity.WatchedAttributes;
            string key = "alegacy_duel_participations";
            int current = wa.GetInt(key, 0);
            wa.SetInt(key, current + 1);
            wa.MarkPathDirty(key);
        }
    }
}
