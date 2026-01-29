using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace VsQuest
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class TemporalRiftProjectorConfigData
    {
        public float riftOffsetY;
        public float riftSize;
        public string riftColor;
        public float riftColorStrength;
    }

    public class TemporalRiftProjectorConfigGui : GuiDialogGeneric
    {
        private const string KeyOffsetY = "offsetY";
        private const string KeySize = "size";
        private const string KeyColor = "color";
        private const string KeyStrength = "strength";

        private readonly BlockPos bePos;
        private bool updating;

        public TemporalRiftProjectorConfigData Data = new TemporalRiftProjectorConfigData();

        public TemporalRiftProjectorConfigGui(BlockPos bePos, ICoreClientAPI capi) : base("Temporal rift projector", capi)
        {
            this.bePos = bePos;
        }

        public override void OnGuiOpened()
        {
            Compose();
        }

        private void Compose()
        {
            ClearComposers();

            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;

            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.RightMiddle).WithFixedAlignmentOffset(-20, 0);

            ElementBounds cur = ElementBounds.Fixed(0, 30, 420, 30);
            ElementBounds fullRow = cur.FlatCopy();

            ElementBounds closeButtonBounds = ElementBounds.FixedSize(0, 0).WithAlignment(EnumDialogArea.LeftFixed).WithFixedPadding(20, 4);
            ElementBounds saveButtonBounds = ElementBounds.FixedSize(0, 0).WithAlignment(EnumDialogArea.RightFixed).WithFixedPadding(20, 4);

            bgBounds.WithChildren(cur, closeButtonBounds, saveButtonBounds);

            SingleComposer = capi.Gui.CreateCompo("alegacyvsquest-temporalriftprojector-config", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(DialogTitle, OnTitleBarClose)
                .BeginChildElements(bgBounds);

            SingleComposer
                .AddStaticText("Rift offset Y", CairoFont.WhiteDetailText(), fullRow = cur)
                .AddNumberInput(cur = fullRow.BelowCopy(0, -10).WithFixedSize(120, 29), null, CairoFont.WhiteDetailText(), KeyOffsetY);

            SingleComposer
                .AddStaticText("Rift size", CairoFont.WhiteDetailText(), fullRow = cur.BelowCopy(0, 10).WithFixedSize(420, 30))
                .AddNumberInput(cur = fullRow.BelowCopy(0, -10).WithFixedSize(120, 29), null, CairoFont.WhiteDetailText(), KeySize);

            SingleComposer
                .AddStaticText("Rift color (hex, e.g. #ff00ff)", CairoFont.WhiteDetailText(), fullRow = cur.BelowCopy(0, 10).WithFixedSize(420, 30))
                .AddTextInput(cur = fullRow.BelowCopy(0, -10).WithFixedSize(220, 29), null, CairoFont.WhiteDetailText(), KeyColor);

            SingleComposer
                .AddStaticText("Color strength (0..1)", CairoFont.WhiteDetailText(), fullRow = cur.BelowCopy(0, 10).WithFixedSize(420, 30))
                .AddNumberInput(cur = fullRow.BelowCopy(0, -10).WithFixedSize(120, 29), null, CairoFont.WhiteDetailText(), KeyStrength);

            SingleComposer
                .AddSmallButton("Close", OnButtonClose, closeButtonBounds.FixedUnder(cur, 10))
                .AddSmallButton("Save", OnButtonSave, saveButtonBounds.FixedUnder(cur, 10))
                .EndChildElements()
                .Compose();

            UpdateFromServer(Data);
        }

        private void OnTitleBarClose()
        {
            OnButtonClose();
        }

        private bool OnButtonClose()
        {
            TryClose();
            return true;
        }

        private bool OnButtonSave()
        {
            if (updating) return true;

            var data = new TemporalRiftProjectorConfigData();
            data.riftOffsetY = (float)SingleComposer.GetNumberInput(KeyOffsetY).GetValue();
            data.riftSize = (float)SingleComposer.GetNumberInput(KeySize).GetValue();
            data.riftColor = SingleComposer.GetTextInput(KeyColor).GetText();
            data.riftColorStrength = (float)SingleComposer.GetNumberInput(KeyStrength).GetValue();

            capi.Network.SendBlockEntityPacket(bePos, 4001, SerializerUtil.Serialize(data));
            return true;
        }

        public void UpdateFromServer(TemporalRiftProjectorConfigData data)
        {
            if (data == null || SingleComposer == null) return;

            updating = true;
            Data = data;

            SingleComposer.GetNumberInput(KeyOffsetY).SetValue(data.riftOffsetY);
            SingleComposer.GetNumberInput(KeySize).SetValue(data.riftSize);
            SingleComposer.GetTextInput(KeyColor).SetValue(data.riftColor ?? "#ffffff");
            SingleComposer.GetNumberInput(KeyStrength).SetValue(data.riftColorStrength);

            updating = false;
        }
    }
}
