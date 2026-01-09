using System;

namespace VsQuest
{
    public static class MobLocalizationFallbacks
    {
        public static bool TryGetFallbackRuName(string baseCode, out string name)
        {
            name = null;
            if (string.IsNullOrWhiteSpace(baseCode)) return false;

            if (string.Equals(baseCode, "locust", StringComparison.OrdinalIgnoreCase)) { name = "Саранча"; return true; }
            if (string.Equals(baseCode, "wolf", StringComparison.OrdinalIgnoreCase)) { name = "Волк"; return true; }
            if (string.Equals(baseCode, "bear", StringComparison.OrdinalIgnoreCase)) { name = "Медведь"; return true; }
            if (string.Equals(baseCode, "shiver", StringComparison.OrdinalIgnoreCase)) { name = "Шивер"; return true; }
            if (string.Equals(baseCode, "bowtorn", StringComparison.OrdinalIgnoreCase)) { name = "Боуторн"; return true; }

            return false;
        }
    }
}
