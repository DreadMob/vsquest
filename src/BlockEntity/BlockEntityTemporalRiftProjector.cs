using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace VsQuest
{
    public class BlockEntityTemporalRiftProjector : BlockEntity
    {
        private const string AttrOffsetY = "alegacyvsquest:temporalriftprojector:offsety";
        private const string AttrSize = "alegacyvsquest:temporalriftprojector:size";
        private const string AttrColor = "alegacyvsquest:temporalriftprojector:color";
        private const string AttrColorStrength = "alegacyvsquest:temporalriftprojector:colorstrength";

        private const int PacketOpenGui = 4000;
        private const int PacketSave = 4001;

        private float offsetY = 2f;
        private float size = 2f;

        private string colorHex = "#ffffff";
        private float colorStrength;

        private TemporalRiftProjectorConfigGui dlg;

        public float OffsetY => offsetY;
        public float Size => size;
        public string ColorHex => colorHex;
        public float ColorStrength => colorStrength;

        internal void OnInteract(IPlayer byPlayer)
        {
            if (byPlayer == null) return;

            if (Api.Side == EnumAppSide.Server)
            {
                var sp = byPlayer as IServerPlayer;
                if (sp == null) return;

                var data = BuildConfigData();
                (Api as ICoreServerAPI).Network.SendBlockEntityPacket(sp, Pos, PacketOpenGui, SerializerUtil.Serialize(data));
                return;
            }
        }

        public override void OnReceivedServerPacket(int packetid, byte[] bytes)
        {
            if (packetid != PacketOpenGui) return;

            var data = SerializerUtil.Deserialize<TemporalRiftProjectorConfigData>(bytes);

            if (dlg == null || !dlg.IsOpened())
            {
                dlg = new TemporalRiftProjectorConfigGui(Pos, Api as ICoreClientAPI);
                dlg.Data = data;
                dlg.TryOpen();
                dlg.OnClosed += () =>
                {
                    dlg?.Dispose();
                    dlg = null;
                };
            }
            else
            {
                dlg.UpdateFromServer(data);
            }

            ApplyConfigData(data, markDirty: false);
        }

        public override void OnReceivedClientPacket(IPlayer fromPlayer, int packetid, byte[] bytes)
        {
            var sp = fromPlayer as IServerPlayer;
            if (sp == null || !sp.HasPrivilege(Privilege.controlserver)) return;

            if (packetid != PacketSave) return;

            var data = SerializerUtil.Deserialize<TemporalRiftProjectorConfigData>(bytes);
            ApplyConfigData(data, markDirty: true);

            var refreshed = BuildConfigData();
            (Api as ICoreServerAPI).Network.SendBlockEntityPacket(sp, Pos, PacketOpenGui, SerializerUtil.Serialize(refreshed));
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);

            var attrs = Block?.Attributes;
            offsetY = attrs?[
                "riftOffsetY"].AsFloat(offsetY) ?? offsetY;
            size = attrs?["riftSize"].AsFloat(size) ?? size;

            colorHex = attrs?["riftColor"].AsString(colorHex) ?? colorHex;
            colorStrength = attrs?["riftColorStrength"].AsFloat(colorStrength) ?? colorStrength;
            if (colorStrength < 0f) colorStrength = 0f;
            if (colorStrength > 1f) colorStrength = 1f;

            if (size < 0.1f) size = 0.1f;
            if (size > 20f) size = 20f;

            MarkDirty(true);
        }

        private TemporalRiftProjectorConfigData BuildConfigData()
        {
            return new TemporalRiftProjectorConfigData
            {
                riftOffsetY = offsetY,
                riftSize = size,
                riftColor = colorHex,
                riftColorStrength = colorStrength
            };
        }

        private void ApplyConfigData(TemporalRiftProjectorConfigData data, bool markDirty)
        {
            if (data == null) return;

            offsetY = data.riftOffsetY;
            size = data.riftSize;
            if (size < 0.1f) size = 0.1f;
            if (size > 20f) size = 20f;

            colorHex = data.riftColor ?? "#ffffff";
            colorStrength = data.riftColorStrength;
            if (colorStrength < 0f) colorStrength = 0f;
            if (colorStrength > 1f) colorStrength = 1f;

            if (markDirty)
            {
                MarkDirty(true);
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            offsetY = tree.GetFloat(AttrOffsetY, offsetY);
            size = tree.GetFloat(AttrSize, size);

            colorHex = tree.GetString(AttrColor, colorHex);
            colorStrength = tree.GetFloat(AttrColorStrength, colorStrength);
            if (colorStrength < 0f) colorStrength = 0f;
            if (colorStrength > 1f) colorStrength = 1f;
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetFloat(AttrOffsetY, offsetY);
            tree.SetFloat(AttrSize, size);

            if (!string.IsNullOrWhiteSpace(colorHex)) tree.SetString(AttrColor, colorHex);
            tree.SetFloat(AttrColorStrength, colorStrength);
        }
    }
}
