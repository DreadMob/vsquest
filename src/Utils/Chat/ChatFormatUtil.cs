namespace VsQuest
{
    public static class ChatFormatUtil
    {
        public static string Font(string text, string hexColor)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            if (string.IsNullOrWhiteSpace(hexColor)) return text;

            return $"<font color=\"{hexColor}\">{text}</font>";
        }

        public static string PrefixAlert(string text)
        {
            return $"{Font("[!] ", "#ff5555")}{Font(text, "#ffffff")}";
        }

        public static string LoreBlock(string title, string middleLine, string bottomLine)
        {
            string safeTitle = title ?? "";
            string safeMiddle = middleLine ?? "";
            string safeBottom = bottomLine ?? "";

            return $"<font color=\"#FAA61A\"><strong>{safeTitle}</strong></font>\n"
                 + $"<font color=\"#C0C0C0\">   ├ {safeMiddle}</font>\n"
                 + $"<font color=\"#C0C0C0\">   └ {safeBottom}</font>";
        }
    }
}
