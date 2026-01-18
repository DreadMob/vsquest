using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace VsQuest.Gui.Journal
{
    public class QuestPage : JournalPage
    {
        private string questGiverId;
        private string questId;
        private string questTitle;

        public override string PageCode => "quest-" + questId;
        public override string CategoryCode => "quests";

        public QuestPage(ICoreClientAPI capi, string questGiverId, string questId, string questTitle) : base(capi)
        {
            this.questGiverId = questGiverId;
            this.questId = questId;
            this.questTitle = questTitle;
            this.titleCached = questTitle?.ToLowerInvariant() ?? "";
        }

        public string QuestGiverId => questGiverId;
        public string QuestId => questId;

        public override void RenderListEntryTo(ICoreClientAPI capi, float dt, double x, double y, double cellWidth, double cellHeight)
        {
            if (Texture == null)
            {
                Texture = new TextTextureUtil(capi).GenTextTexture(questTitle, CairoFont.WhiteSmallText());
            }
            RenderTextureIfExists(x, y);
        }

        public override float GetTextMatchWeight(string searchText)
        {
            if (string.IsNullOrEmpty(searchText)) return 1f;
            if (titleCached.Equals(searchText, StringComparison.OrdinalIgnoreCase)) return 4f;
            if (titleCached.StartsWith(searchText, StringComparison.OrdinalIgnoreCase)) return 3f;
            if (titleCached.Contains(searchText, StringComparison.OrdinalIgnoreCase)) return 2f;
            return 0f;
        }

        public override void ComposePage(GuiComposer composer, ElementBounds textBounds, ActionConsumable<string> openDetailPageFor)
        {
        }
    }
}
