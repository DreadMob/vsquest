using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace VsQuest.Gui.Journal
{
    public class BrowseHistoryElement
    {
        public JournalPage Page;
        public string FilterContext;
        public float PosY;
    }

    public class QuestJournalDialog : GuiDialog
    {
        private const double ListHeight = 500.0;
        private const double ListWidth = 500.0;

        private Dictionary<string, int> pageNumberByPageCode = new Dictionary<string, int>();
        private List<JournalPage> allPages = new List<JournalPage>();
        private List<IJournalPage> shownPages = new List<IJournalPage>();
        private List<QuestGiverPage> noteGiverPages = new List<QuestGiverPage>();
        private List<QuestPage> notePages = new List<QuestPage>();
        private Dictionary<string, QuestPage> questPagesByLoreCode = new Dictionary<string, QuestPage>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, QuestPage> notePagesByLoreCode = new Dictionary<string, QuestPage>(StringComparer.OrdinalIgnoreCase);
        private Stack<BrowseHistoryElement> browseHistory = new Stack<BrowseHistoryElement>();

        private string currentSearchText;
        private string currentCategoryCode;
        private string currentQuestGiverFilter;

        private GuiComposer overviewGui;
        private GuiComposer detailViewGui;
        private JournalTab[] tabs;

        private IClientPlayer player;
        private List<QuestJournalEntry> allEntries;

        public override string ToggleKeyCombinationCode => null;
        public override double DrawOrder => 0.2;
        public override bool PrefersUngrabbedMouse => true;

        public QuestJournalDialog(ICoreClientAPI capi) : base(capi)
        {
            player = capi.World.Player;
            LoadEntries();
            InitOverviewGui();
        }

        public void Refresh()
        {
            LoadEntries();
            InitOverviewGui();
            FilterItems();
        }

        private void LoadEntries()
        {
            allPages.Clear();
            pageNumberByPageCode.Clear();
            noteGiverPages.Clear();
            notePages.Clear();
            questPagesByLoreCode.Clear();
            notePagesByLoreCode.Clear();

            if (player?.Entity?.WatchedAttributes == null)
            {
                allEntries = new List<QuestJournalEntry>();
                return;
            }

            allEntries = QuestJournalEntry.Load(player.Entity.WatchedAttributes)
                .Where(e => e != null && !string.IsNullOrWhiteSpace(e.QuestId))
                .ToList();

            bool entriesChanged = false;
            var questSystem = capi?.ModLoader?.GetModSystem<QuestSystem>();
            foreach (var entry in allEntries)
            {
                if (entry == null) continue;

                string originalTitle = entry.Title;
                bool titleWasNote = string.Equals(originalTitle, "note", StringComparison.OrdinalIgnoreCase);
                bool titleWasQuest = string.Equals(originalTitle, "quest", StringComparison.OrdinalIgnoreCase);
                bool titleWasOverwrite = string.Equals(originalTitle, "overwrite", StringComparison.OrdinalIgnoreCase);

                if ((titleWasNote || titleWasQuest || titleWasOverwrite)
                    && entry.Chapters != null
                    && entry.Chapters.Count > 0
                    && !string.IsNullOrWhiteSpace(entry.Chapters[0]))
                {
                    string candidateTitle = entry.Chapters[0];
                    if (candidateTitle.Length <= 120 && !candidateTitle.Contains("\n"))
                    {
                        entry.Title = candidateTitle;
                        entriesChanged = true;
                        if (entry.Chapters.Count > 1)
                        {
                            entry.Chapters.RemoveAt(0);
                            entriesChanged = true;
                        }
                    }

                    if (titleWasNote && !entry.IsNote)
                    {
                        entry.IsNote = true;
                        entriesChanged = true;
                    }
                    else if (titleWasQuest && entry.IsNote)
                    {
                        entry.IsNote = false;
                        entriesChanged = true;
                    }
                }

                if (entry.IsNote && questSystem?.QuestRegistry != null)
                {
                    bool hasQuestById = !string.IsNullOrWhiteSpace(entry.QuestId)
                        && questSystem.QuestRegistry.ContainsKey(entry.QuestId);
                    bool hasQuestByLore = !string.IsNullOrWhiteSpace(entry.LoreCode)
                        && questSystem.QuestRegistry.ContainsKey(entry.LoreCode);
                    if (hasQuestById || hasQuestByLore)
                    {
                        entry.IsNote = false;
                        entriesChanged = true;
                    }
                }
            }

            if (entriesChanged)
            {
                QuestJournalEntry.Save(player.Entity.WatchedAttributes, allEntries);
                player.Entity.WatchedAttributes.MarkPathDirty(QuestJournalEntry.JournalEntriesKey);
            }

            var questEntries = allEntries.Where(e => !IsNoteEntry(e)).ToList();
            var noteEntries = allEntries.Where(e => IsNoteEntry(e)).ToList();

            BuildQuestGiverPages(questEntries);
            BuildQuestPages(questEntries);
            BuildNoteGiverPages(noteEntries);
            BuildNotePages(noteEntries);
            BuildQuestMapPage(questSystem, questEntries);

            for (int i = 0; i < allPages.Count; i++)
            {
                allPages[i].PageNumber = i;
                pageNumberByPageCode[allPages[i].PageCode] = i;
            }
        }

        private void BuildQuestGiverPages(List<QuestJournalEntry> entries)
        {
            var questGivers = entries
                .GroupBy(e => e.QuestId, StringComparer.OrdinalIgnoreCase)
                .Select(g => new { QuestId = g.Key, Count = g.Count() })
                .OrderBy(g => g.QuestId, StringComparer.OrdinalIgnoreCase);

            foreach (var giver in questGivers)
            {
                string title = GetQuestGiverTitle(giver.QuestId);
                allPages.Add(new QuestGiverPage(capi, giver.QuestId, title, giver.Count));
            }
        }

        private void BuildQuestPages(List<QuestJournalEntry> entries)
        {
            foreach (var entry in entries)
            {
                string entryTitle = GetEntryTitle(entry);
                var page = new QuestPage(capi, entry.QuestId, entry.LoreCode, entryTitle);
                allPages.Add(page);
                if (!string.IsNullOrWhiteSpace(entry?.LoreCode))
                {
                    questPagesByLoreCode[entry.LoreCode] = page;
                }
            }
        }

        private void BuildNoteGiverPages(List<QuestJournalEntry> entries)
        {
            var noteGivers = entries
                .GroupBy(e => e.QuestId, StringComparer.OrdinalIgnoreCase)
                .Select(g => new { QuestId = g.Key, Count = g.Count() })
                .OrderBy(g => g.QuestId, StringComparer.OrdinalIgnoreCase);

            foreach (var giver in noteGivers)
            {
                string title = GetQuestGiverTitle(giver.QuestId);
                noteGiverPages.Add(new QuestGiverPage(capi, giver.QuestId, title, giver.Count));
            }
        }

        private void BuildNotePages(List<QuestJournalEntry> entries)
        {
            foreach (var entry in entries)
            {
                string entryTitle = GetEntryTitle(entry);
                var page = new QuestPage(capi, entry.QuestId, entry.LoreCode, entryTitle);
                notePages.Add(page);
                if (!string.IsNullOrWhiteSpace(entry?.LoreCode))
                {
                    notePagesByLoreCode[entry.LoreCode] = page;
                }
            }
        }

        private void BuildQuestMapPage(QuestSystem questSystem, List<QuestJournalEntry> entries)
        {
            if (questSystem?.QuestRegistry == null || questSystem.QuestRegistry.Count == 0)
            {
                return;
            }

            var quests = questSystem.QuestRegistry.Values
                .Where(q => q != null && !string.IsNullOrWhiteSpace(q.id) && !IsDebuggingQuestId(q.id))
                .OrderBy(q => q.id, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (quests.Count == 0) return;

            var completed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (player is IPlayer iPlayer)
            {
                completed = new HashSet<string>(questSystem.GetNormalizedCompletedQuestIds(iPlayer), StringComparer.OrdinalIgnoreCase);
            }

            var activeQuestIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var active = questSystem.GetPlayerQuests(player.PlayerUID);
                if (active != null)
                {
                    foreach (var aq in active)
                    {
                        if (!string.IsNullOrWhiteSpace(aq?.questId))
                        {
                            activeQuestIds.Add(aq.questId);
                        }
                    }
                }
            }
            catch
            {
            }

            string mapTitle = Lang.Get("alegacyvsquest:journal-map-title");
            var questGiverByQuestId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (entries != null)
            {
                foreach (var entry in entries)
                {
                    string questId = entry?.LoreCode ?? entry?.QuestId;
                    if (string.IsNullOrWhiteSpace(questId) || string.IsNullOrWhiteSpace(entry?.QuestId)) continue;
                    string normalized = questSystem.NormalizeQuestId(questId);
                    if (!questGiverByQuestId.ContainsKey(normalized))
                    {
                        questGiverByQuestId[normalized] = entry.QuestId;
                    }
                }
            }

            var questIdsByNpc = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in questGiverByQuestId)
            {
                string giverId = pair.Value;
                if (string.IsNullOrWhiteSpace(giverId)) continue;
                if (!questIdsByNpc.TryGetValue(giverId, out var questIds))
                {
                    questIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    questIdsByNpc[giverId] = questIds;
                }
                questIds.Add(pair.Key);
            }

            var npcNodes = new List<QuestMapNode>();
            var npcIds = questIdsByNpc.Keys.OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToList();
            if (npcIds.Count > 0)
            {
                for (int i = 0; i < npcIds.Count; i++)
                {
                    string npcId = npcIds[i];
                    float x = (float)((i + 1.0) / (npcIds.Count + 1.0));
                    npcNodes.Add(new QuestMapNode
                    {
                        QuestId = "npc-" + npcId,
                        TargetId = npcId,
                        Title = GetQuestGiverTitle(npcId),
                        X = x,
                        Y = 0.12f,
                        Status = QuestMapNodeStatus.Available,
                        Kind = QuestMapNodeKind.Npc
                    });
                }
            }

            int columns = Math.Min(4, Math.Max(1, (int)Math.Ceiling(Math.Sqrt(quests.Count))));
            int rows = (int)Math.Ceiling(quests.Count / (double)columns);
            if (rows <= 0) rows = 1;

            float questYStart = npcNodes.Count > 0 ? 0.25f : 0.1f;
            float questYSpan = npcNodes.Count > 0 ? 0.7f : 0.8f;

            var questNodes = new List<QuestMapNode>();
            var edges = new List<(string FromId, string ToId)>();

            for (int i = 0; i < quests.Count; i++)
            {
                int col = i % columns;
                int row = i / columns;

                float x = (float)((col + 0.5) / columns);
                float y = questYStart + (float)((row + 0.5) / rows * questYSpan);

                var quest = quests[i];
                string title = Lang.Get(quest.id + "-title");
                if (title == quest.id + "-title")
                {
                    title = quest.id;
                }

                QuestMapNodeStatus status = QuestMapNodeStatus.Locked;
                if (completed.Contains(quest.id))
                {
                    status = QuestMapNodeStatus.Completed;
                }
                else if (activeQuestIds.Contains(quest.id))
                {
                    status = QuestMapNodeStatus.Current;
                }
                else if (PredecessorsCompleted(questSystem, quest, completed))
                {
                    status = QuestMapNodeStatus.Available;
                }

                questNodes.Add(new QuestMapNode
                {
                    QuestId = quest.id,
                    TargetId = quest.id,
                    Title = title,
                    X = x,
                    Y = y,
                    Status = status,
                    Kind = QuestMapNodeKind.Quest
                });
            }

            foreach (var quest in quests)
            {
                if (quest == null || string.IsNullOrWhiteSpace(quest.id)) continue;

                IEnumerable<string> predecessors = Enumerable.Empty<string>();
                if (!string.IsNullOrWhiteSpace(quest.predecessor))
                {
                    predecessors = predecessors.Concat(new[] { quest.predecessor });
                }
                if (quest.predecessors != null && quest.predecessors.Count > 0)
                {
                    predecessors = predecessors.Concat(quest.predecessors);
                }

                foreach (var pred in predecessors)
                {
                    if (string.IsNullOrWhiteSpace(pred)) continue;
                    string normalized = questSystem.NormalizeQuestId(pred);
                    edges.Add((normalized, quest.id));
                }
            }

            var mapPage = new QuestProgressMapPage(capi, mapTitle, npcNodes, questNodes, edges, questIdsByNpc);
            allPages.Add(mapPage);
        }

        private static bool IsDebuggingQuestId(string questId)
        {
            if (string.IsNullOrWhiteSpace(questId)) return true;
            return questId.StartsWith("vsquestdebugging", StringComparison.OrdinalIgnoreCase)
                || questId.StartsWith("debugging", StringComparison.OrdinalIgnoreCase);
        }

        private static bool PredecessorsCompleted(QuestSystem questSystem, Quest quest, HashSet<string> completed)
        {
            if (quest == null) return false;
            if (completed == null) return false;

            if (!string.IsNullOrWhiteSpace(quest.predecessor))
            {
                string pred = questSystem.NormalizeQuestId(quest.predecessor);
                if (!completed.Contains(pred)) return false;
            }

            if (quest.predecessors != null)
            {
                foreach (var pred in quest.predecessors)
                {
                    if (string.IsNullOrWhiteSpace(pred)) continue;
                    string normalized = questSystem.NormalizeQuestId(pred);
                    if (!completed.Contains(normalized)) return false;
                }
            }

            return true;
        }

        private string GetQuestGiverTitle(string questId)
        {
            if (string.IsNullOrWhiteSpace(questId)) return "";
            string title = Lang.Get(questId + "-title");
            if (title == questId + "-title")
            {
                int colonIndex = questId.LastIndexOf(':');
                if (colonIndex > 0 && colonIndex < questId.Length - 1)
                {
                    return questId.Substring(colonIndex + 1);
                }
                return questId;
            }
            return title;
        }

        private string GetEntryTitle(QuestJournalEntry entry)
        {
            if (entry == null) return "";
            if (!string.IsNullOrWhiteSpace(entry.Title)) return entry.Title;
            return entry.LoreCode ?? "";
        }

        private bool IsNoteEntry(QuestJournalEntry entry)
        {
            if (entry == null) return false;
            return entry.IsNote;
        }

        private QuestPage GetEntryPageForEntry(QuestJournalEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.LoreCode)) return null;

            if (IsNoteEntry(entry) && notePagesByLoreCode.TryGetValue(entry.LoreCode, out var notePage))
            {
                return notePage;
            }

            if (questPagesByLoreCode.TryGetValue(entry.LoreCode, out var questPage))
            {
                return questPage;
            }

            return null;
        }

        private IEnumerable<QuestPage> GetAllEntryPagesInRecentOrder()
        {
            if (allEntries == null) yield break;

            for (int i = allEntries.Count - 1; i >= 0; i--)
            {
                var page = GetEntryPageForEntry(allEntries[i]);
                if (page != null)
                {
                    yield return page;
                }
            }
        }

        private JournalTab[] GenerateTabs(out int currentTabIndex)
        {
            currentTabIndex = 0;
            var tabList = new List<JournalTab>
            {
                new JournalTab { Name = Lang.Get("alegacyvsquest:tab-all"), CategoryCode = "all" },
                new JournalTab { Name = Lang.Get("alegacyvsquest:tab-quests"), CategoryCode = "quests" },
                new JournalTab { Name = Lang.Get("alegacyvsquest:tab-notes"), CategoryCode = "notes" },
                new JournalTab { Name = Lang.Get("alegacyvsquest:tab-map"), CategoryCode = "map" }
            };

            for (int i = 0; i < tabList.Count; i++)
            {
                if (tabList[i].CategoryCode == currentCategoryCode)
                {
                    currentTabIndex = i;
                    break;
                }
            }

            return tabList.ToArray();
        }

        private void InitOverviewGui()
        {
            ElementBounds searchFieldBounds = ElementBounds.Fixed(GuiStyle.ElementToDialogPadding - 2.0, 45.0, 300.0, 30.0);
            ElementBounds stackListBounds = ElementBounds.Fixed(0.0, 0.0, ListWidth, ListHeight).FixedUnder(searchFieldBounds, 5.0);
            ElementBounds clipBounds = stackListBounds.ForkBoundingParent();
            ElementBounds insetBounds = stackListBounds.FlatCopy().FixedGrow(6.0).WithFixedOffset(-3.0, -3.0);
            ElementBounds scrollbarBounds = insetBounds.CopyOffsetedSibling(3.0 + stackListBounds.fixedWidth + 7.0).WithFixedWidth(20.0);

            ElementBounds closeButtonBounds = ElementBounds.FixedSize(0.0, 0.0).FixedUnder(clipBounds, 18.0)
                .WithAlignment(EnumDialogArea.RightFixed)
                .WithFixedPadding(20.0, 4.0)
                .WithFixedAlignmentOffset(2.0, 0.0);

            ElementBounds backButtonBounds = ElementBounds.FixedSize(0.0, 0.0).FixedUnder(clipBounds, 15.0)
                .WithAlignment(EnumDialogArea.LeftFixed)
                .WithFixedPadding(20.0, 4.0)
                .WithFixedAlignmentOffset(-6.0, 3.0);

            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(insetBounds, stackListBounds, scrollbarBounds, closeButtonBounds);

            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
                .WithAlignment(EnumDialogArea.None)
                .WithAlignment(EnumDialogArea.CenterFixed)
                .WithFixedPosition(0.0, 70.0);

            ElementBounds tabBounds = ElementBounds.Fixed(-200.0, 35.0, 200.0, 545.0);

            tabs = GenerateTabs(out int curTab);

            overviewGui = capi.Gui.CreateCompo("questjournal-overview", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(Lang.Get("alegacyvsquest:journal-title"), OnTitleBarClose)
                .AddVerticalTabs(tabs, tabBounds, OnTabClicked, "verticalTabs")
                .AddTextInput(searchFieldBounds, FilterItemsBySearchText, CairoFont.WhiteSmallishText(), "searchField")
                .BeginChildElements(bgBounds)
                .BeginClip(clipBounds)
                .AddInset(insetBounds, 3)
                .AddInteractiveElement(new JournalFlatList(capi, stackListBounds, OnLeftClickListElement, shownPages), "stacklist")
                .EndClip()
                .AddVerticalScrollbar(OnNewScrollbarValueOverviewPage, scrollbarBounds, "scrollbar")
                .AddSmallButton(Lang.Get("general-back"), OnButtonBack, backButtonBounds, EnumButtonStyle.Normal, "backButton")
                .AddSmallButton(Lang.Get("alegacyvsquest:button-close-journal"), OnButtonClose, closeButtonBounds)
                .EndChildElements()
                .Compose();

            UpdateOverviewScrollbar();

            overviewGui.GetTextInput("searchField").SetPlaceHolderText(Lang.Get("alegacyvsquest:search-placeholder"));
            overviewGui.GetVerticalTab("verticalTabs").SetValue(curTab, triggerHandler: false);
            overviewGui.FocusElement(overviewGui.GetTextInput("searchField").TabIndex);

            if (string.IsNullOrEmpty(currentCategoryCode))
            {
                currentCategoryCode = "all";
            }
        }

        private void InitDetailGui()
        {
            ElementBounds textBounds = ElementBounds.Fixed(11.5, 45.0, ListWidth - 3.5, 30.0 + ListHeight + 17.0);
            ElementBounds clipBounds = textBounds.ForkBoundingParent();
            ElementBounds insetBounds = textBounds.FlatCopy().FixedGrow(6.0).WithFixedOffset(-3.0, -3.0);
            ElementBounds scrollbarBounds = clipBounds.CopyOffsetedSibling(textBounds.fixedWidth + 7.0, -6.0, 0.0, 6.0).WithFixedWidth(20.0);

            ElementBounds closeButtonBounds = ElementBounds.FixedSize(0.0, 0.0).FixedUnder(clipBounds, 15.0)
                .WithAlignment(EnumDialogArea.RightFixed)
                .WithFixedPadding(20.0, 4.0)
                .WithFixedAlignmentOffset(-11.0, 1.0);

            ElementBounds backButtonBounds = ElementBounds.FixedSize(0.0, 0.0).FixedUnder(clipBounds, 15.0)
                .WithAlignment(EnumDialogArea.LeftFixed)
                .WithFixedPadding(20.0, 4.0)
                .WithFixedAlignmentOffset(4.0, 1.0);

            ElementBounds overviewButtonBounds = ElementBounds.FixedSize(0.0, 0.0).FixedUnder(clipBounds, 15.0)
                .WithAlignment(EnumDialogArea.CenterFixed)
                .WithFixedPadding(20.0, 4.0)
                .WithFixedAlignmentOffset(0.0, 1.0);

            ElementBounds bgBounds = insetBounds.ForkBoundingParent(5.0, 40.0, 36.0, 52.0)
                .WithFixedPadding(GuiStyle.ElementToDialogPadding / 2.0);
            bgBounds.WithChildren(insetBounds, textBounds, scrollbarBounds, backButtonBounds, closeButtonBounds);

            ElementBounds dialogBounds = bgBounds.ForkBoundingParent()
                .WithAlignment(EnumDialogArea.None)
                .WithAlignment(EnumDialogArea.CenterFixed)
                .WithFixedPosition(0.0, 70.0);

            ElementBounds tabBounds = ElementBounds.Fixed(-200.0, 35.0, 200.0, 545.0);

            BrowseHistoryElement curPage = browseHistory.Peek();
            float posY = curPage.PosY;

            detailViewGui?.Dispose();
            detailViewGui = capi.Gui.CreateCompo("questjournal-detail", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(Lang.Get("alegacyvsquest:journal-title"), OnTitleBarClose)
                .AddVerticalTabs(GenerateTabs(out int curTab), tabBounds, OnDetailViewTabClicked, "verticalTabs")
                .BeginChildElements(bgBounds)
                .BeginClip(clipBounds)
                .AddInset(insetBounds, 3);

            curPage.Page.ComposePage(detailViewGui, textBounds, OpenDetailPageFor);
            GuiElement lastAddedElement = detailViewGui.LastAddedElement;

            detailViewGui.EndClip()
                .AddVerticalScrollbar(OnNewScrollbarValueDetailPage, scrollbarBounds, "scrollbar")
                .AddSmallButton(Lang.Get("general-back"), OnButtonBack, backButtonBounds)
                .AddSmallButton(Lang.Get("alegacyvsquest:button-overview"), OnButtonOverview, overviewButtonBounds)
                .AddSmallButton(Lang.Get("general-close"), OnButtonClose, closeButtonBounds)
                .EndChildElements()
                .Compose();

            detailViewGui.GetScrollbar("scrollbar").SetHeights((float)ListHeight, (float)lastAddedElement.Bounds.fixedHeight);
            detailViewGui.GetScrollbar("scrollbar").CurrentYPosition = posY;
            OnNewScrollbarValueDetailPage(posY);
            detailViewGui.GetVerticalTab("verticalTabs").SetValue(curTab, triggerHandler: false);
        }

        private void UpdateOverviewScrollbar()
        {
            var stacklist = overviewGui?.GetElement("stacklist") as JournalFlatList;
            if (stacklist != null)
            {
                stacklist.CalcTotalHeight();
                overviewGui.GetScrollbar("scrollbar").SetHeights((float)ListHeight, (float)stacklist.insideBounds.fixedHeight);
            }
        }

        private void OnTabClicked(int index, GuiTab tab)
        {
            var journalTab = tab as JournalTab;
            currentCategoryCode = journalTab?.CategoryCode ?? "all";
            currentQuestGiverFilter = null;
            browseHistory.Clear();
            if (currentCategoryCode == "map")
            {
                OpenMapPage();
                return;
            }
            FilterItems();
        }

        private void OnDetailViewTabClicked(int index, GuiTab tab)
        {
            browseHistory.Clear();
            OnTabClicked(index, tab);
        }

        private void OnLeftClickListElement(int index)
        {
            if (index < 0 || index >= shownPages.Count) return;

            var page = shownPages[index] as JournalPage;
            if (page == null) return;

            if (page is QuestGiverPage giverPage)
            {
                currentQuestGiverFilter = giverPage.QuestGiverId;
                browseHistory.Push(new BrowseHistoryElement { Page = giverPage, FilterContext = currentQuestGiverFilter });
                FilterItems();
                return;
            }

            if (page is QuestPage questPage)
            {
                var entry = allEntries.FirstOrDefault(e =>
                    string.Equals(e.LoreCode, questPage.QuestId, StringComparison.OrdinalIgnoreCase));

                if (entry != null)
                {
                    var entryPage = new QuestEntryPage(capi, entry.QuestId, GetEntryTitle(entry), entry.Chapters);
                    browseHistory.Push(new BrowseHistoryElement { Page = entryPage });
                    InitDetailGui();
                }
                return;
            }

            if (page is QuestProgressMapPage mapPage)
            {
                browseHistory.Push(new BrowseHistoryElement { Page = mapPage });
                InitDetailGui();
                return;
            }
        }

        private bool OpenDetailPageFor(string pageCode)
        {
            if (string.IsNullOrWhiteSpace(pageCode)) return false;
            const string entryPrefix = "entry-";

            if (pageCode.StartsWith(entryPrefix, StringComparison.OrdinalIgnoreCase))
            {
                string questId = pageCode.Substring(entryPrefix.Length);
                if (string.IsNullOrWhiteSpace(questId)) return false;

                var entry = allEntries.FirstOrDefault(e =>
                    string.Equals(e.QuestId, questId, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(e.LoreCode, questId, StringComparison.OrdinalIgnoreCase));

                if (entry != null)
                {
                    var entryPage = new QuestEntryPage(capi, entry.QuestId, GetEntryTitle(entry), entry.Chapters);
                    browseHistory.Push(new BrowseHistoryElement { Page = entryPage });
                    InitDetailGui();
                    return true;
                }
            }

            return false;
        }

        private void FilterItemsBySearchText(string text)
        {
            if (currentSearchText != text)
            {
                currentSearchText = text;
                FilterItems();
            }
        }

        private void FilterItems()
        {
            shownPages.Clear();
            string searchLower = currentSearchText?.ToLowerInvariant()?.Trim() ?? "";

            if (currentCategoryCode == "notes")
            {
                if (string.IsNullOrEmpty(currentQuestGiverFilter))
                {
                    foreach (var page in noteGiverPages)
                    {
                        if (string.IsNullOrEmpty(searchLower) || page.GetTextMatchWeight(searchLower) > 0)
                        {
                            shownPages.Add(page);
                            continue;
                        }

                        bool hasMatchingNote = notePages.Any(notePage =>
                            string.Equals(notePage.QuestGiverId, page.QuestGiverId, StringComparison.OrdinalIgnoreCase)
                            && notePage.GetTextMatchWeight(searchLower) > 0);

                        if (hasMatchingNote)
                        {
                            shownPages.Add(page);
                        }
                    }
                }
                else
                {
                    foreach (var page in notePages)
                    {
                        if (string.Equals(page.QuestGiverId, currentQuestGiverFilter, StringComparison.OrdinalIgnoreCase))
                        {
                            if (string.IsNullOrEmpty(searchLower) || page.GetTextMatchWeight(searchLower) > 0)
                            {
                                shownPages.Add(page);
                            }
                        }
                    }
                }
            }
            else if (currentCategoryCode == "all")
            {
                foreach (var page in GetAllEntryPagesInRecentOrder())
                {
                    if (string.IsNullOrEmpty(searchLower) || page.GetTextMatchWeight(searchLower) > 0)
                    {
                        shownPages.Add(page);
                    }
                }
            }
            else if (currentCategoryCode == "quests")
            {
                if (string.IsNullOrEmpty(currentQuestGiverFilter))
                {
                    foreach (var page in allPages.OfType<QuestGiverPage>())
                    {
                        if (string.IsNullOrEmpty(searchLower) || page.GetTextMatchWeight(searchLower) > 0)
                        {
                            shownPages.Add(page);
                            continue;
                        }

                        bool hasMatchingQuest = allPages.OfType<QuestPage>().Any(questPage =>
                            string.Equals(questPage.QuestGiverId, page.QuestGiverId, StringComparison.OrdinalIgnoreCase)
                            && questPage.GetTextMatchWeight(searchLower) > 0);

                        if (hasMatchingQuest)
                        {
                            shownPages.Add(page);
                        }
                    }
                }
                else
                {
                    foreach (var page in allPages.OfType<QuestPage>())
                    {
                        if (string.Equals(page.QuestGiverId, currentQuestGiverFilter, StringComparison.OrdinalIgnoreCase))
                        {
                            if (string.IsNullOrEmpty(searchLower) || page.GetTextMatchWeight(searchLower) > 0)
                            {
                                shownPages.Add(page);
                            }
                        }
                    }
                }
            }
            else if (currentCategoryCode == "map")
            {
                // Map opens directly; nothing to list here.
            }

            var stacklist = overviewGui?.GetElement("stacklist") as JournalFlatList;
            if (stacklist != null)
            {
                stacklist.Elements = shownPages;
                stacklist.CalcTotalHeight();
            }

            UpdateOverviewScrollbar();
        }

        private void OnNewScrollbarValueOverviewPage(float value)
        {
            var flatList = overviewGui?.GetElement("stacklist") as JournalFlatList;
            if (flatList != null)
            {
                flatList.insideBounds.fixedY = 3f - value;
                flatList.insideBounds.CalcWorldBounds();
            }
        }

        private void OnNewScrollbarValueDetailPage(float value)
        {
            var richtext = detailViewGui?.GetRichtext("richtext");
            if (richtext != null)
            {
                richtext.Bounds.fixedY = 3f - value;
                richtext.Bounds.CalcWorldBounds();
            }
            if (browseHistory.Count > 0)
            {
                browseHistory.Peek().PosY = detailViewGui?.GetScrollbar("scrollbar")?.CurrentYPosition ?? 0f;
            }
        }

        private void OnTitleBarClose()
        {
            TryClose();
        }

        private bool OnButtonClose()
        {
            TryClose();
            return true;
        }

        private bool OnButtonBack()
        {
            if (browseHistory.Count == 0)
            {
                return true;
            }

            browseHistory.Pop();

            if (browseHistory.Count > 0)
            {
                var prev = browseHistory.Peek();
                if (prev.Page is QuestGiverPage)
                {
                    currentQuestGiverFilter = prev.FilterContext;
                    FilterItems();
                }
                else
                {
                    InitDetailGui();
                }
            }
            else
            {
                currentQuestGiverFilter = null;
                FilterItems();
            }

            return true;
        }

        private bool OnButtonOverview()
        {
            browseHistory.Clear();
            currentQuestGiverFilter = null;
            FilterItems();
            return true;
        }

        private void OpenMapPage()
        {
            var mapPage = allPages.OfType<QuestProgressMapPage>().FirstOrDefault();
            if (mapPage == null)
            {
                FilterItems();
                return;
            }

            browseHistory.Clear();
            browseHistory.Push(new BrowseHistoryElement { Page = mapPage });
            InitDetailGui();
        }

        public override void OnRenderGUI(float deltaTime)
        {
            if (browseHistory.Count == 0 || browseHistory.Peek().Page is QuestGiverPage)
            {
                SingleComposer = overviewGui;
            }
            else
            {
                SingleComposer = detailViewGui;
            }

            if (SingleComposer == overviewGui)
            {
                var backButton = overviewGui.GetButton("backButton");
                if (backButton != null)
                {
                    backButton.Enabled = browseHistory.Count > 0;
                }
            }

            base.OnRenderGUI(deltaTime);
        }

        public override void OnGuiOpened()
        {
            InitOverviewGui();
            FilterItems();
            base.OnGuiOpened();
        }

        public override void OnGuiClosed()
        {
            browseHistory.Clear();
            currentQuestGiverFilter = null;
            var searchField = overviewGui?.GetTextInput("searchField");
            if (searchField != null)
            {
                searchField.SetValue("");
            }
            currentSearchText = "";
            base.OnGuiClosed();
        }

        public override bool CaptureAllInputs()
        {
            return false;
        }

        public override void Dispose()
        {
            foreach (var page in allPages)
            {
                page?.Dispose();
            }
            overviewGui?.Dispose();
            detailViewGui?.Dispose();
        }
    }
}
