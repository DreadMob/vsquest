using System;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace VsQuest
{
    public static class BossKillAnnouncementUtil
    {
        private static readonly string[] Templates = new[]
        {
            "{victim} пал в бою с {boss}",
            "{boss} отправил {victim} отдыхать у костра",
            "{victim} не выдержал натиска: {boss}",
            "{boss} раздавил {victim} без жалости",
            "{victim} был повержен: {boss}",
            "{victim} встретил свою судьбу. Виновник: {boss}",
            "{boss} показал {victim}, кто здесь главный",
            "{victim} проиграл дуэль против {boss}",
            "{boss} оставил от {victim} только воспоминания",
            "{victim} не успел увернуться от {boss}"
        };

        public static void AnnouncePlayerKilledByBoss(ICoreServerAPI sapi, IServerPlayer victim, Entity killerBossEntity)
        {
            if (sapi == null || victim == null || killerBossEntity == null) return;

            string bossName = MobLocalizationUtils.GetMobDisplayName(killerBossEntity.Code?.ToShortString());
            if (string.IsNullOrWhiteSpace(bossName)) bossName = killerBossEntity.Code?.ToShortString() ?? "?";

            string victimName = ChatFormatUtil.Font(victim.PlayerName, "#ffd75e");
            string bossNameColored = ChatFormatUtil.Font(bossName, "#ff77ff");

            string template = Templates.Length == 0
                ? "{victim} погиб от {boss}"
                : Templates[sapi.World.Rand.Next(0, Templates.Length)];

            string message = template
                .Replace("{victim}", victimName)
                .Replace("{boss}", bossNameColored);

            GlobalChatBroadcastUtil.BroadcastGeneralChat(sapi, ChatFormatUtil.PrefixAlert(message), Vintagestory.API.Common.EnumChatType.Notification);
        }
    }
}
