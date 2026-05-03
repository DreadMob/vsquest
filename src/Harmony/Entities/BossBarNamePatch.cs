using System;
using HarmonyLib;
using Vintagestory.GameContent;

namespace VsQuest.Harmony
{
    [HarmonyPatch(typeof(EntityBehaviorBoss), "get_BossName")]
    public static class BossBarNamePatch
    {
        public static bool Prefix(EntityBehaviorBoss __instance, ref string __result)
        {
            try
            {
                var entity = __instance?.entity;
                string domain = entity?.Code?.Domain;
                string path = entity?.Code?.Path;
                if (string.IsNullOrWhiteSpace(domain) || string.IsNullOrWhiteSpace(path))
                {
                    return true;
                }

                string key = domain + ":item-creature-" + path;
                string localized = LocalizationUtils.GetSafeStrictDomains(key);
                if (!string.IsNullOrWhiteSpace(localized) && !string.Equals(localized, key, StringComparison.OrdinalIgnoreCase))
                {
                    __result = localized;
                    return false;
                }

                string byEntity = MobLocalizationUtils.GetMobDisplayName(entity);
                if (!string.IsNullOrWhiteSpace(byEntity))
                {
                    __result = byEntity;
                    return false;
                }

                return true;
            }
            catch
            {
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(EntityBehaviorErelBoss), "get_BossName")]
    public static class ErelBossBarNamePatch
    {
        public static void Postfix(ref string __result)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(__result)) return;
                string localized = LocalizationUtils.GetSafeStrictDomains(__result);
                if (!string.IsNullOrWhiteSpace(localized) && !string.Equals(localized, __result, StringComparison.OrdinalIgnoreCase))
                {
                    __result = localized;
                }
            }
            catch
            {
            }
        }
    }
}
