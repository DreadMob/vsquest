using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace VsQuest
{
    public static class MobLocalizationUtils
    {
        private static Dictionary<string, string> displayNameMap;

        public static void LoadFromAssets(ICoreAPI api)
        {
            if (api == null) return;

            var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            void TryMergeFromDomain(string domain)
            {
                if (string.IsNullOrWhiteSpace(domain)) return;
                try
                {
                    var dicts = api.Assets.GetMany<Dictionary<string, string>>(api.Logger, "config/mobdisplaynames", domain);
                    foreach (var pair in dicts)
                    {
                        if (pair.Value == null) continue;
                        foreach (var kvp in pair.Value)
                        {
                            if (string.IsNullOrWhiteSpace(kvp.Key) || string.IsNullOrWhiteSpace(kvp.Value)) continue;
                            merged[kvp.Key] = kvp.Value;
                        }
                    }
                }
                catch
                {
                }
            }

            // Support legacy/alternate asset domains used in this workspace.
            TryMergeFromDomain("vsquest");
            TryMergeFromDomain("alegacyvsquest");

            foreach (var mod in api.ModLoader.Mods)
            {
                TryMergeFromDomain(mod?.Info?.ModID);
            }

            displayNameMap = merged.Count > 0 ? merged : null;
        }

        public static string GetMobDisplayName(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return code;

            // Always apply explicit overrides first (including fallback to base code).
            foreach (var mapped in GetCandidateCodes(code))
            {
                if (displayNameMap != null && displayNameMap.TryGetValue(mapped, out var mappedName) && !string.IsNullOrWhiteSpace(mappedName))
                {
                    return mappedName;
                }
            }

            // Last-resort fallback for locust variants: vanilla translations for locust variants are often English-only.
            // Since quests treat locust variants as the same target, keep the display name consistent.
            string mappedBase = MapCode(code);
            if (MobLocalizationFallbacks.TryGetFallbackRuName(mappedBase, out var fallbackRuName))
            {
                string key = $"mob-{mappedBase}";
                try
                {
                    string t = Lang.Get(key);
                    if (!string.Equals(t, key, StringComparison.OrdinalIgnoreCase)) return t;
                }
                catch
                {
                }

                return fallbackRuName;
            }

            // If this is a variant (e.g. locust-forest), prefer base-code translations before variant translations.
            // This allows mods to override a whole family via mob-locust / mobdisplaynames.json without needing per-variant entries.
            string baseCode = MapCode(code);
            if (!string.IsNullOrWhiteSpace(baseCode) && !string.Equals(baseCode, code, StringComparison.OrdinalIgnoreCase))
            {
                foreach (string key in GetTranslationKeys(baseCode))
                {
                    try
                    {
                        string t = Lang.Get(key);
                        if (!string.Equals(t, key, StringComparison.OrdinalIgnoreCase)) return t;
                    }
                    catch
                    {
                    }
                }
            }

            // Then try game translations.
            foreach (var mapped in GetCandidateCodes(code))
            {
                foreach (string key in GetTranslationKeys(mapped))
                {
                    try
                    {
                        string t = Lang.Get(key);
                        if (!string.Equals(t, key, StringComparison.OrdinalIgnoreCase)) return t;
                    }
                    catch
                    {
                    }
                }
            }

            return MapCode(code);
        }

        private static IEnumerable<string> GetTranslationKeys(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) yield break;

            string domain = null;
            string path = code;
            int colonIndex = code.IndexOf(':');
            if (colonIndex > 0 && colonIndex < code.Length - 1)
            {
                domain = code.Substring(0, colonIndex);
                path = code.Substring(colonIndex + 1);
            }

            // Backwards compat with older mod versions (if any custom translations exist)
            yield return $"mob-{path}";

            // Vanilla uses item-creature-<code> for entity names
            yield return $"item-creature-{path}";
            yield return $"game:item-creature-{path}";
            if (!string.IsNullOrWhiteSpace(domain)) yield return $"{domain}:item-creature-{path}";
        }

        private static IEnumerable<string> GetCandidateCodes(string code)
        {
            yield return code;

            string baseCode = MapCode(code);
            if (!string.IsNullOrWhiteSpace(baseCode) && !string.Equals(baseCode, code, StringComparison.OrdinalIgnoreCase))
            {
                yield return baseCode;
            }
        }

        public static string NormalizeMobCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return code;

            int colonIndex = code.IndexOf(':');
            if (colonIndex > 0 && colonIndex < code.Length - 1)
            {
                code = code.Substring(colonIndex + 1);
            }

            return code;
        }

        public static bool MobCodeMatches(string targetCode, string killedCode)
        {
            if (string.IsNullOrWhiteSpace(targetCode) || string.IsNullOrWhiteSpace(killedCode)) return false;

            targetCode = NormalizeMobCode(targetCode);
            killedCode = NormalizeMobCode(killedCode);

            if (string.Equals(targetCode, killedCode, StringComparison.OrdinalIgnoreCase)) return true;
            if (killedCode.StartsWith(targetCode + "-", StringComparison.OrdinalIgnoreCase)) return true;

            return false;
        }

        private static string MapCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return code;

            code = NormalizeMobCode(code);

            int dashIndex = code.IndexOf('-');
            if (dashIndex > 0)
            {
                return code.Substring(0, dashIndex);
            }

            return code;
        }
    }
}
