namespace VsQuest
{
    public static class QuestQuestionStateUtil
    {
        public static string BaseKey(string token) => $"alegacyvsquest:question:{token}";
        public static string AnsweredKey(string token) => $"{BaseKey(token)}:answered";
        public static string CorrectKey(string token) => $"{BaseKey(token)}:correct";
        public static string CorrectIndexKey(string token) => $"{BaseKey(token)}:correctindex";
        public static string CorrectValueKey(string token) => $"{BaseKey(token)}:correctvalue";
        public static string CorrectStringKey(string token) => $"{BaseKey(token)}:correctstring";
        public static string SuccessActionsKey(string token) => $"{BaseKey(token)}:success";
        public static string FailActionsKey(string token) => $"{BaseKey(token)}:fail";
        public static string QuestIdKey(string token) => $"{BaseKey(token)}:questid";
        public static string QuestGiverIdKey(string token) => $"{BaseKey(token)}:questgiver";
    }
}
