using System;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace VsQuest.Harmony.Players
{
    /// <summary>
    /// Event-driven BossGrab handler. No ticking - reacts only when attribute changes.
    /// </summary>
    [HarmonyPatch(typeof(EntityPlayer), "OnReceivedServerAnimations")]
    public class EntityPlayer_OnReceivedServerAnimations_Patch
    {
        private const string BossGrabNoSneakUntilKey = "alegacyvsquest:bossgrab:nosneakuntil";
        private const string BossGrabActiveKey = "alegacyvsquest:bossgrab:active";
        
        // Track active grab per player entity ID
        private static readonly System.Collections.Generic.Dictionary<long, long> ActiveGrabs = new();

        public static void Postfix(EntityPlayer __instance)
        {
            if (!HarmonyPatchSwitches.PlayerEnabled(HarmonyPatchSwitches.Player_EntityAgent_OnGameTick_Unified)) return;
            if (__instance?.World?.Side != EnumAppSide.Client) return;
            if (__instance.WatchedAttributes == null) return;

            long entityId = __instance.EntityId;
            long untilMs = __instance.WatchedAttributes.GetLong(BossGrabNoSneakUntilKey, 0);
            bool isActive = __instance.WatchedAttributes.GetBool(BossGrabActiveKey, false);
            
            // No active grab - clean up if was tracked
            if (!isActive || untilMs <= 0)
            {
                if (ActiveGrabs.Remove(entityId))
                {
                    // Reset sneak control
                    __instance.Controls.Sneak = false;
                }
                return;
            }

            // Check if already handling this grab
            if (ActiveGrabs.TryGetValue(entityId, out var existingUntil) && existingUntil == untilMs)
            {
                return; // Already handling this grab instance
            }

            // New grab started - handle it
            ActiveGrabs[entityId] = untilMs;
            HandleGrab(__instance, untilMs);
        }

        private static void HandleGrab(EntityPlayer player, long untilMs)
        {
            long nowMs = 0;
            try { nowMs = player.World.ElapsedMilliseconds; } catch { }
            
            if (nowMs <= 0) return;
            
            int remainingMs = (int)(untilMs - nowMs);
            if (remainingMs <= 0) return;

            // Apply immediately
            player.Controls.Sneak = false;

            // Schedule restore using client-side callback
            if (player.World.Api is ICoreClientAPI capi)
            {
                capi.Event.RegisterCallback((dt) =>
                {
                    // Only restore if still tracked (not overwritten by new grab)
                    if (ActiveGrabs.TryGetValue(player.EntityId, out var trackedUntil) && trackedUntil == untilMs)
                    {
                        ActiveGrabs.Remove(player.EntityId);
                    }
                }, remainingMs);
            }
        }
    }
}
