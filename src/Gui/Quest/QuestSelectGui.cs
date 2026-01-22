using System;
using System.Collections.Generic;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace VsQuest
{
    public class QuestSelectGui : GuiDialog
    {
        public override string ToggleKeyCombinationCode => null;

        private const string DropDownKey = "questdropdown";

        private bool recomposeQueued;

        private long questGiverId;
        private string selectedAvailableQuestId;
        private ActiveQuest selectedActiveQuest;
        private string selectedActiveQuestKey;

        private List<string> availableQuestIds;
        private List<ActiveQuest> activeQuests;
        private IClientPlayer player;
        private string noAvailableQuestDescLangKey;
        private string noAvailableQuestCooldownDescLangKey;
        private int noAvailableQuestCooldownDaysLeft;
        private int noAvailableQuestRotationDaysLeft;

        private string reputationNpcId;
        private string reputationFactionId;
        private int reputationNpcValue;
        private int reputationFactionValue;
        private string reputationNpcRankLangKey;
        private string reputationFactionRankLangKey;
        private string reputationNpcTitleLangKey;
        private string reputationFactionTitleLangKey;
        private bool reputationNpcHasRewards;
        private bool reputationFactionHasRewards;
        private int reputationNpcRewardsCount;
        private int reputationFactionRewardsCount;

        private int curTab = 0;
        private bool closeGuiAfterAcceptingAndCompleting;
        public QuestSelectGui(ICoreClientAPI capi, long questGiverId, List<string> availableQuestIds, List<ActiveQuest> activeQuests, QuestConfig questConfig, string noAvailableQuestDescLangKey = null, string noAvailableQuestCooldownDescLangKey = null, int noAvailableQuestCooldownDaysLeft = 0, int noAvailableQuestRotationDaysLeft = 0, string reputationNpcId = null, string reputationFactionId = null, int reputationNpcValue = 0, int reputationFactionValue = 0, string reputationNpcRankLangKey = null, string reputationFactionRankLangKey = null, string reputationNpcTitleLangKey = null, string reputationFactionTitleLangKey = null, bool reputationNpcHasRewards = false, bool reputationFactionHasRewards = false, int reputationNpcRewardsCount = 0, int reputationFactionRewardsCount = 0) : base(capi)
        {
            player = capi.World.Player;
            closeGuiAfterAcceptingAndCompleting = questConfig.CloseGuiAfterAcceptingAndCompleting;
            ApplyData(questGiverId, availableQuestIds, activeQuests, noAvailableQuestDescLangKey, noAvailableQuestCooldownDescLangKey, noAvailableQuestCooldownDaysLeft, noAvailableQuestRotationDaysLeft, reputationNpcId, reputationFactionId, reputationNpcValue, reputationFactionValue, reputationNpcRankLangKey, reputationFactionRankLangKey, reputationNpcTitleLangKey, reputationFactionTitleLangKey, reputationNpcHasRewards, reputationFactionHasRewards, reputationNpcRewardsCount, reputationFactionRewardsCount);
            RequestRecompose();
        }

        private static string ActiveQuestKey(ActiveQuest quest)
        {
            if (quest == null) return null;
            return $"{quest.questGiverId}:{quest.questId}";
        }

        private void RequestRecompose()
        {
            if (recomposeQueued) return;
            recomposeQueued = true;

            capi.Event.EnqueueMainThreadTask(() =>
            {
                recomposeQueued = false;
                recompose();
            }, "alegacyvsquest-recompose");
        }

        private void CloseOpenedDropDownDeferred()
        {
            capi.Event.EnqueueMainThreadTask(() =>
            {
                CloseOpenedDropDown();
            }, "alegacyvsquest-closedropdown");
        }

        private void ApplyData(long questGiverId, List<string> availableQuestIds, List<ActiveQuest> activeQuests, string noAvailableQuestDescLangKey, string noAvailableQuestCooldownDescLangKey, int noAvailableQuestCooldownDaysLeft, int noAvailableQuestRotationDaysLeft, string reputationNpcId, string reputationFactionId, int reputationNpcValue, int reputationFactionValue, string reputationNpcRankLangKey, string reputationFactionRankLangKey, string reputationNpcTitleLangKey, string reputationFactionTitleLangKey, bool reputationNpcHasRewards, bool reputationFactionHasRewards, int reputationNpcRewardsCount, int reputationFactionRewardsCount)
        {
            this.questGiverId = questGiverId;
            this.availableQuestIds = availableQuestIds;
            this.activeQuests = activeQuests;

            this.noAvailableQuestDescLangKey = noAvailableQuestDescLangKey;
            this.noAvailableQuestCooldownDescLangKey = noAvailableQuestCooldownDescLangKey;
            this.noAvailableQuestCooldownDaysLeft = noAvailableQuestCooldownDaysLeft;
            this.noAvailableQuestRotationDaysLeft = noAvailableQuestRotationDaysLeft;
            this.reputationNpcId = reputationNpcId;
            this.reputationFactionId = reputationFactionId;
            this.reputationNpcValue = reputationNpcValue;
            this.reputationFactionValue = reputationFactionValue;
            this.reputationNpcRankLangKey = reputationNpcRankLangKey;
            this.reputationFactionRankLangKey = reputationFactionRankLangKey;
            this.reputationNpcTitleLangKey = reputationNpcTitleLangKey;
            this.reputationFactionTitleLangKey = reputationFactionTitleLangKey;
            this.reputationNpcHasRewards = reputationNpcHasRewards;
            this.reputationFactionHasRewards = reputationFactionHasRewards;
            this.reputationNpcRewardsCount = reputationNpcRewardsCount;
            this.reputationFactionRewardsCount = reputationFactionRewardsCount;

            if (activeQuests != null && activeQuests.Count > 0)
            {
                if (!string.IsNullOrEmpty(selectedActiveQuestKey))
                {
                    selectedActiveQuest = activeQuests.Find(q => ActiveQuestKey(q) == selectedActiveQuestKey);
                }

                if (selectedActiveQuest == null)
                {
                    selectedActiveQuest = activeQuests[0];
                }

                selectedActiveQuestKey = ActiveQuestKey(selectedActiveQuest);
            }
            else
            {
                selectedActiveQuest = null;
                selectedActiveQuestKey = null;
            }

            // Preserve the currently selected tab when updating data.
            // Only switch tabs if the current tab has no content.
            bool hasAvailable = availableQuestIds != null && availableQuestIds.Count > 0;
            bool hasActive = activeQuests != null && activeQuests.Count > 0;
            bool hasReputation = !string.IsNullOrWhiteSpace(reputationNpcId) || !string.IsNullOrWhiteSpace(reputationFactionId);

            if (curTab == 0 && !hasAvailable && hasActive)
            {
                curTab = 1;
            }
            else if (curTab == 1 && !hasActive && hasAvailable)
            {
                curTab = 0;
            }
            else if (curTab == 2 && !hasReputation)
            {
                curTab = hasAvailable ? 0 : (hasActive ? 1 : 0);
            }
            else if (curTab != 0 && curTab != 1 && curTab != 2)
            {
                // Initial state fallback
                curTab = hasAvailable ? 0 : (hasActive ? 1 : (hasReputation ? 2 : 0));
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
            ElementBounds questTextBounds = ElementBounds.Fixed(0, 60, 400, 500);
            ElementBounds scrollbarBounds = questTextBounds.CopyOffsetedSibling(questTextBounds.fixedWidth + 10).WithFixedWidth(20).WithFixedHeight(questTextBounds.fixedHeight);
            ElementBounds clippingBounds = questTextBounds.ForkBoundingParent();
            ElementBounds bottomLeftButtonBounds = ElementBounds.Fixed(10, 570, 200, 20);
            ElementBounds bottomRightButtonBounds = ElementBounds.Fixed(220, 570, 200, 20);
            ElementBounds bottomRightSecondaryButtonBounds = ElementBounds.Fixed(220, 545, 200, 20);

            GuiTab[] tabs = new GuiTab[] {
                new GuiTab() { Name = Lang.Get("alegacyvsquest:tab-available-quests"), DataInt = 0 },
                new GuiTab() { Name = Lang.Get("alegacyvsquest:tab-active-quests"), DataInt = 1 },
                new GuiTab() { Name = Lang.Get("alegacyvsquest:tab-reputation"), DataInt = 2 }
            };

            bgBounds.BothSizing = ElementSizing.FitToChildren;
            SingleComposer = capi.Gui.CreateCompo("QuestSelectDialog-", dialogBounds)
                            .AddShadedDialogBG(bgBounds)
                            .AddDialogTitleBar(Lang.Get("alegacyvsquest:quest-select-title"), () => TryClose())
                            .AddVerticalTabs(tabs, ElementBounds.Fixed(-200, 35, 200, 260), OnTabClicked, "tabs")
                            .BeginChildElements(bgBounds);

            // GuiElementVerticalTabs constructor forces tabs[0].Active = true.
            // Force the correct active tab for visual highlight.
            SingleComposer.GetVerticalTab("tabs").SetValue(curTab, false);

            if (curTab == 0)
            {
                if (availableQuestIds != null && availableQuestIds.Count > 0)
                {
                    if (string.IsNullOrEmpty(selectedAvailableQuestId) || !availableQuestIds.Contains(selectedAvailableQuestId))
                    {
                        selectedAvailableQuestId = availableQuestIds[0];
                    }

                    int selectedIndex = Math.Max(0, availableQuestIds.IndexOf(selectedAvailableQuestId));

                    SingleComposer.AddDropDown(availableQuestIds.ToArray(), availableQuestIds.ConvertAll<string>(id => Lang.Get(id + "-title")).ToArray(), selectedIndex, onAvailableQuestSelectionChanged, ElementBounds.FixedOffseted(EnumDialogArea.RightTop, 0, 20, 400, 30), DropDownKey)
                        .AddButton(Lang.Get("alegacyvsquest:button-cancel"), TryClose, bottomLeftButtonBounds)
                        .AddButton(Lang.Get("alegacyvsquest:button-accept"), acceptQuest, bottomRightButtonBounds)
                        .BeginClip(clippingBounds)
                            .AddRichtext(questText(selectedAvailableQuestId), CairoFont.WhiteSmallishText(), questTextBounds, "questtext")
                        .EndClip()
                        .AddVerticalScrollbar(OnNewScrollbarvalue, scrollbarBounds, "scrollbar");
                }
                else
                {
                    string noQuestText = (noAvailableQuestCooldownDaysLeft > 0 && !string.IsNullOrEmpty(noAvailableQuestCooldownDescLangKey))
                        ? LocalizationUtils.GetSafe(noAvailableQuestCooldownDescLangKey, noAvailableQuestCooldownDaysLeft, noAvailableQuestRotationDaysLeft)
                        : LocalizationUtils.GetFallback(noAvailableQuestDescLangKey, "alegacyvsquest:no-quest-available-desc");

                    SingleComposer.AddStaticText(noQuestText, CairoFont.WhiteSmallishText(), ElementBounds.Fixed(0, 60, 400, 500))
                        .AddButton(Lang.Get("alegacyvsquest:button-cancel"), TryClose, ElementBounds.FixedOffseted(EnumDialogArea.CenterBottom, 0, -10, 200, 20));
                }
            }
            else if (curTab == 1)
            {
                if (activeQuests != null && activeQuests.Count > 0)
                {
                    if (selectedActiveQuest == null || string.IsNullOrEmpty(selectedActiveQuestKey) || activeQuests.FindIndex(match => ActiveQuestKey(match) == selectedActiveQuestKey) < 0)
                    {
                        selectedActiveQuest = activeQuests[0];
                        selectedActiveQuestKey = ActiveQuestKey(selectedActiveQuest);
                    }

                    int selected = Math.Max(0, activeQuests.FindIndex(match => ActiveQuestKey(match) == selectedActiveQuestKey));

                    string[] activeQuestKeys = activeQuests.ConvertAll<string>(quest => ActiveQuestKey(quest)).ToArray();

                    SingleComposer.AddDropDown(activeQuestKeys, activeQuests.ConvertAll<string>(quest => Lang.Get(quest.questId + "-title")).ToArray(), selected, onActiveQuestSelectionChanged, ElementBounds.FixedOffseted(EnumDialogArea.RightTop, 0, 20, 400, 30), DropDownKey)
                        .AddButton(Lang.Get("alegacyvsquest:button-cancel"), TryClose, bottomLeftButtonBounds)
                        .AddIf(selectedActiveQuest.IsCompletableOnClient)
                            .AddButton(Lang.Get("alegacyvsquest:button-complete"), completeQuest, bottomRightButtonBounds)
                        .EndIf()

                        .BeginClip(clippingBounds)
                            .AddRichtext(activeQuestText(selectedActiveQuest), CairoFont.WhiteSmallishText(), questTextBounds, "questtext")
                        .EndClip()
                        .AddVerticalScrollbar(OnNewScrollbarvalue, scrollbarBounds, "scrollbar");
                }
                else
                {
                    SingleComposer.AddStaticText(Lang.Get("alegacyvsquest:no-quest-active-desc"), CairoFont.WhiteSmallishText(), ElementBounds.Fixed(0, 60, 400, 500))
                        .AddButton(Lang.Get("alegacyvsquest:button-cancel"), TryClose, ElementBounds.FixedOffseted(EnumDialogArea.CenterBottom, 0, -10, 200, 20));
                }
            }
            else
            {
                string repText = reputationText();
                bool hasNpcRewards = reputationNpcHasRewards;
                bool hasFactionRewards = reputationFactionHasRewards;
                bool hasRewards = hasNpcRewards || hasFactionRewards;
                SingleComposer.AddButton(Lang.Get("alegacyvsquest:button-cancel"), TryClose, bottomLeftButtonBounds)
                    .AddIf(hasRewards)
                        .AddIf(hasNpcRewards)
                            .AddButton(Lang.Get("alegacyvsquest:button-claim-rewards-npc"), claimNpcRewards, hasFactionRewards ? bottomRightSecondaryButtonBounds : bottomRightButtonBounds)
                        .EndIf()
                        .AddIf(hasFactionRewards)
                            .AddButton(Lang.Get("alegacyvsquest:button-claim-rewards-faction"), claimFactionRewards, bottomRightButtonBounds)
                        .EndIf()
                    .EndIf()
                    .BeginClip(clippingBounds)
                        .AddRichtext(repText, CairoFont.WhiteSmallishText(), questTextBounds, "questtext")
                    .EndClip()
                    .AddVerticalScrollbar(OnNewScrollbarvalue, scrollbarBounds, "scrollbar");
            }
            ;
            SingleComposer.GetScrollbar("scrollbar")?.SetHeights((float)questTextBounds.fixedHeight, (float)questTextBounds.fixedHeight);
            SingleComposer.EndChildElements()
                    .Compose();
            SingleComposer.GetScrollbar("scrollbar")?.SetNewTotalHeight((float)SingleComposer.GetRichtext("questtext").TotalHeight);
            SingleComposer.GetScrollbar("scrollbar")?.SetScrollbarPosition(0);
        }

        private void OnNewScrollbarvalue(float value)
        {
            var textArea = SingleComposer.GetRichtext("questtext");

            textArea.Bounds.fixedY = -value;
            textArea.Bounds.CalcWorldBounds();
        }

        private void OnTabClicked(int id, GuiTab tab)
        {
            CloseOpenedDropDown();
            curTab = id;
            RequestRecompose();
        }

        private string questText(string questId)
        {
            return Lang.Get(questId + "-desc");
        }

        private string activeQuestText(ActiveQuest quest)
        {
            return quest.ProgressText;
        }

        private string reputationText()
        {
            var lines = new List<string>();

            if (!string.IsNullOrWhiteSpace(reputationNpcId))
            {
                string title = !string.IsNullOrWhiteSpace(reputationNpcTitleLangKey)
                    ? Lang.Get(reputationNpcTitleLangKey)
                    : reputationNpcId;
                string rank = !string.IsNullOrWhiteSpace(reputationNpcRankLangKey)
                    ? Lang.Get(reputationNpcRankLangKey)
                    : Lang.Get("alegacyvsquest:reputation-rank-unknown");

                lines.Add(Lang.Get("alegacyvsquest:reputation-npc-header", title));
                lines.Add(Lang.Get("alegacyvsquest:reputation-value", reputationNpcValue));
                lines.Add(Lang.Get("alegacyvsquest:reputation-rank", rank));
                if (reputationNpcRewardsCount > 0)
                {
                    lines.Add(Lang.Get("alegacyvsquest:reputation-rewards-count", reputationNpcRewardsCount));
                }
                lines.Add("");
            }

            if (!string.IsNullOrWhiteSpace(reputationFactionId))
            {
                string title = !string.IsNullOrWhiteSpace(reputationFactionTitleLangKey)
                    ? Lang.Get(reputationFactionTitleLangKey)
                    : reputationFactionId;
                string rank = !string.IsNullOrWhiteSpace(reputationFactionRankLangKey)
                    ? Lang.Get(reputationFactionRankLangKey)
                    : Lang.Get("alegacyvsquest:reputation-rank-unknown");

                lines.Add(Lang.Get("alegacyvsquest:reputation-faction-header", title));
                lines.Add(Lang.Get("alegacyvsquest:reputation-value", reputationFactionValue));
                lines.Add(Lang.Get("alegacyvsquest:reputation-rank", rank));
                if (reputationFactionRewardsCount > 0)
                {
                    lines.Add(Lang.Get("alegacyvsquest:reputation-rewards-count", reputationFactionRewardsCount));
                }
            }

            if (lines.Count == 0)
            {
                return Lang.Get("alegacyvsquest:reputation-empty");
            }

            return string.Join("\n", lines);
        }

        private bool acceptQuest()
        {
            var message = new QuestAcceptedMessage()
            {
                questGiverId = questGiverId,
                questId = selectedAvailableQuestId
            };
            capi.Network.GetChannel("alegacyvsquest").SendPacket(message);
            if (closeGuiAfterAcceptingAndCompleting)
            {
                TryClose();
            }
            else
            {
                availableQuestIds.Remove(selectedAvailableQuestId);
                RequestRecompose();
            }
            return true;
        }

        private bool completeQuest()
        {
            var message = new QuestCompletedMessage()
            {
                questGiverId = questGiverId,
                questId = selectedActiveQuest.questId
            };
            capi.Network.GetChannel("alegacyvsquest").SendPacket(message);
            if (closeGuiAfterAcceptingAndCompleting)
            {
                TryClose();
            }
            else
            {
                activeQuests.RemoveAll(quest => quest != null && selectedActiveQuest != null && quest.questId == selectedActiveQuest.questId && quest.questGiverId == selectedActiveQuest.questGiverId);
                RequestRecompose();
            }
            return true;
        }

        private bool claimNpcRewards()
        {
            var message = new ClaimReputationRewardsMessage()
            {
                questGiverId = questGiverId,
                scope = "npc"
            };
            capi.Network.GetChannel("alegacyvsquest").SendPacket(message);
            return true;
        }

        private bool claimFactionRewards()
        {
            var message = new ClaimReputationRewardsMessage()
            {
                questGiverId = questGiverId,
                scope = "faction"
            };
            capi.Network.GetChannel("alegacyvsquest").SendPacket(message);
            return true;
        }

        private void onAvailableQuestSelectionChanged(string questId, bool selected)
        {
            if (selected)
            {
                selectedAvailableQuestId = questId;
                SingleComposer.GetRichtext("questtext").SetNewText(questText(questId), CairoFont.WhiteSmallishText());
                SingleComposer.GetScrollbar("scrollbar")?.SetNewTotalHeight((float)SingleComposer.GetRichtext("questtext").TotalHeight);

                SingleComposer.GetScrollbar("scrollbar")?.SetScrollbarPosition(0);
                OnNewScrollbarvalue(0);

                capi.Event.EnqueueMainThreadTask(() =>
                {
                    CloseOpenedDropDown();
                    RequestRecompose();
                }, "alegacyvsquest-availablequestchanged");
            }
        }

        private void onActiveQuestSelectionChanged(string questId, bool selected)
        {
            if (selected)
            {
                selectedActiveQuestKey = questId;
                selectedActiveQuest = activeQuests.Find(quest => ActiveQuestKey(quest) == selectedActiveQuestKey);

                if (selectedActiveQuest == null)
                {
                    return;
                }

                SingleComposer.GetRichtext("questtext").SetNewText(activeQuestText(selectedActiveQuest), CairoFont.WhiteSmallishText());
                SingleComposer.GetScrollbar("scrollbar")?.SetNewTotalHeight((float)SingleComposer.GetRichtext("questtext").TotalHeight);
                SingleComposer.GetScrollbar("scrollbar")?.SetScrollbarPosition(0);
                OnNewScrollbarvalue(0);

                capi.Event.EnqueueMainThreadTask(() =>
                {
                    CloseOpenedDropDown();
                    RequestRecompose();
                }, "alegacyvsquest-activequestchanged");
            }
        }

        public void UpdateFromMessage(QuestInfoMessage message)
        {
            if (message == null) return;

            CloseOpenedDropDown();
            ApplyData(message.questGiverId, message.availableQestIds, message.activeQuests, message.noAvailableQuestDescLangKey, message.noAvailableQuestCooldownDescLangKey, message.noAvailableQuestCooldownDaysLeft, message.noAvailableQuestRotationDaysLeft, message.reputationNpcId, message.reputationFactionId, message.reputationNpcValue, message.reputationFactionValue, message.reputationNpcRankLangKey, message.reputationFactionRankLangKey, message.reputationNpcTitleLangKey, message.reputationFactionTitleLangKey, message.reputationNpcHasRewards, message.reputationFactionHasRewards, message.reputationNpcRewardsCount, message.reputationFactionRewardsCount);

            RequestRecompose();
        }

        private void CloseOpenedDropDown()
        {
            var dropdown = SingleComposer?.GetDropDown(DropDownKey);
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
            var dropdown = SingleComposer?.GetDropDown(DropDownKey);
            bool clickInsideDropdown = dropdown != null && dropdown.IsPositionInside(args.X, args.Y);
            bool clickInsideListMenu = dropdown?.listMenu?.IsOpened == true && dropdown.listMenu.Bounds?.PointInside(args.X, args.Y) == true;

            if (dropdown?.listMenu?.IsOpened == true && !clickInsideDropdown && !clickInsideListMenu)
            {
                capi.Event.EnqueueMainThreadTask(CloseOpenedDropDown, "alegacyvsquest-close-dropdown-deferred");
            }

            base.OnMouseDown(args);
        }

        public override void OnGuiClosed()
        {
            CloseOpenedDropDown();
            base.OnGuiClosed();
        }
    }
}