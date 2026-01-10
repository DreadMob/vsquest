using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class QuestActionRegistry : IQuestActionRegistry
    {
        private readonly Dictionary<string, IQuestAction> actionRegistry;
        private readonly ICoreAPI api;

        public QuestActionRegistry(Dictionary<string, IQuestAction> actionRegistry, ICoreAPI api)
        {
            this.actionRegistry = actionRegistry;
            this.api = api;
        }

        public void RegisterActions(ICoreServerAPI sapi, Action<IServerPlayer, QuestAcceptedMessage, ICoreServerAPI> onQuestAcceptedCallback)
        {
            actionRegistry.Add("despawnquestgiver", new DelegateQuestAction((api, message, byPlayer, args) =>
                api.World.RegisterCallback(dt => api.World.GetEntityById(message.questGiverId).Die(EnumDespawnReason.Removed), int.Parse(args[0]))));
            
            actionRegistry.Add("playsound", new DelegateQuestAction(PlaySoundAction.Execute));
            
            actionRegistry.Add("spawnentities", new DelegateQuestAction(SpawnActions.SpawnEntities));
            actionRegistry.Add("spawnany", new DelegateQuestAction(SpawnActions.SpawnAnyOfEntities));
            actionRegistry.Add("spawnsmoke", new DelegateQuestAction(SpawnActions.SpawnSmoke));
            actionRegistry.Add("recruitentity", new DelegateQuestAction(SpawnActions.RecruitEntity));
            
            actionRegistry.Add("healplayer", new DelegateQuestAction((api, message, byPlayer, args) =>
                byPlayer.Entity.ReceiveDamage(new DamageSource() { Type = EnumDamageType.Heal }, 100)));
            
            actionRegistry.Add("addplayerattribute", new DelegateQuestAction((api, message, byPlayer, args) =>
                byPlayer.Entity.WatchedAttributes.SetString(args[0], args[1])));
            
            actionRegistry.Add("removeplayerattribute", new DelegateQuestAction((api, message, byPlayer, args) =>
                byPlayer.Entity.WatchedAttributes.RemoveAttribute(args[0])));
            
            actionRegistry.Add("completequest", new DelegateQuestAction(QuestLifecycleActions.CompleteQuest));
            
            actionRegistry.Add("acceptquest", new DelegateQuestAction((api, message, byPlayer, args) =>
                onQuestAcceptedCallback(byPlayer, new QuestAcceptedMessage() { questGiverId = long.Parse(args[0]), questId = args[1] }, sapi)));
            
            actionRegistry.Add("giveitem", new DelegateQuestAction(ItemActions.GiveItem));
            actionRegistry.Add("addtraits", new DelegateQuestAction(TraitActions.AddTraits));
            actionRegistry.Add("removetraits", new DelegateQuestAction(TraitActions.RemoveTraits));
            actionRegistry.Add("servercommand", new DelegateQuestAction(CommandActions.ServerCommand));
            actionRegistry.Add("playercommand", new DelegateQuestAction(CommandActions.PlayerCommand));
            actionRegistry.Add("questitem", new DelegateQuestAction(ItemActions.GiveActionItem));

            actionRegistry.Add("allowcharselonce", new DelegateQuestAction((api, message, byPlayer, args) =>
            {
                byPlayer?.Entity?.WatchedAttributes?.SetBool("allowcharselonce", true);
                byPlayer?.Entity?.WatchedAttributes?.MarkPathDirty("allowcharselonce");
            }));

            actionRegistry.Add("randomkill", new DelegateQuestAction(RandomKillAction.Execute));

            actionRegistry.Add("resetwalkdistance", new DelegateQuestAction(ResetWalkDistanceAction.Execute));

            actionRegistry.Add("setquestgiverattribute", new DelegateQuestAction(SetQuestGiverAttributeAction.Execute));
            actionRegistry.Add("notify", new DelegateQuestAction(NotifyAction.Execute));
            actionRegistry.Add("showquestfinaldialog", new DelegateQuestAction(ShowQuestFinalDialogAction.Execute));
        }
    }
}
