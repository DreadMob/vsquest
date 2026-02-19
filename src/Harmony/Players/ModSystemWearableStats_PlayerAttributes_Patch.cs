using System;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace VsQuest.Harmony.Players
{
    [HarmonyPatch(typeof(ModSystemWearableStats), "handleDamaged")]
    public class ModSystemWearableStats_handleDamaged_PlayerAttributes_Patch
    {
        // Damage types that protection applies to - stored as bitmask for fast check
        private static readonly EnumDamageType[] PhysicalDamageTypes = new[]
        {
            EnumDamageType.BluntAttack,
            EnumDamageType.SlashingAttack,
            EnumDamageType.PiercingAttack,
            EnumDamageType.Crushing,
            EnumDamageType.Injury
        };

        public static void Postfix(ModSystemWearableStats __instance, IPlayer player, float damage, DamageSource dmgSource, ref float __result)
        {
            if (!HarmonyPatchSwitches.PlayerEnabled(HarmonyPatchSwitches.Player_ModSystemWearableStats_handleDamaged_PlayerAttributes)) return;
            if (__result <= 0f) return;

            // Early exit: check damage type before any attribute reads
            if (!IsProtectionApplicableFast(dmgSource)) return;

            if (player?.Entity?.WatchedAttributes == null) return;

            // Batch read protection values from WatchedAttributes
            var wa = player.Entity.WatchedAttributes;
            var tree = wa.GetTreeAttribute("vsquestadmin");
            if (tree == null) return;
            
            float playerFlat = tree.GetFloat("attr:protection", 0f);
            float playerPerc = tree.GetFloat("attr:protectionperc", 0f);

            // Fast exit: no protection
            if (playerFlat <= 0f && playerPerc <= 0f) return;

            float newDamage = __result;
            if (playerFlat > 0f)
            {
                newDamage = System.Math.Max(0f, newDamage - playerFlat);
            }
            if (playerPerc > 0f)
            {
                playerPerc = System.Math.Min(0.95f, playerPerc);
                newDamage *= (1f - playerPerc);
            }

            __result = newDamage;
        }

        private static bool IsProtectionApplicableFast(DamageSource dmgSource)
        {
            if (dmgSource == null) return false;
            
            // Fast check without try-catch - direct comparison
            var type = dmgSource.Type;
            // Unrolled loop for performance
            return type == EnumDamageType.BluntAttack
                || type == EnumDamageType.SlashingAttack
                || type == EnumDamageType.PiercingAttack
                || type == EnumDamageType.Crushing
                || type == EnumDamageType.Injury;
        }
    }
}
