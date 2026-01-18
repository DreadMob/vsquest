using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace VsQuest.Gui.Journal
{
    public abstract class JournalPage : IJournalPage
    {
        public int PageNumber;
        public LoadedTexture Texture;
        protected string titleCached;
        protected ICoreClientAPI capi;

        public abstract string PageCode { get; }
        public abstract string CategoryCode { get; }
        public virtual bool Visible { get; set; } = true;

        protected JournalPage(ICoreClientAPI capi)
        {
            this.capi = capi;
        }

        public abstract void RenderListEntryTo(ICoreClientAPI capi, float dt, double x, double y, double cellWidth, double cellHeight);

        public abstract float GetTextMatchWeight(string searchText);

        public abstract void ComposePage(GuiComposer composer, ElementBounds textBounds, ActionConsumable<string> openDetailPageFor);

        public virtual void Dispose()
        {
            Texture?.Dispose();
            Texture = null;
        }

        protected void RenderTextureIfExists(double x, double y)
        {
            if (Texture == null) return;
            float pad = (float)GuiElement.scaled(10.0);
            capi.Render.Render2DTexturePremultipliedAlpha(Texture.TextureId, x + pad, y + pad / 2, Texture.Width, Texture.Height);
        }
    }
}
