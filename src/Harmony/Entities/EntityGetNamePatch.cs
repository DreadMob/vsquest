using System;
using HarmonyLib;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace VsQuest.Harmony
{
    [HarmonyPatch(typeof(Entity), "GetName")]
    public static class EntityGetNamePatch
    {
        public static bool Prefix(Entity __instance, ref string __result)
        {
            try
            {
                if (__instance == null) return true;

                string domain = __instance.Code?.Domain;
                bool isQuestDomain = string.Equals(domain, "alstory", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(domain, "albase", StringComparison.OrdinalIgnoreCase);
                bool isQuestTargetDomain = string.Equals(domain, "vsquest", StringComparison.OrdinalIgnoreCase) ||
                                           string.Equals(domain, "alegacyvsquest", StringComparison.OrdinalIgnoreCase);

                if (!isQuestDomain && !isQuestTargetDomain) return true;

                bool hasQuestOrBossBehavior =
                    __instance.GetBehavior<EntityBehaviorQuestBoss>() != null
                    || __instance.GetBehavior<EntityBehaviorQuestTarget>() != null
                    || __instance.GetBehavior<EntityBehaviorBoss>() != null;

                if (!hasQuestOrBossBehavior && !isQuestDomain) return true;

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
