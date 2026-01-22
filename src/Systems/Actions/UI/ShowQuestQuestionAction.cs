using System;
using System.Collections.Generic;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class ShowQuestQuestionAction : IQuestAction
    {
        public void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (sapi == null || byPlayer == null) return;
            if (args == null || args.Length < 7)
            {
                throw new QuestException("The 'showquestquestion' action requires at least 7 arguments: token, titleLangKey, textLangKey, correctIndex, successActions, failActions, optionLangKey...");
            }

            string token = args[0];
            string titleLangKey = args[1];
            string textLangKey = args[2];

            if (string.IsNullOrWhiteSpace(token))
            {
                throw new QuestException("The 'showquestquestion' action requires a non-empty token.");
            }

            if (!int.TryParse(args[3], out int correctIndex)) correctIndex = 0;
            string successActions = args[4];
            string failActions = args[5];

            if (string.Equals(successActions, "none", StringComparison.OrdinalIgnoreCase)) successActions = null;
            if (string.Equals(failActions, "none", StringComparison.OrdinalIgnoreCase)) failActions = null;

            var optionKeys = new List<string>();
            for (int i = 6; i < args.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(args[i])) optionKeys.Add(args[i]);
            }

            if (optionKeys.Count == 0)
            {
                throw new QuestException("The 'showquestquestion' action requires at least one optionLangKey.");
            }

            if (correctIndex < 0) correctIndex = 0;
            if (correctIndex >= optionKeys.Count) correctIndex = optionKeys.Count - 1;

            var wa = byPlayer.Entity?.WatchedAttributes;
            if (wa != null)
            {
                wa.SetBool(QuestQuestionStateUtil.AnsweredKey(token), false);
                wa.MarkPathDirty(QuestQuestionStateUtil.AnsweredKey(token));

                wa.SetBool(QuestQuestionStateUtil.CorrectKey(token), false);
                wa.MarkPathDirty(QuestQuestionStateUtil.CorrectKey(token));

                wa.SetInt(QuestQuestionStateUtil.CorrectValueKey(token), 0);
                wa.MarkPathDirty(QuestQuestionStateUtil.CorrectValueKey(token));

                wa.SetString(QuestQuestionStateUtil.CorrectStringKey(token), "0");
                wa.MarkPathDirty(QuestQuestionStateUtil.CorrectStringKey(token));

                wa.SetInt(QuestQuestionStateUtil.CorrectIndexKey(token), correctIndex);
                wa.MarkPathDirty(QuestQuestionStateUtil.CorrectIndexKey(token));

                if (!string.IsNullOrWhiteSpace(successActions))
                {
                    wa.SetString(QuestQuestionStateUtil.SuccessActionsKey(token), successActions);
                    wa.MarkPathDirty(QuestQuestionStateUtil.SuccessActionsKey(token));
                }
                else
                {
                    wa.RemoveAttribute(QuestQuestionStateUtil.SuccessActionsKey(token));
                    wa.MarkPathDirty(QuestQuestionStateUtil.SuccessActionsKey(token));
                }

                if (!string.IsNullOrWhiteSpace(failActions))
                {
                    wa.SetString(QuestQuestionStateUtil.FailActionsKey(token), failActions);
                    wa.MarkPathDirty(QuestQuestionStateUtil.FailActionsKey(token));
                }
                else
                {
                    wa.RemoveAttribute(QuestQuestionStateUtil.FailActionsKey(token));
                    wa.MarkPathDirty(QuestQuestionStateUtil.FailActionsKey(token));
                }

                if (!string.IsNullOrWhiteSpace(message?.questId))
                {
                    wa.SetString(QuestQuestionStateUtil.QuestIdKey(token), message.questId);
                    wa.MarkPathDirty(QuestQuestionStateUtil.QuestIdKey(token));
                }
                else
                {
                    wa.RemoveAttribute(QuestQuestionStateUtil.QuestIdKey(token));
                    wa.MarkPathDirty(QuestQuestionStateUtil.QuestIdKey(token));
                }

                wa.SetLong(QuestQuestionStateUtil.QuestGiverIdKey(token), message?.questGiverId ?? 0L);
                wa.MarkPathDirty(QuestQuestionStateUtil.QuestGiverIdKey(token));
            }

            sapi.Network.GetChannel("alegacyvsquest").SendPacket(new ShowQuestQuestionMessage
            {
                Token = token,
                TitleLangKey = titleLangKey,
                TextLangKey = textLangKey,
                OptionLangKeys = optionKeys.ToArray()
            }, byPlayer);
        }
    }
}
