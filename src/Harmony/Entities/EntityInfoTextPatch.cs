using System;
using HarmonyLib;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace VsQuest.Harmony
{
    [HarmonyPatch(typeof(Entity), "GetInfoText")]
    public static class EntityInfoTextPatch
    {
        public static bool Prefix(Entity __instance, ref string __result)
        {
            try
            {
                if (!HarmonyPatchSwitches.EntityInfoTextEnabled(HarmonyPatchSwitches.EntityInfoText_Entity_GetInfoText)) return true;
                if (__instance == null) return true;

                if (!ShouldHideInfo(__instance))
                {
                    return true;
                }

                __result = string.Empty;
                return false;
            }
            catch
            {
                return true;
            }
        }

        public static void Postfix(Entity __instance, ref string __result)
        {
            try
            {
                if (!HarmonyPatchSwitches.EntityInfoTextEnabled(HarmonyPatchSwitches.EntityInfoText_Entity_GetInfoText)) return;
                __result = ResolveInfoTextLangKey(__instance, __result);
            }
            catch
            {
            }
        }

        internal static bool ShouldHideInfo(Entity entity)
        {
            if (entity == null) return false;

            try
            {
                // Cheap check first: entity properties
                if (entity.Properties?.Attributes != null
                    && entity.Properties.Attributes["alegacyvsquestHideInfoText"].AsBool(false))
                {
                    return true;
                }
            }
            catch
            {
            }

            // Domain check before expensive GetBehavior
            string domain = entity.Code?.Domain;
            if (!string.Equals(domain, "alstory", StringComparison.OrdinalIgnoreCase)) return false;

            // Only check behaviors for alstory entities (rare case)
            return entity.GetBehavior<EntityBehaviorQuestBoss>() != null
                || entity.GetBehavior<EntityBehaviorBoss>() != null;
        }

        internal static string ResolveInfoTextLangKey(Entity entity, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;

            string trimmed = text.Trim();

            // Only touch plain key-like values; keep regular info text untouched.
            if (trimmed.Contains(" ") || trimmed.Contains("\n") || trimmed.Contains("\r")) return text;

            string localized = LocalizationUtils.GetSafeStrictDomains(trimmed);
            if (!string.IsNullOrWhiteSpace(localized) && !string.Equals(localized, trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return localized;
            }

            // Handle common entity-name keys without explicit domain in info text.
            if (trimmed.StartsWith("item-creature-", StringComparison.OrdinalIgnoreCase))
            {
                string domain = entity?.Code?.Domain;
                if (!string.IsNullOrWhiteSpace(domain))
                {
                    string full = domain + ":" + trimmed;
                    localized = LocalizationUtils.GetSafeStrictDomains(full);
                    if (!string.IsNullOrWhiteSpace(localized) && !string.Equals(localized, full, StringComparison.OrdinalIgnoreCase))
                    {
                        return localized;
                    }
                }
            }

            return text;
        }
    }

    [HarmonyPatch(typeof(EntityAgent), "GetInfoText")]
    public static class EntityAgentInfoTextPatch
    {
        public static bool Prefix(EntityAgent __instance, ref string __result)
        {
            try
            {
                if (!HarmonyPatchSwitches.EntityInfoTextEnabled(HarmonyPatchSwitches.EntityInfoText_EntityAgent_GetInfoText)) return true;
                if (__instance == null) return true;

                if (!EntityInfoTextPatch.ShouldHideInfo(__instance))
                {
                    return true;
                }

                __result = string.Empty;
                return false;
            }
            catch
            {
                return true;
            }
        }

        public static void Postfix(EntityAgent __instance, ref string __result)
        {
            try
            {
                if (!HarmonyPatchSwitches.EntityInfoTextEnabled(HarmonyPatchSwitches.EntityInfoText_EntityAgent_GetInfoText)) return;
                __result = EntityInfoTextPatch.ResolveInfoTextLangKey(__instance, __result);
            }
            catch
            {
            }
        }
    }
}
