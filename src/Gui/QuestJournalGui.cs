using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace VsQuest
{
    public class QuestJournalGui : GuiDialog
    {
        public override string ToggleKeyCombinationCode => null;

        private const string DropDownKey = "entrydropdown";
        private const string EntryListKey = DropDownKey;
        private const string QuestDropDownKey = "questdropdown";
        private const string LastQuestIdKey = "alegacyvsquest:journal:lastquestid";
        private const string LastEntryKey = "alegacyvsquest:journal:lastentrykey";
        private const int MaxTabCount = 12;

        private bool recomposeQueued;
        private IClientPlayer player;
        private bool keepEntryListOpen;
        private bool useEntryListMenu;

        private List<string> questIds = new List<string>();
        private List<QuestJournalEntry> entriesForQuest = new List<QuestJournalEntry>();
        private int curTab;
        private string selectedQuestId;
        private string selectedEntryKey;

        public QuestJournalGui(ICoreClientAPI capi) : base(capi)
        {
            player = capi.World.Player;
            ApplyData();
            RequestRecompose();
        }

        public void Refresh()
        {
            ApplyData();
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
            }, "alegacyvsquest-journal-recompose");
        }

        private void ApplyData()
        {
            questIds.Clear();
            entriesForQuest.Clear();

            if (player?.Entity?.WatchedAttributes == null)
            {
                selectedQuestId = null;
                selectedEntryKey = null;
                return;
            }

            var entries = QuestJournalEntry.Load(player.Entity.WatchedAttributes)
                .Where(e => e != null && !string.IsNullOrWhiteSpace(e.QuestId))
                .ToList();

            questIds = entries
                .Select(e => e.QuestId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (questIds.Count == 0)
            {
                selectedQuestId = null;
                selectedEntryKey = null;
                return;
            }

            string lastQuestId = player.Entity.WatchedAttributes.GetString(LastQuestIdKey, null);
            string lastEntryKey = player.Entity.WatchedAttributes.GetString(LastEntryKey, null);
            if (string.IsNullOrWhiteSpace(selectedQuestId)
                && !string.IsNullOrWhiteSpace(lastQuestId)
                && questIds.Contains(lastQuestId))
            {
                selectedQuestId = lastQuestId;
            }

            if (string.IsNullOrWhiteSpace(selectedQuestId) || !questIds.Contains(selectedQuestId))
            {
                selectedQuestId = questIds[0];
                selectedEntryKey = null;
            }

            UpdateEntriesForQuest(entries, selectedQuestId);

            if (string.IsNullOrWhiteSpace(selectedEntryKey)
                && !string.IsNullOrWhiteSpace(lastEntryKey)
                && entriesForQuest.Any(e => string.Equals(e?.LoreCode, lastEntryKey, StringComparison.OrdinalIgnoreCase)))
            {
                selectedEntryKey = lastEntryKey;
            }

            if (entriesForQuest.Count > 0 && !string.IsNullOrWhiteSpace(selectedEntryKey))
            {
                curTab = Math.Max(0, entriesForQuest.FindIndex(e => string.Equals(e.LoreCode, selectedEntryKey, StringComparison.OrdinalIgnoreCase)));
            }
        }

        private void UpdateEntriesForQuest(List<QuestJournalEntry> allEntries, string questId)
        {
            entriesForQuest = allEntries
                .Where(e => e != null && string.Equals(e.QuestId, questId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (entriesForQuest.Count == 0)
            {
                selectedEntryKey = null;
                return;
            }

            if (string.IsNullOrWhiteSpace(selectedEntryKey) || entriesForQuest.All(e => !string.Equals(e.LoreCode, selectedEntryKey, StringComparison.OrdinalIgnoreCase)))
            {
                selectedEntryKey = entriesForQuest[entriesForQuest.Count - 1].LoreCode;
            }
        }

        private void recompose()
        {
            CloseOpenedDropDown();
            var prevComposer = SingleComposer;
            if (prevComposer != null)
            {
                prevComposer.Dispose();
                Composers.Remove("single");
            }

            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            const double headerY = 18;
            const double entryHeaderY = 52;
            const double contentY = 70;
            const double contentHeight = 470;
            const double entryOffsetX = -240;
            const double textWidth = 420;
            const double headerPaddingX = 10;

            ElementBounds tabBounds = ElementBounds.Fixed(entryOffsetX, contentY, 220, contentHeight);
            ElementBounds textBounds = ElementBounds.Fixed(0, contentY, textWidth, contentHeight);
            ElementBounds scrollbarBounds = textBounds.CopyOffsetedSibling(textBounds.fixedWidth + 10).WithFixedWidth(20).WithFixedHeight(textBounds.fixedHeight);
            ElementBounds clippingBounds = textBounds.ForkBoundingParent();
            double bottomButtonY = contentY + contentHeight + 12;
            ElementBounds bottomButtonBounds = ElementBounds.Fixed((textWidth - 200) / 2, bottomButtonY, 200, 20);

            bgBounds.BothSizing = ElementSizing.FitToChildren;

            SingleComposer = capi.Gui.CreateCompo("QuestJournalDialog-", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(Lang.Get("alegacyvsquest:journal-title"), () => TryClose())
                .BeginChildElements(bgBounds);

            if (questIds.Count == 0)
            {
                SingleComposer.AddStaticText(Lang.Get("alegacyvsquest:no-journal-entries"), CairoFont.WhiteSmallishText(), ElementBounds.Fixed(0, 60, 420, 120))
                    .AddButton(Lang.Get("alegacyvsquest:button-cancel"), TryClose, bottomButtonBounds)
                    .EndChildElements()
                    .Compose();
                return;
            }

            string[] questKeys = questIds.ToArray();
            string[] questTitles = questIds.Select(QuestTitle).ToArray();
            int selectedQuestIndex = Math.Max(0, questIds.FindIndex(id => string.Equals(id, selectedQuestId, StringComparison.OrdinalIgnoreCase)));

            ElementBounds questDropDownBounds = ElementBounds.Fixed(textBounds.fixedX + headerPaddingX, headerY, textBounds.fixedWidth - headerPaddingX * 2, 30);

            SingleComposer.AddDropDown(questKeys, questTitles, selectedQuestIndex, OnQuestSelectionChanged, questDropDownBounds, QuestDropDownKey);

            if (entriesForQuest.Count > 0)
            {
                if (string.IsNullOrWhiteSpace(selectedEntryKey)
                    || entriesForQuest.All(e => e == null || !string.Equals(e.LoreCode, selectedEntryKey, StringComparison.OrdinalIgnoreCase)))
                {
                    selectedEntryKey = entriesForQuest.FirstOrDefault(e => e != null && !string.IsNullOrWhiteSpace(e.LoreCode))?.LoreCode;
                }

                int selectedIndex = Math.Max(0, entriesForQuest.FindIndex(e => string.Equals(e.LoreCode, selectedEntryKey, StringComparison.OrdinalIgnoreCase)));

                string[] entryKeys = entriesForQuest.Select(e => e.LoreCode).ToArray();
                string groupTitle = QuestTitle(selectedQuestId);
                string[] entryTitles = entriesForQuest.Select(e => StripEntryPrefixForList(EntryTitle(e), groupTitle)).ToArray();

                bool useEntryTabs = entriesForQuest.Count <= MaxTabCount
                    && ShouldUseEntryTabs(entryTitles, tabBounds.fixedWidth - 12);
                keepEntryListOpen = !useEntryTabs;
                useEntryListMenu = keepEntryListOpen;
                if (useEntryTabs)
                {
                    GuiTab[] entryTabs = entriesForQuest
                        .Select((entry, index) => new GuiTab() { Name = StripEntryPrefixForList(EntryTitle(entry), groupTitle), DataInt = index })
                        .ToArray();

                    SingleComposer.AddVerticalTabs(entryTabs, tabBounds, OnTabClicked, "tabs");
                    SingleComposer.GetVerticalTab("tabs").SetValue(curTab, false);
                }
                else
                {
                    ElementBounds entryDropDownBounds = ElementBounds.Fixed(tabBounds.fixedX, entryHeaderY, tabBounds.fixedWidth, 30);

                    SingleComposer.AddDropDown(entryKeys, entryTitles, selectedIndex, OnEntrySelectionChanged, entryDropDownBounds, DropDownKey);
                }

                SingleComposer
                    .BeginClip(clippingBounds)
                        .AddRichtext(EntryText(selectedEntryKey), CairoFont.WhiteSmallishText(), textBounds, "entrytext")
                    .EndClip()
                    .AddVerticalScrollbar(OnNewScrollbarvalue, scrollbarBounds, "scrollbar")
                    .AddButton(Lang.Get("alegacyvsquest:button-cancel"), TryClose, bottomButtonBounds);
            }
            else
            {
                SingleComposer.AddStaticText(Lang.Get("alegacyvsquest:no-journal-entries"), CairoFont.WhiteSmallishText(), ElementBounds.Fixed(0, 60, 420, 120))
                    .AddButton(Lang.Get("alegacyvsquest:button-cancel"), TryClose, bottomButtonBounds);
            }

            SingleComposer.EndChildElements()
                .Compose();

            SingleComposer.GetScrollbar("scrollbar")?.SetHeights((float)textBounds.fixedHeight, (float)textBounds.fixedHeight);
            SingleComposer.GetScrollbar("scrollbar")?.SetNewTotalHeight((float)(SingleComposer.GetRichtext("entrytext")?.TotalHeight ?? textBounds.fixedHeight));
            SingleComposer.GetScrollbar("scrollbar")?.SetScrollbarPosition(0);
            EnsureEntryListOpen();
        }

        private void OnNewScrollbarvalue(float value)
        {
            var textArea = SingleComposer.GetRichtext("entrytext");
            if (textArea == null) return;

            textArea.Bounds.fixedY = -value;
            textArea.Bounds.CalcWorldBounds();
        }

        private void OnTabClicked(int id, GuiTab tab)
        {
            CloseOpenedDropDown();
            curTab = id;

            if (id >= 0 && id < entriesForQuest.Count)
            {
                selectedEntryKey = entriesForQuest[id]?.LoreCode;
            }

            ApplyData();
            RequestRecompose();
        }

        private void OnEntrySelectionChanged(string entryKey, bool selected)
        {
            if (!selected) return;

            selectedEntryKey = entryKey;
            SingleComposer.GetRichtext("entrytext")?.SetNewText(EntryText(selectedEntryKey), CairoFont.WhiteSmallishText());
            SingleComposer.GetScrollbar("scrollbar")?.SetNewTotalHeight((float)(SingleComposer.GetRichtext("entrytext")?.TotalHeight ?? 0f));
            SingleComposer.GetScrollbar("scrollbar")?.SetScrollbarPosition(0);
            OnNewScrollbarvalue(0);

            capi.Event.EnqueueMainThreadTask(() =>
            {
                CloseOpenedDropDown();
                RequestRecompose();
            }, "alegacyvsquest-journal-entrychanged");
        }

        private void OnQuestSelectionChanged(string questId, bool selected)
        {
            if (!selected) return;

            selectedQuestId = questId;
            selectedEntryKey = null;

            ApplyData();
            RequestRecompose();
        }

        private string QuestTitle(string questId)
        {
            if (string.IsNullOrWhiteSpace(questId)) return "";

            string title = Lang.Get(questId + "-title");
            return title == questId + "-title" ? questId : title;
        }

        private string EntryTitle(QuestJournalEntry entry)
        {
            if (entry == null) return "";
            if (!string.IsNullOrWhiteSpace(entry.Title)) return StripKnownNpcPrefix(entry.Title);
            return StripKnownNpcPrefix(entry.LoreCode ?? "");
        }

        private static string StripKnownNpcPrefix(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";

            string s = value.Trim();

            int colon = s.IndexOf(':');
            if (colon > 0 && colon < s.Length - 1)
            {
                string prefix = s.Substring(0, colon).Trim();
                if (prefix.Equals("game", StringComparison.OrdinalIgnoreCase) || prefix.Equals("survival", StringComparison.OrdinalIgnoreCase) || prefix.Equals("albase", StringComparison.OrdinalIgnoreCase) || prefix.Equals("vsquestdebugging", StringComparison.OrdinalIgnoreCase))
                {
                    s = s.Substring(colon + 1).TrimStart();
                }
            }

            return s;
        }

        private static string StripEntryPrefixForList(string entryTitle, string groupTitle)
        {
            if (string.IsNullOrWhiteSpace(entryTitle)) return "";
            if (string.IsNullOrWhiteSpace(groupTitle)) return entryTitle;

            string normalizedTitle = entryTitle.TrimStart();
            string prefix = groupTitle.Trim();
            if (prefix.Length == 0) return entryTitle;

            if (!normalizedTitle.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return entryTitle;
            }

            int start = normalizedTitle.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
            if (start < 0) return entryTitle;

            int cut = start + prefix.Length;
            while (cut < normalizedTitle.Length && (normalizedTitle[cut] == ':' || char.IsWhiteSpace(normalizedTitle[cut])))
            {
                cut++;
            }

            return normalizedTitle.Substring(cut).TrimStart();
        }

        private string EntryText(string entryKey)
        {
            if (string.IsNullOrWhiteSpace(entryKey)) return "";

            var entry = entriesForQuest.FirstOrDefault(e => string.Equals(e.LoreCode, entryKey, StringComparison.OrdinalIgnoreCase));
            if (entry?.Chapters == null || entry.Chapters.Count == 0) return "";

            string Normalize(string s)
            {
                if (string.IsNullOrEmpty(s)) return s;
                return s.Replace("\\r\\n", "\n").Replace("\\n", "\n");
            }

            return string.Join("\n\n", entry.Chapters.Where(c => !string.IsNullOrWhiteSpace(c)).Select(Normalize));
        }

        private float ComputeDropdownWidth(string[] texts, double maxWidth, float minWidth)
        {
            float maxTextWidth = minWidth;
            float charWidth = (float)GuiStyle.SmallishFontSize * 0.62f;

            foreach (var text in texts)
            {
                if (string.IsNullOrWhiteSpace(text)) continue;
                float estimatedWidth = text.Length * charWidth;
                maxTextWidth = Math.Max(maxTextWidth, estimatedWidth);
            }

            return Math.Min(maxTextWidth + 30f, (float)maxWidth);
        }

        private bool ShouldUseEntryTabs(IEnumerable<string> titles, double maxWidth)
        {
            if (titles == null) return false;

            float charWidth = (float)GuiStyle.SmallishFontSize * 0.62f;
            foreach (var title in titles)
            {
                if (string.IsNullOrWhiteSpace(title)) continue;
                float estimatedWidth = title.Length * charWidth;
                if (estimatedWidth > maxWidth)
                {
                    return false;
                }
            }

            return true;
        }

        private void CloseOpenedDropDown()
        {
            CloseOpenedDropDown(DropDownKey);
            CloseOpenedDropDown(QuestDropDownKey);
        }

        private void CloseOpenedDropDown(string key)
        {
            if (key == DropDownKey && keepEntryListOpen)
            {
                EnsureEntryListOpen();
                return;
            }

            var dropdown = SingleComposer?.GetDropDown(key);
            if (dropdown?.listMenu?.IsOpened == true)
            {
                try
                {
                    MethodInfo closeMethod = dropdown.listMenu.GetType().GetMethod("Close", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    closeMethod?.Invoke(dropdown.listMenu, null);
                }
                catch
                {
                }

                dropdown.listMenu.OnFocusLost();
                dropdown.OnFocusLost();
                SingleComposer?.UnfocusOwnElements();
            }
        }

        public override void OnMouseDown(MouseEvent args)
        {
            bool shouldClose = false;

            var entryDropdown = SingleComposer?.GetDropDown(DropDownKey);
            bool entryClickInsideDropdown = entryDropdown != null && entryDropdown.IsPositionInside(args.X, args.Y);
            bool entryClickInsideListMenu = entryDropdown?.listMenu?.IsOpened == true && entryDropdown.listMenu.Bounds?.PointInside(args.X, args.Y) == true;
            if (!keepEntryListOpen && entryDropdown?.listMenu?.IsOpened == true && !entryClickInsideDropdown && !entryClickInsideListMenu)
            {
                shouldClose = true;
            }

            var questDropdown = SingleComposer?.GetDropDown(QuestDropDownKey);
            bool questClickInsideDropdown = questDropdown != null && questDropdown.IsPositionInside(args.X, args.Y);
            bool questClickInsideListMenu = questDropdown?.listMenu?.IsOpened == true && questDropdown.listMenu.Bounds?.PointInside(args.X, args.Y) == true;
            if (questDropdown?.listMenu?.IsOpened == true && !questClickInsideDropdown && !questClickInsideListMenu)
            {
                shouldClose = true;
            }

            if (shouldClose)
            {
                capi.Event.EnqueueMainThreadTask(CloseOpenedDropDown, "alegacyvsquest-journal-close-dropdown-deferred");
            }
            else
            {
                EnsureEntryListOpen();
            }

            base.OnMouseDown(args);

            if (keepEntryListOpen && entryClickInsideDropdown)
            {
                EnsureEntryListOpen();
            }
        }

        public override void OnMouseMove(MouseEvent args)
        {
            base.OnMouseMove(args);
            EnsureEntryListOpen();
        }

        public override void OnGuiClosed()
        {
            CloseOpenedDropDown();
            base.OnGuiClosed();
        }

        private void EnsureEntryListOpen()
        {
            if (!keepEntryListOpen) return;

            var dropdown = SingleComposer?.GetDropDown(DropDownKey);
            if (dropdown?.listMenu == null) return;

            dropdown.listMenu.Open();
            dropdown.listMenu.HoveredIndex = dropdown.listMenu.SelectedIndex;
        }
    }
}
