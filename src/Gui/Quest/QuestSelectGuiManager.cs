using Vintagestory.API.Client;

namespace VsQuest
{
    public class QuestSelectGuiManager
    {
        private QuestSelectGui questSelectGui;
        private readonly QuestConfig config;

        public QuestSelectGuiManager(QuestConfig config)
        {
            this.config = config;
        }

        public void HandleQuestInfoMessage(QuestInfoMessage message, ICoreClientAPI capi)
        {
            if (questSelectGui == null)
            {
                questSelectGui = CreateQuestSelectGui(message, capi);
                questSelectGui.TryOpen();
                return;
            }

            if (questSelectGui.IsOpened())
            {
                questSelectGui.UpdateFromMessage(message);
                return;
            }

            questSelectGui = CreateQuestSelectGui(message, capi);
            questSelectGui.TryOpen();
        }

        private QuestSelectGui CreateQuestSelectGui(QuestInfoMessage message, ICoreClientAPI capi)
        {
            var gui = new QuestSelectGui(capi, message.questGiverId, message.availableQestIds, message.activeQuests, config, message.noAvailableQuestDescLangKey, message.noAvailableQuestCooldownDescLangKey, message.noAvailableQuestCooldownDaysLeft, message.noAvailableQuestRotationDaysLeft, message.reputationNpcId, message.reputationFactionId, message.reputationNpcValue, message.reputationFactionValue, message.reputationNpcRankLangKey, message.reputationFactionRankLangKey, message.reputationNpcTitleLangKey, message.reputationFactionTitleLangKey, message.reputationNpcHasRewards, message.reputationFactionHasRewards, message.reputationNpcRewardsCount, message.reputationFactionRewardsCount);
            gui.OnClosed += () =>
            {
                if (questSelectGui != null && !questSelectGui.IsOpened())
                {
                    questSelectGui = null;
                }
            };
            return gui;
        }
    }
}
