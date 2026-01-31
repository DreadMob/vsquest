namespace VsQuest
{
    public class QuizDefinition
    {
        public string id;

        public string scoreAttributeKey;

        public int questionCount;
        public int neededCorrect;

        public string titleLangKey;
        public string bodyLangKey;

        public string progressTemplateLangKey;
        public string resultTemplateLangKey;

        public int[] resultBodyScoreThresholds;
        public string[] resultBodyLangKeys;

        public string retryButtonLangKey;
        public string closeButtonLangKey;

        public string questionLangKeyFormat;
        public string optionALangKeyFormat;
        public string optionBLangKeyFormat;
        public string optionCLangKeyFormat;
        public string optionDLangKeyFormat;

        public int[] correctOptions;
    }
}
