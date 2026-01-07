using System;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using vsquest.src.Systems.Actions;

namespace VsQuest
{
    public static class QuestActionHandlers
    {
        public static void SetQuestGiverAttribute(ICoreServerAPI api, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (args.Length < 3) throw new QuestException("The 'setquestgiverattribute' action requires 3 arguments: key, type, value.");

            var entity = api.World.GetEntityById(message.questGiverId);
            if (entity == null) return;

            string key = args[0];
            string type = args[1].ToLowerInvariant();
            string value = args[2];

            if (type == "bool")
            {
                entity.WatchedAttributes.SetBool(key, value == "true" || value == "1");
            }
            else if (type == "int")
            {
                entity.WatchedAttributes.SetInt(key, int.Parse(value));
            }
            else if (type == "string")
            {
                entity.WatchedAttributes.SetString(key, value);
            }
            else
            {
                throw new QuestException("The 'setquestgiverattribute' action type must be one of: bool, int, string.");
            }

            entity.WatchedAttributes.MarkPathDirty(key);
        }

        public static void Notify(ICoreServerAPI api, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (args.Length < 1) throw new QuestException("The 'notify' action requires 1 argument: message.");
            api.Network.GetChannel("vsquest").SendPacket(new ShowNotificationMessage() { Notification = args[0] }, byPlayer);
        }

        public static void ShowQuestFinalDialog(ICoreServerAPI api, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (args.Length < 2) throw new QuestException("The 'showquestfinaldialog' action requires at least 2 arguments: titleLangKey, textLangKey.");

            api.Network.GetChannel("vsquest").SendPacket(new ShowQuestDialogMessage()
            {
                TitleLangKey = args[0],
                TextLangKey = args[1],
                Option1LangKey = args.Length >= 3 ? args[2] : null,
                Option2LangKey = args.Length >= 4 ? args[3] : null
            }, byPlayer);
        }
    }
}
