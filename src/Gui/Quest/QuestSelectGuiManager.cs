using Vintagestory.API.Client;
using Vintagestory.GameContent;

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
            TryCloseOpenDialogue(capi);

            try
            {
                if (questSelectGui != null && questSelectGui.IsOpened())
                {
                    questSelectGui.TryClose();
                }
            }
            catch
            {
            }

            questSelectGui = CreateQuestSelectGui(message, capi);
            questSelectGui.TryOpen();
        }

        private static void TryCloseOpenDialogue(ICoreClientAPI capi)
        {
            try
            {
                var opened = capi?.Gui?.OpenedGuis;
                if (opened == null) return;

                for (int i = opened.Count - 1; i >= 0; i--)
                {
                    if (opened[i] is GuiDialogueDialog dlg && dlg.IsOpened())
                    {
                        dlg.TryClose();
                    }
                }
            }
            catch
            {
            }
        }

        private QuestSelectGui CreateQuestSelectGui(QuestInfoMessage message, ICoreClientAPI capi)
        {
            var gui = new QuestSelectGui(capi, message, config);
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
