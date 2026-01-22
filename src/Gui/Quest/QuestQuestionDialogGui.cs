using System;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace VsQuest
{
    public class QuestQuestionDialogGui : GuiDialog
    {
        public override string ToggleKeyCombinationCode => null;

        private readonly string token;
        private readonly string titleLangKey;
        private readonly string textLangKey;
        private readonly string[] optionLangKeys;

        public QuestQuestionDialogGui(ICoreClientAPI capi, string token, string titleLangKey, string textLangKey, string[] optionLangKeys) : base(capi)
        {
            this.token = token;
            this.titleLangKey = titleLangKey;
            this.textLangKey = textLangKey;
            this.optionLangKeys = optionLangKeys ?? Array.Empty<string>();
            Recompose();
        }

        private void Recompose()
        {
            int optionCount = optionLangKeys?.Length ?? 0;
            if (optionCount <= 0) optionCount = 1;

            double optionHeight = 24.0;
            double optionSpacing = 6.0;
            double optionAreaHeight = optionCount * optionHeight + (optionCount - 1) * optionSpacing;

            double textHeight = Math.Max(180.0, 380.0 - optionAreaHeight);
            double buttonStartY = 40.0 + textHeight + 12.0;

            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            ElementBounds textBounds = ElementBounds.Fixed(0, 40, 520, textHeight);
            ElementBounds clippingBounds = textBounds.ForkBoundingParent();
            ElementBounds scrollbarBounds = textBounds.CopyOffsetedSibling(textBounds.fixedWidth + 10).WithFixedWidth(20).WithFixedHeight(textBounds.fixedHeight);

            bgBounds.BothSizing = ElementSizing.FitToChildren;

            string titleText = LocalizationUtils.GetSafe(titleLangKey);
            string bodyText = LocalizationUtils.GetSafe(textLangKey);

            SingleComposer = capi.Gui.CreateCompo("QuestQuestionDialog-" + token, dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(titleText, () => TryClose())
                .BeginChildElements(bgBounds);

            SingleComposer
                .BeginClip(clippingBounds)
                    .AddRichtext(bodyText, CairoFont.WhiteSmallishText(), textBounds, "questiontext")
                .EndClip()
                .AddVerticalScrollbar(OnNewScrollbarvalue, scrollbarBounds, "scrollbar");

            for (int i = 0; i < optionLangKeys.Length; i++)
            {
                string optionKey = optionLangKeys[i];
                string optionLabel = LocalizationUtils.GetSafe(optionKey);
                int capturedIndex = i;

                ElementBounds optionBounds = ElementBounds.Fixed(0, buttonStartY + i * (optionHeight + optionSpacing), 520, optionHeight);
                SingleComposer.AddButton(optionLabel, () => OnOptionSelected(capturedIndex), optionBounds);
            }

            SingleComposer
                .EndChildElements()
                .Compose();

            SingleComposer.GetScrollbar("scrollbar")?.SetHeights((float)textBounds.fixedHeight, (float)textBounds.fixedHeight);
            SingleComposer.GetScrollbar("scrollbar")?.SetNewTotalHeight((float)SingleComposer.GetRichtext("questiontext").TotalHeight);
            SingleComposer.GetScrollbar("scrollbar")?.SetScrollbarPosition(0);
        }

        private void OnNewScrollbarvalue(float value)
        {
            var textArea = SingleComposer.GetRichtext("questiontext");
            textArea.Bounds.fixedY = -value;
            textArea.Bounds.CalcWorldBounds();
        }

        private bool OnOptionSelected(int index)
        {
            capi.Network.GetChannel("alegacyvsquest").SendPacket(new QuestQuestionAnswerMessage
            {
                Token = token,
                SelectedIndex = index
            });
            TryClose();
            return true;
        }

        public static void ShowFromMessage(ShowQuestQuestionMessage message, ICoreClientAPI capi)
        {
            new QuestQuestionDialogGui(capi, message.Token, message.TitleLangKey, message.TextLangKey, message.OptionLangKeys).TryOpen();
        }
    }
}
