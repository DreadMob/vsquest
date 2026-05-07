using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace VsQuest
{
    /// <summary>
    /// GUI dialog for rerolling boss items.
    /// Shows available reroll groups and allows player to exchange items.
    /// </summary>
    public class RerollDialogGui : GuiDialog
    {
        public override string ToggleKeyCombinationCode => null;

        private readonly string[] availableGroups;
        private readonly List<(string groupId, string groupName, int itemCount, int itemsRequired)> groups = new List<(string, string, int, int)>();

        public RerollDialogGui(ICoreClientAPI capi, string[] availableGroups) : base(capi)
        {
            this.availableGroups = availableGroups;
            ParseGroups();
            recompose();
        }

        private void ParseGroups()
        {
            if (availableGroups == null) return;

            foreach (var groupStr in availableGroups)
            {
                if (string.IsNullOrWhiteSpace(groupStr)) continue;

                var parts = groupStr.Split('|');
                if (parts.Length >= 4)
                {
                    string groupId = parts[0];
                    string groupName = parts[1];
                    if (int.TryParse(parts[2], out int itemCount) && int.TryParse(parts[3], out int itemsRequired))
                    {
                        groups.Add((groupId, groupName, itemCount, itemsRequired));
                    }
                }
            }
        }

        private void recompose()
        {
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            ElementBounds listBounds = ElementBounds.Fixed(0, 40, 400, Math.Max(100, groups.Count * 35));

            bgBounds.BothSizing = ElementSizing.FitToChildren;

            string titleText = LocalizationUtils.GetSafe("alegacyvsquest:reroll-dialog-title");

            SingleComposer = capi.Gui.CreateCompo("RerollDialog-", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(titleText, () => TryClose())
                .BeginChildElements(bgBounds);

            if (groups.Count == 0)
            {
                string noItemsText = LocalizationUtils.GetSafe("alegacyvsquest:reroll-no-items");
                SingleComposer
                    .AddRichtext(noItemsText, CairoFont.WhiteSmallishText(), listBounds, "notext")
                    .AddButton(Lang.Get("alegacyvsquest:button-cancel"), TryClose, ElementBounds.Fixed(100, 60, 200, 20));
            }
            else
            {
                int yOffset = 0;
                foreach (var group in groups)
                {
                    string groupText = $"{group.groupName} ({group.itemCount}/{group.itemsRequired})";
                    string buttonText = LocalizationUtils.GetSafe("alegacyvsquest:reroll-button");

                    ElementBounds textBounds = ElementBounds.Fixed(10, yOffset, 280, 30);
                    ElementBounds buttonBounds = ElementBounds.Fixed(300, yOffset + 5, 100, 20);

                    SingleComposer
                        .AddStaticText(groupText, CairoFont.WhiteSmallishText(), textBounds)
                        .AddButton(buttonText, () => OnRerollClick(group.groupId), buttonBounds);

                    yOffset += 35;
                }

                ElementBounds closeButtonBounds = ElementBounds.Fixed(100, yOffset + 10, 200, 20);
                SingleComposer.AddButton(Lang.Get("alegacyvsquest:button-cancel"), TryClose, closeButtonBounds);
            }

            SingleComposer.EndChildElements().Compose();
        }

        private bool OnRerollClick(string groupId)
        {
            capi.Network.GetChannel("alegacyvsquest").SendPacket(new ExecuteRerollMessage
            {
                GroupId = groupId
            });
            TryClose();
            return true;
        }

        public static void ShowFromMessage(ShowRerollDialogMessage message, ICoreClientAPI capi)
        {
            new RerollDialogGui(capi, message.AvailableGroups).TryOpen();
        }
    }
}
