using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace VsQuest
{
    public class QuestJournalHotkeyHandler
    {
        private const string JournalHotkeyCode = "alegacyvsquest-journal";
        private GuiDialog questJournalGui;
        private readonly ICoreClientAPI capi;

        public QuestJournalHotkeyHandler(ICoreClientAPI capi)
        {
            this.capi = capi;
        }

        public void Register()
        {
            capi.Input.RegisterHotKey(JournalHotkeyCode, Lang.Get("alegacyvsquest:hotkey-journal"), GlKeys.N, HotkeyType.GUIOrOtherControls);
            capi.Input.SetHotKeyHandler(JournalHotkeyCode, _ =>
            {
                Toggle();
                return true;
            });
        }

        public void Toggle()
        {
            if (questJournalGui == null)
            {
                questJournalGui = new Gui.Journal.QuestJournalDialog(capi);
                questJournalGui.OnClosed += () =>
                {
                    if (questJournalGui != null && !questJournalGui.IsOpened())
                    {
                        questJournalGui = null;
                    }
                };
                questJournalGui.TryOpen();
                return;
            }

            if (questJournalGui.IsOpened())
            {
                questJournalGui.TryClose();
                return;
            }

            (questJournalGui as Gui.Journal.QuestJournalDialog)?.Refresh();
            questJournalGui.TryOpen();
        }
    }
}
