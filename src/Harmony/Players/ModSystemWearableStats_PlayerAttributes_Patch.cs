using System;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace VsQuest.Harmony.Players
{
    [HarmonyPatch(typeof(ModSystemWearableStats), "handleDamaged")]
    public class ModSystemWearableStats_handleDamaged_PlayerAttributes_Patch
    {
        public static void Postfix(ModSystemWearableStats __instance, IPlayer player, float damage, DamageSource dmgSource, ref float __result)
        {
            if (!HarmonyPatchSwitches.PlayerEnabled(HarmonyPatchSwitches.Player_ModSystemWearableStats_handleDamaged_PlayerAttributes)) return;
            if (__result <= 0f) return;

            if (player?.Entity?.WatchedAttributes == null) return;

            if (!IsProtectionApplicable(dmgSource)) return;

            float playerFlat = player.Entity.WatchedAttributes.GetFloat("vsquestadmin:attr:protection", 0f);
            float playerPerc = player.Entity.WatchedAttributes.GetFloat("vsquestadmin:attr:protectionperc", 0f);

            float newDamage = __result;
            newDamage = System.Math.Max(0f, newDamage - playerFlat);

            playerPerc = System.Math.Max(0f, System.Math.Min(0.95f, playerPerc));
            newDamage *= (1f - playerPerc);

            __result = newDamage;
        }

        private static bool IsProtectionApplicable(DamageSource dmgSource)
        {
            EnumDamageType type;
            try
            {
                type = dmgSource?.Type ?? EnumDamageType.Injury;
            }
            catch (Exception e)
            {
                var api = dmgSource?.SourceEntity?.Api ?? dmgSource?.GetCauseEntity()?.Api;
                api?.Logger?.Error($"[vsquest] IsProtectionApplicable Get DamageSource.Type: {e}");
                type = EnumDamageType.Injury;
            }

            // Apply custom armor only to direct physical damage.
            // Do not reduce suffocation/drowning, hunger, poison, fire, etc.
            return type == EnumDamageType.BluntAttack
                || type == EnumDamageType.SlashingAttack
                || type == EnumDamageType.PiercingAttack
                || type == EnumDamageType.Crushing
                || type == EnumDamageType.Injury;
        }
    }
}
