using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace VsQuest
{
    public class RichTextComponentQuestHover : RichTextComponent
    {
        private readonly ICoreClientAPI capi;
        private readonly string hoverText;
        private GuiElementHoverText hoverElem;
        private bool isHover;
        private string lastHoverText;

        public RichTextComponentQuestHover(ICoreClientAPI api, string displayText, string hoverText, CairoFont font)
            : base(api, displayText, font)
        {
            capi = api;
            this.hoverText = hoverText;
            MouseOverCursor = "linkselect";
        }

        private void EnsureHoverElem()
        {
            if (hoverElem != null) return;

            var bounds = ElementBounds.Fixed(0, 0, 1, 1);
            bounds.ParentBounds = ElementBounds.Empty;
            hoverElem = new GuiElementHoverText(capi, "", CairoFont.WhiteSmallText(), 420, bounds);
            hoverElem.SetAutoDisplay(false);
            hoverElem.SetAutoWidth(true);
            hoverElem.SetFollowMouse(true);
            hoverElem.SetVisible(false);
        }

        private void UpdateHoverText()
        {
            if (hoverElem == null) return;
            if (lastHoverText == hoverText) return;

            lastHoverText = hoverText;
            hoverElem.SetNewText(hoverText);
        }

        public override void RenderInteractiveElements(float deltaTime, double renderX, double renderY, double renderZ)
        {
            base.RenderInteractiveElements(deltaTime, renderX, renderY, renderZ);

            isHover = false;
            double offsetX = GetFontOrientOffsetX();

            for (int i = 0; i < BoundsPerLine.Length; i++)
            {
                if (BoundsPerLine[i].PointInside(capi.Input.MouseX - renderX - offsetX, capi.Input.MouseY - renderY))
                {
                    isHover = true;
                    break;
                }
            }

            if (!isHover || string.IsNullOrWhiteSpace(hoverText))
            {
                if (hoverElem != null)
                {
                    hoverElem.SetVisible(false);
                }
                return;
            }

            EnsureHoverElem();
            UpdateHoverText();
            hoverElem.SetVisible(true);

            bool scissorWasEnabled = capi.Render.ScissorStack.Count > 0;
            if (scissorWasEnabled)
            {
                capi.Render.GlScissorFlag(false);
            }

            hoverElem.RenderInteractiveElements(deltaTime);

            if (scissorWasEnabled)
            {
                capi.Render.GlScissorFlag(true);
            }
        }

        public override bool UseMouseOverCursor(ElementBounds richtextBounds)
        {
            return isHover;
        }

        public override void Dispose()
        {
            base.Dispose();
            hoverElem?.Dispose();
        }
    }
}
