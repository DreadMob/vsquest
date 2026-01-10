using Vintagestory.API.Common;

namespace VsQuest
{
    public static class LocalizationUtils
    {
        public static void LoadFromAssets(ICoreAPI api)
        {
            MobLocalizationUtils.LoadFromAssets(api);
        }

        public static string GetSafe(string langKey)
        {
            return LangUtil.GetSafe(langKey);
        }

        public static string GetSafe(string langKey, params object[] args)
        {
            return LangUtil.GetSafe(langKey, args);
        }

        public static string GetFallback(string primaryLangKey, string fallbackLangKey)
        {
            return LangUtil.GetFallback(primaryLangKey, fallbackLangKey);
        }

        public static string GetMobDisplayName(string code)
        {
            return MobLocalizationUtils.GetMobDisplayName(code);
        }

        public static bool MobCodeMatches(string targetCode, string killedCode)
        {
            return MobLocalizationUtils.MobCodeMatches(targetCode, killedCode);
        }

        public static string NormalizeMobCode(string code)
        {
            return MobLocalizationUtils.NormalizeMobCode(code);
        }
    }
}
