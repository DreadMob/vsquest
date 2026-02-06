using HarmonyLib;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace VsQuest.Harmony
{
    [HarmonyPatch(typeof(Block), "OnBlockInteractStart")]
    public class BlockInteractPatch
    {
        private const int DebounceMs = 100;

        private static long lastSendMs;
        private static int lastX = int.MinValue;
        private static int lastY = int.MinValue;
        private static int lastZ = int.MinValue;
        private static int lastDim = int.MinValue;
        private static string lastBlockCode;

        public static void Postfix(Block __instance, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, bool __result)
        {
            if (world.Api.Side != EnumAppSide.Client || blockSel == null) return;

            if (__result)
            {
                return;
            }

            // If the player is holding a block, this is very likely a placement attempt.
            // We already track placement on the server via DidPlaceBlock, and sending an extra
            // "vanilla interact" packet for every placement attempt can create server-side load
            // spikes when quests are active (leading to rubberband).
            try
            {
                var held = byPlayer?.InventoryManager?.ActiveHotbarSlot?.Itemstack;
                if (held?.Collectible is Block)
                {
                    return;
                }
            }
            catch
            {
                // ignore and fall through to keep behavior stable if API changes
            }

            var capi = world.Api as ICoreClientAPI;
            if (capi == null) return;

            try
            {
                int x = blockSel.Position.X;
                int y = blockSel.Position.Y;
                int z = blockSel.Position.Z;
                int dim = byPlayer?.Entity?.Pos?.Dimension ?? 0;
                string code = __instance?.Code?.ToString();

                long now = Environment.TickCount64;
                if ((now - lastSendMs) < DebounceMs
                    && lastX == x && lastY == y && lastZ == z
                    && lastDim == dim
                    && string.Equals(lastBlockCode, code, StringComparison.Ordinal))
                {
                    return;
                }

                lastSendMs = now;
                lastX = x;
                lastY = y;
                lastZ = z;
                lastDim = dim;
                lastBlockCode = code;
            }
            catch
            {
            }

            capi.Network.GetChannel("alegacyvsquest").SendPacket(new VanillaBlockInteractMessage()
            {
                Position = blockSel.Position,
                BlockCode = __instance.Code.ToString()
            });
        }
    }
}