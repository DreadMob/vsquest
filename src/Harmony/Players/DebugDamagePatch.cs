using System;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace VsQuest.Harmony.Players
{
    /// <summary>
    /// Patch for debug damage logging - sends damage messages to players who enabled debug mode.
    /// </summary>
    [HarmonyPatch(typeof(EntityAgent), "ReceiveDamage")]
    public static class DebugDamagePatch
    {
        public static void Postfix(EntityAgent __instance, DamageSource damageSource, float damage)
        {
            // Fast exit if patch disabled
            if (!HarmonyPatchSwitches.DebugDamageEnabled(HarmonyPatchSwitches.DebugDamage_EntityAgent_ReceiveDamage)) return;
            
            // Fast exit if no one has debug enabled
            if (DebugDamageCommandHandler.EnabledPlayers.Count == 0) return;
            
            if (damage <= 0f) return;
            if (damageSource == null) return;
            if (__instance?.Api?.Side != EnumAppSide.Server) return;

            // Only log damage dealt BY players, not received BY players
            var causeEntity = damageSource.GetCauseEntity() ?? damageSource.SourceEntity;
            if (causeEntity is not EntityPlayer playerEntity) return;

            var sapi = __instance.Api as ICoreServerAPI;
            var attacker = playerEntity.Player as IServerPlayer;
            
            DebugDamageCommandHandler.SendDamageMessage(sapi, attacker, __instance, damage, damageSource);
        }
    }
}
