using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
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
            {
                if (args.Length < 1) throw new QuestException("The 'playsound' action requires at least 1 argument: soundLocation.");

                var sound = new AssetLocation(args[0]);
                if (args.Length < 2)
                {
                    api.World.PlaySoundFor(sound, byPlayer);
                    return;
                }

                if (!float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float volume))
                {
                    volume = 1f;
                }

                var world = api.World;

                // Find an overload that explicitly has a parameter named "volume" to avoid accidentally mapping to pitch.
                var methods = world.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.Name == "PlaySoundFor")
                    .ToArray();

                foreach (var m in methods)
                {
                    var ps = m.GetParameters();
                    if (ps.Length < 2) continue;
                    if (ps[0].ParameterType != typeof(AssetLocation)) continue;
                    if (!ps[1].ParameterType.IsInstanceOfType(byPlayer)) continue;

                    int volumeIndex = -1;
                    for (int i = 0; i < ps.Length; i++)
                    {
                        if (ps[i].ParameterType == typeof(float) && string.Equals(ps[i].Name, "volume", StringComparison.OrdinalIgnoreCase))
                        {
                            volumeIndex = i;
                            break;
                        }
                    }
                    if (volumeIndex == -1) continue;

                    var invokeArgs = new object[ps.Length];
                    invokeArgs[0] = sound;
                    invokeArgs[1] = byPlayer;

                    for (int i = 2; i < ps.Length; i++)
                    {
                        if (i == volumeIndex)
                        {
                            invokeArgs[i] = volume;
                            continue;
                        }

                        invokeArgs[i] = ps[i].HasDefaultValue
                            ? ps[i].DefaultValue
                            : (ps[i].ParameterType.IsValueType ? Activator.CreateInstance(ps[i].ParameterType) : null);
                    }

                    m.Invoke(world, invokeArgs);
                    return;
                }

                // Fallback: no suitable overload available.
                world.PlaySoundFor(sound, byPlayer);
            });
            
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

            actionRegistry.Add("setquestgiverattribute", SetQuestGiverAttributeAction.Execute);
            actionRegistry.Add("notify", NotifyAction.Execute);
            actionRegistry.Add("showquestfinaldialog", ShowQuestFinalDialogAction.Execute);
        }
    }
}
