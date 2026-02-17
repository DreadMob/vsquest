using System;
using HarmonyLib;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace VsQuest.Harmony
{
    [HarmonyPatch(typeof(Entity), "GetPrefixAndCreatureName")]
    public static class EntityPrefixAndCreatureNamePatch
    {
        public static bool Prefix(Entity __instance, ref string __result)
        {
            try
            {
                if (!HarmonyPatchSwitches.EntityPrefixAndCreatureNameEnabled(HarmonyPatchSwitches.EntityPrefixAndCreatureName_Entity_GetPrefixAndCreatureName)) return true;
                if (__instance == null) return true;

                // Cheap domain check first before expensive GetBehavior calls
                string domain = __instance.Code?.Domain;
                bool isQuestDomain = string.Equals(domain, "alstory", StringComparison.OrdinalIgnoreCase);
                bool isQuestTargetDomain = string.Equals(domain, "vsquest", StringComparison.OrdinalIgnoreCase) ||
                                          string.Equals(domain, "alegacyvsquest", StringComparison.OrdinalIgnoreCase);
                
                if (!isQuestDomain && !isQuestTargetDomain)
                {
                    // Fast path: not a quest entity, skip expensive checks
                    return true;
                }

                if (__instance.GetBehavior<EntityBehaviorQuestBoss>() == null
                    && __instance.GetBehavior<EntityBehaviorQuestTarget>() == null
                    && __instance.GetBehavior<EntityBehaviorBoss>() == null)
                {
                    return true;
                }

                string name = MobLocalizationUtils.GetMobDisplayName(__instance);
                if (string.IsNullOrWhiteSpace(name)) return true;

                __result = name;
                return false;
            }
            catch
            {
                return true;
            }
        }
    }
}
