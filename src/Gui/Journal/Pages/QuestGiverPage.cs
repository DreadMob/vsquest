using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace VsQuest.Gui.Journal
{
    public class QuestGiverPage : JournalPage
    {
        private string questGiverId;
        private string questGiverTitle;
        private int entryCount;

        public override string PageCode => "questgiver-" + questGiverId;
        public override string CategoryCode => "quests";

        public QuestGiverPage(ICoreClientAPI capi, string questGiverId, string questGiverTitle, int entryCount) : base(capi)
        {
            this.questGiverId = questGiverId;
            this.questGiverTitle = questGiverTitle;
            this.entryCount = entryCount;
            this.titleCached = questGiverTitle?.ToLowerInvariant() ?? "";
        }

        public string QuestGiverId => questGiverId;

        public override void RenderListEntryTo(ICoreClientAPI capi, float dt, double x, double y, double cellWidth, double cellHeight)
        {
            if (Texture == null)
            {
                string countText = entryCount == 1 
                    ? Lang.Get("alegacyvsquest:entry-count-single", entryCount)
                    : Lang.Get("alegacyvsquest:entry-count-plural", entryCount);
                string displayText = $"{questGiverTitle} ({countText})";
                Texture = new TextTextureUtil(capi).GenTextTexture(displayText, CairoFont.WhiteSmallText());
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
