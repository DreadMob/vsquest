using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace VsQuest.Gui.Journal
{
    public class NotesPlaceholderPage : JournalPage
    {
        public override string PageCode => "notes-placeholder";
        public override string CategoryCode => "notes";

        public NotesPlaceholderPage(ICoreClientAPI capi) : base(capi)
        {
            titleCached = "";
        }

        public override void RenderListEntryTo(ICoreClientAPI capi, float dt, double x, double y, double cellWidth, double cellHeight)
        {
            if (Texture == null)
            {
                Texture = new TextTextureUtil(capi).GenTextTexture(Lang.Get("alegacyvsquest:notes-coming-soon"), CairoFont.WhiteSmallText());
            }
            RenderTextureIfExists(x, y);
        }

        public override float GetTextMatchWeight(string searchText)
        {
            return 0f;
        }

        public override void ComposePage(GuiComposer composer, ElementBounds textBounds, ActionConsumable<string> openDetailPageFor)
        {
            composer.AddStaticText(Lang.Get("alegacyvsquest:notes-coming-soon"), CairoFont.WhiteSmallishText(), textBounds);
        }
    }
}
