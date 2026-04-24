using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace VsQuest
{
    public class ServerInfoGui : GuiDialog
    {
        public override string ToggleKeyCombinationCode => null;

        private bool recomposeQueued;
        private int curTab;
        private const int TextTop = 20;
        private const int ButtonGapAbove = 10;

        private readonly string[] tabLangKeys = new string[]
        {
            "albase:herald-tab-political",
            "albase:herald-tab-karma",
            "albase:herald-tab-duels",
            "albase:herald-tab-newbiequests",
            "albase:herald-tab-ossuary",
            "albase:herald-tab-claims",
            "albase:herald-tab-class",
            "albase:herald-tab-cities",
            "albase:herald-tab-teleporter",
            "albase:herald-tab-support"
        };

        private readonly string[] bodyLangKeys = new string[]
        {
            "albase:herald-body-political",
            "albase:herald-body-karma",
            "albase:herald-body-duels",
            "albase:herald-body-newbiequests",
            "albase:herald-body-ossuary",
            "albase:herald-body-claims",
            "albase:herald-body-class",
            "albase:herald-body-cities",
            "albase:herald-body-teleporter",
            "albase:herald-body-support"
        };

        public ServerInfoGui(ICoreClientAPI capi, int startTab = 0) : base(capi)
        {
            curTab = Math.Max(0, Math.Min(bodyLangKeys.Length - 1, startTab));
            RequestRecompose();
        }

        private void RequestRecompose()
        {
            if (recomposeQueued) return;
            recomposeQueued = true;

            capi.Event.EnqueueMainThreadTask(() =>
            {
                recomposeQueued = false;
                recompose();
            }, "alegacyvsquest-serverinfo-recompose");
        }

        private void recompose()
        {
            var prevComposer = SingleComposer;
            if (prevComposer != null)
            {
                prevComposer.Dispose();
                Composers.Remove("single");
            }

            const int tabsWidth = 240;
            const int mainWidth = 660;
            const int mainHeight = 600;
            const int buttonHeight = 20;
            const int bottomPadding = 10;
            int textHeight = mainHeight - TextTop - ButtonGapAbove - buttonHeight - bottomPadding;

            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;

            ElementBounds tabsBounds = ElementBounds.Fixed(-tabsWidth, 35, tabsWidth, 400);

            ElementBounds textBounds = ElementBounds.Fixed(0, TextTop, mainWidth, textHeight);
            ElementBounds clippingBounds = textBounds.ForkBoundingParent();
            ElementBounds scrollbarBounds = textBounds.CopyOffsetedSibling(textBounds.fixedWidth + 10).WithFixedWidth(20).WithFixedHeight(textBounds.fixedHeight);
            ElementBounds buttonBounds = ElementBounds.Fixed((mainWidth - 200) / 2, TextTop + textHeight + ButtonGapAbove, 200, buttonHeight);

            var tabsList = new List<GuiTab>();
            for (int i = 0; i < tabLangKeys.Length; i++)
            {
                tabsList.Add(new GuiTab() { Name = Lang.Get(tabLangKeys[i]), DataInt = i });
            }

            GuiTab[] tabs = tabsList.ToArray();

            SingleComposer = capi.Gui.CreateCompo("ServerInfoDialog-", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(Lang.Get("albase:herald-gui-title"), () => TryClose())
                .AddVerticalTabs(tabs, tabsBounds, OnTabClicked, "tabs")
                .BeginChildElements(bgBounds);

            SingleComposer.GetVerticalTab("tabs").SetValue(curTab, false);

            SingleComposer
                .BeginClip(clippingBounds)
                    .AddRichtext(Lang.Get(bodyLangKeys[curTab]), CairoFont.WhiteSmallishText(), textBounds, "infotext")
                .EndClip()
                .AddVerticalScrollbar(OnNewScrollbarvalue, scrollbarBounds, "scrollbar")
                .AddButton(Lang.Get("alegacyvsquest:button-cancel"), TryClose, buttonBounds);

            SingleComposer.EndChildElements().Compose();

            var textElement = SingleComposer.GetRichtext("infotext");
            if (textElement != null)
            {
                SingleComposer.GetScrollbar("scrollbar")?.SetHeights((float)textBounds.fixedHeight, (float)textBounds.fixedHeight);
                SingleComposer.GetScrollbar("scrollbar")?.SetNewTotalHeight((float)textElement.TotalHeight);
                SingleComposer.GetScrollbar("scrollbar")?.SetScrollbarPosition(0);
                OnNewScrollbarvalue(0);
            }
        }

        private void OnNewScrollbarvalue(float value)
        {
            var textArea = SingleComposer.GetRichtext("infotext");
            if (textArea == null) return;

            textArea.Bounds.fixedY = -value;
            textArea.Bounds.CalcWorldBounds();
        }

        private void OnTabClicked(int id, GuiTab tab)
        {
            curTab = id;
            RequestRecompose();
        }
    }
}
