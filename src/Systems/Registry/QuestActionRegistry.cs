using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using vsquest.src.Systems.Actions;

namespace VsQuest
{
    public class QuestActionRegistry
    {
        private readonly Dictionary<string, QuestAction> actionRegistry;
        private readonly ICoreAPI api;

        public QuestActionRegistry(Dictionary<string, QuestAction> actionRegistry, ICoreAPI api)
        {
            this.actionRegistry = actionRegistry;
            this.api = api;
        }

        public void RegisterActions(ICoreServerAPI sapi, Action<IServerPlayer, QuestAcceptedMessage, ICoreServerAPI> onQuestAcceptedCallback)
        {
            actionRegistry.Add("despawnquestgiver", (api, message, byPlayer, args) => 
                api.World.RegisterCallback(dt => api.World.GetEntityById(message.questGiverId).Die(EnumDespawnReason.Removed), int.Parse(args[0])));
            
            actionRegistry.Add("playsound", (api, message, byPlayer, args) => 
                api.World.PlaySoundFor(new AssetLocation(args[0]), byPlayer));
            
            actionRegistry.Add("spawnentities", ActionUtil.SpawnEntities);
            actionRegistry.Add("spawnany", ActionUtil.SpawnAnyOfEntities);
            actionRegistry.Add("spawnsmoke", ActionUtil.SpawnSmoke);
            actionRegistry.Add("recruitentity", ActionUtil.RecruitEntity);
            
            actionRegistry.Add("healplayer", (api, message, byPlayer, args) => 
                byPlayer.Entity.ReceiveDamage(new DamageSource() { Type = EnumDamageType.Heal }, 100));
            
            actionRegistry.Add("addplayerattribute", (api, message, byPlayer, args) => 
                byPlayer.Entity.WatchedAttributes.SetString(args[0], args[1]));
            
            actionRegistry.Add("removeplayerattribute", (api, message, byPlayer, args) => 
                byPlayer.Entity.WatchedAttributes.RemoveAttribute(args[0]));
            
            actionRegistry.Add("completequest", ActionUtil.CompleteQuest);
            
            actionRegistry.Add("acceptquest", (api, message, byPlayer, args) => 
                onQuestAcceptedCallback(byPlayer, new QuestAcceptedMessage() { questGiverId = long.Parse(args[0]), questId = args[1] }, sapi));
            
            actionRegistry.Add("giveitem", ActionUtil.GiveItem);
            actionRegistry.Add("addtraits", ActionUtil.AddTraits);
            actionRegistry.Add("removetraits", ActionUtil.RemoveTraits);
            actionRegistry.Add("servercommand", ActionUtil.ServerCommand);
            actionRegistry.Add("playercommand", ActionUtil.PlayerCommand);
            actionRegistry.Add("giveactionitem", ActionUtil.GiveActionItem);

            actionRegistry.Add("setquestgiverattribute", (api, message, byPlayer, args) =>
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
            });

            actionRegistry.Add("notify", (api, message, byPlayer, args) =>
            {
                if (args.Length < 1) throw new QuestException("The 'notify' action requires 1 argument: message.");
                api.Network.GetChannel("vsquest").SendPacket(new ShowNotificationMessage() { Notification = args[0] }, byPlayer);
            });

            actionRegistry.Add("showquestfinaldialog", (api, message, byPlayer, args) =>
            {
                if (args.Length < 2) throw new QuestException("The 'showquestfinaldialog' action requires at least 2 arguments: titleLangKey, textLangKey.");
                api.Network.GetChannel("vsquest").SendPacket(new ShowQuestDialogMessage()
                {
                    TitleLangKey = args[0],
                    TextLangKey = args[1],
                    Option1LangKey = args.Length >= 4 ? args[2] : null,
                    Option2LangKey = args.Length >= 4 ? args[3] : null
                }, byPlayer);
            });
        }
    }
}
