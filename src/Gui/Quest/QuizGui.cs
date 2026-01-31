using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace VsQuest
{
    public class QuizDialogGui : GuiDialog
    {
        public override string ToggleKeyCombinationCode => null;
        public override bool UnregisterOnClose => true;

        private ShowQuizMessage message;

        public QuizDialogGui(ICoreClientAPI capi, ShowQuizMessage message) : base(capi)
        {
            this.message = message;
            Recompose();
        }

        private void Recompose()
        {
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            const double contentLeft = 10;
            const double contentTop = 40;
            const double contentWidth = 560;
            const double contentHeight = 330;
            const double scrollbarWidth = 20;
            const double scrollbarGap = 10;

            ElementBounds textBounds = ElementBounds.Fixed(contentLeft, contentTop, contentWidth - scrollbarGap - scrollbarWidth, contentHeight);
            ElementBounds clippingBounds = ElementBounds.Fixed(contentLeft, contentTop, contentWidth - scrollbarGap - scrollbarWidth, contentHeight);
            ElementBounds scrollbarBounds = ElementBounds.Fixed(contentLeft + (contentWidth - scrollbarWidth), contentTop, scrollbarWidth, contentHeight);

            ElementBounds btnABounds = ElementBounds.Fixed(10, 390, 270, 24);
            ElementBounds btnBBounds = ElementBounds.Fixed(290, 390, 270, 24);
            ElementBounds btnCBounds = ElementBounds.Fixed(10, 420, 270, 24);
            ElementBounds btnDBounds = ElementBounds.Fixed(290, 420, 270, 24);
            ElementBounds retryBounds = ElementBounds.Fixed(10, 455, 270, 24);
            ElementBounds closeBounds = ElementBounds.Fixed(290, 455, 270, 24);
            ElementBounds doneBounds = ElementBounds.Fixed(10, 455, 550, 24);

            bgBounds.BothSizing = ElementSizing.FitToChildren;

            string titleText = !string.IsNullOrWhiteSpace(message.TitleLangKey) ? LocalizationUtils.GetSafe(message.TitleLangKey) : Lang.Get("alegacyvsquest:quest");

            string bodyText;
            if (!string.IsNullOrWhiteSpace(message.BodyLangKey))
            {
                bodyText = LocalizationUtils.GetSafe(message.BodyLangKey);
            }
            else if (message.IsFinished || string.IsNullOrWhiteSpace(message.QuestionLangKey))
            {
                bodyText = "";
            }
            else
            {
                string q = LocalizationUtils.GetSafe(message.QuestionLangKey);
                string aText = StripOptionPrefix(LocalizationUtils.GetSafe(message.OptionALangKey));
                string bText = StripOptionPrefix(LocalizationUtils.GetSafe(message.OptionBLangKey));
                string cText = StripOptionPrefix(LocalizationUtils.GetSafe(message.OptionCLangKey));
                string dText = StripOptionPrefix(LocalizationUtils.GetSafe(message.OptionDLangKey));

                bodyText = $"{q}\n\nА) {aText}\nБ) {bText}\nВ) {cText}\nГ) {dText}";
            }

            string header = "";
            if (message.IsFinished)
            {
                bool passed = message.NeededCorrect <= 0 || message.Correct >= message.NeededCorrect;
                if (!string.IsNullOrWhiteSpace(message.ResultTemplateLangKey))
                {
                    header = $"{LocalizationUtils.GetSafe(message.ResultTemplateLangKey, message.Correct, message.Wrong, message.NeededCorrect)}\n\n";
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(message.ProgressTemplateLangKey))
                {
                    header = $"{LocalizationUtils.GetSafe(message.ProgressTemplateLangKey, message.QuestionIndex, message.TotalQuestions, message.Correct, message.Wrong, message.NeededCorrect)}\n\n";
                }
            }

            bodyText = header + bodyText;

            SingleComposer = capi.Gui.CreateCompo("QuizDialogGui-", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(titleText, () => TryClose())
                .BeginChildElements(bgBounds);

            SingleComposer
                .BeginClip(clippingBounds)
                    .AddRichtext(bodyText, CairoFont.WhiteSmallishText(), textBounds, "quiztext")
                .EndClip()
                .AddVerticalScrollbar(OnNewScrollbarvalue, scrollbarBounds, "scrollbar");

            if (!message.IsFinished)
            {
                string closeText = !string.IsNullOrWhiteSpace(message.CloseButtonLangKey) ? LocalizationUtils.GetSafe(message.CloseButtonLangKey) : Lang.Get("alegacyvsquest:button-cancel");

                SingleComposer
                    .AddButton("А", () => Submit(1), btnABounds)
                    .AddButton("Б", () => Submit(2), btnBBounds)
                    .AddButton("В", () => Submit(3), btnCBounds)
                    .AddButton("Г", () => Submit(4), btnDBounds)
                    .AddButton(closeText, TryClose, closeBounds);
            }
            else
            {
                bool passed = message.NeededCorrect <= 0 || message.Correct >= message.NeededCorrect;
                string closeText = passed
                    ? Lang.Get("alegacyvsquest:button-complete")
                    : (!string.IsNullOrWhiteSpace(message.CloseButtonLangKey) ? LocalizationUtils.GetSafe(message.CloseButtonLangKey) : Lang.Get("alegacyvsquest:button-cancel"));

                if (!passed)
                {
                    string retryText = !string.IsNullOrWhiteSpace(message.RetryButtonLangKey) ? LocalizationUtils.GetSafe(message.RetryButtonLangKey) : Lang.Get("alegacyvsquest:button-cancel");
                    SingleComposer
                        .AddButton(retryText, () => Retry(), retryBounds)
                        .AddButton(closeText, TryClose, closeBounds);
                }
                else
                {
                    SingleComposer.AddButton(closeText, TryClose, doneBounds);
                }
            }

            SingleComposer.EndChildElements().Compose();

            SingleComposer.GetScrollbar("scrollbar")?.SetHeights((float)textBounds.fixedHeight, (float)textBounds.fixedHeight);
            SingleComposer.GetScrollbar("scrollbar")?.SetNewTotalHeight((float)SingleComposer.GetRichtext("quiztext").TotalHeight);
            SingleComposer.GetScrollbar("scrollbar")?.SetScrollbarPosition(0);
        }

        private bool Submit(int option)
        {
            capi.Network.GetChannel(VsQuestNetworkRegistry.QuestChannelName).SendPacket(new SubmitQuizAnswerMessage
            {
                QuizId = message.QuizId,
                SelectedOption = option,
                Retry = false
            });

            return true;
        }

        private bool Retry()
        {
            capi.Network.GetChannel(VsQuestNetworkRegistry.QuestChannelName).SendPacket(new SubmitQuizAnswerMessage
            {
                QuizId = message.QuizId,
                SelectedOption = 0,
                Retry = true
            });

            return true;
        }

        private void OnNewScrollbarvalue(float value)
        {
            var textArea = SingleComposer?.GetRichtext("quiztext");
            if (textArea == null) return;
            textArea.Bounds.fixedY = -value;
            textArea.Bounds.CalcWorldBounds();
        }

        private static string StripOptionPrefix(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            string t = text.TrimStart();
            if (t.Length < 2) return text;

            char c0 = char.ToLowerInvariant(t[0]);
            if ((c0 == 'a' || c0 == 'b' || c0 == 'c' || c0 == 'd' || c0 == 'а' || c0 == 'б' || c0 == 'в' || c0 == 'г')
                && (t[1] == ')' || t[1] == '.' || t[1] == ':'))
            {
                t = t.Substring(2).TrimStart();
            }

            return t;
        }

        public static void ShowFromMessage(ShowQuizMessage message, ICoreClientAPI capi)
        {
            var existing = FindExistingDialog(capi);
            if (existing != null)
            {
                existing.UpdateFromMessage(message);
                return;
            }

            new QuizDialogGui(capi, message).TryOpen();
        }

        private void UpdateFromMessage(ShowQuizMessage message)
        {
            if (message == null) return;
            this.message = message;
            Recompose();
        }

        private static QuizDialogGui FindExistingDialog(ICoreClientAPI capi)
        {
            try
            {
                var opened = capi?.Gui?.OpenedGuis;
                if (opened == null) return null;

                foreach (var gui in opened)
                {
                    if (gui is QuizDialogGui quiz) return quiz;
                }
            }
            catch
            {
            }

            return null;
        }

        public override void OnGuiClosed()
        {
            try
            {
                base.OnGuiClosed();
            }
            catch
            {
            }

            try
            {
                Composers?.ClearComposers();
            }
            catch
            {
            }

            try
            {
                capi?.Gui?.OpenedGuis?.Remove(this);
                capi?.Gui?.LoadedGuis?.Remove(this);
            }
            catch
            {
            }
        }
    }
}
