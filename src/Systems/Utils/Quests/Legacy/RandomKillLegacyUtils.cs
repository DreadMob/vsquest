using Vintagestory.API.Server;

namespace VsQuest
{
    public static class RandomKillLegacyUtils
    {
        public static string[] NormalizeRollArgs(string[] args)
        {
            if (args != null && args.Length >= 3)
            {
                bool looksLikeLegacy = int.TryParse(args[0], out _)
                    && int.TryParse(args[1], out _)
                    && !int.TryParse(args[2], out _);

                if (looksLikeLegacy)
                {
                    var newArgs = new string[args.Length + 1];
                    newArgs[0] = "1";
                    for (int i = 0; i < args.Length; i++)
                    {
                        newArgs[i + 1] = args[i];
                    }
                    args = newArgs;
                }
            }

            return args;
        }

        public static bool TryHandleLegacyKill(ICoreServerAPI sapi, IServerPlayer serverPlayer, ActiveQuest activeQuest, string killedCode)
        {
            var wa = serverPlayer?.Entity?.WatchedAttributes;
            if (wa == null) return false;

            string questId = activeQuest?.questId;
            if (string.IsNullOrWhiteSpace(questId)) return false;

            string codeKey = RandomKillQuestUtils.CodeKey(questId);
            if (!wa.HasAttribute(codeKey)) return false;

            string targetCode = wa.GetString(codeKey, null);
            if (string.IsNullOrWhiteSpace(targetCode)) return false;
            if (!LocalizationUtils.MobCodeMatches(targetCode, killedCode)) return false;

            string needKey = RandomKillQuestUtils.NeedKey(questId);
            string haveKey = RandomKillQuestUtils.HaveKey(questId);

            int need = wa.GetInt(needKey, 0);
            if (need <= 0) return false;

            int have = wa.GetInt(haveKey, 0);
            if (have >= need) return false;

            have++;
            if (have > need) have = need;
            wa.SetInt(haveKey, have);
            wa.MarkPathDirty(haveKey);

            RandomKillQuestUtils.FireActions(sapi, serverPlayer, activeQuest, have >= need);
            return true;
        }
    }
}
