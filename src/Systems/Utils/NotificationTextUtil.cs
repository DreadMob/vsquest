using Vintagestory.API.Config;

namespace VsQuest
{
    public static class NotificationTextUtil
    {
        private static string TryBuildNotificationFromTemplate(ShowNotificationMessage message)
        {
            if (message == null) return null;
            if (string.IsNullOrWhiteSpace(message.Template) || string.IsNullOrWhiteSpace(message.MobCode)) return null;

            string mobName = LocalizationUtils.GetMobDisplayName(message.MobCode);
            try
            {
                return Lang.HasTranslation(message.Template)
                    ? Lang.Get(message.Template, message.Need, mobName)
                    : string.Format(message.Template, message.Need, mobName);
            }
            catch
            {
                return message.Template;
            }
        }

        private static string TryBuildNotificationFromLegacyText(ShowNotificationMessage message)
        {
            if (message == null) return null;

            string text = message.Notification;
            if (string.IsNullOrEmpty(text)) return text;

            try
            {
                if (Lang.HasTranslation(text)) text = Lang.Get(text);
            }
            catch
            {
            }

            return text;
        }

        public static string Build(ShowNotificationMessage message)
        {
            string text = TryBuildNotificationFromTemplate(message);
            if (string.IsNullOrEmpty(text))
            {
                text = TryBuildNotificationFromLegacyText(message);
            }

            return text;
        }
    }
}
