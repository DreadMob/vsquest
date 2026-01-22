using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.API.Datastructures;

namespace VsQuest
{
    public class QuestSystem : ModSystem
    {
        public Dictionary<string, Quest> QuestRegistry { get; private set; } = new Dictionary<string, Quest>();
        public Dictionary<string, IQuestAction> ActionRegistry { get; private set; } = new Dictionary<string, IQuestAction>();
        public Dictionary<string, ActionObjectiveBase> ActionObjectiveRegistry { get; private set; } = new Dictionary<string, ActionObjectiveBase>();

        private QuestPersistenceManager persistenceManager;
        private QuestLifecycleManager lifecycleManager;
        private QuestEventHandler eventHandler;
        private QuestActionRegistry actionRegistry;
        private QuestObjectiveRegistry objectiveRegistry;
        private QuestNetworkChannelRegistry networkChannelRegistry;
        private QuestChatCommandRegistry chatCommandRegistry;

        public QuestConfig Config { get; set; }
        private ICoreAPI api;

        private VsQuestDiscoveryHud discoveryHud;
        private QuestJournalHotkeyHandler journalHotkeyHandler;
        private QuestSelectGuiManager questSelectGuiManager;
        private QuestNotificationHandler notificationHandler;

        public override void StartPre(ICoreAPI api)
        {
            this.api = api;
            base.StartPre(api);
            ModClassRegistry.RegisterAll(api);
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            MobLocalizationUtils.LoadFromAssets(api);

            var harmony = new HarmonyLib.Harmony("alegacyvsquest");
            harmony.PatchAll();

            LocalizationUtils.LoadFromAssets(api);

            VsQuest.Harmony.EntityInteractPatch.TryPatch(harmony);

            objectiveRegistry = new QuestObjectiveRegistry(ActionObjectiveRegistry, api);
            objectiveRegistry.Register();

            networkChannelRegistry = new QuestNetworkChannelRegistry(this);

            try
            {
                Config = api.LoadModConfig<QuestConfig>("questconfig.json");
                if (Config != null)
                {
                    api.Logger.Notification("Mod Config successfully loaded.");
                }
                else
                {
                    api.Logger.Notification("No Mod Config specified. Falling back to default settings");
                    Config = new QuestConfig();
                }
            }
            catch
            {
                Config = new QuestConfig();
                api.Logger.Error("Failed to load custom mod configuration. Falling back to default settings!");
            }
            finally
            {
                api.StoreModConfig(Config, "questconfig.json");
            }
        }

        public override void StartClientSide(ICoreClientAPI capi)
        {
            base.StartClientSide(capi);

            if (networkChannelRegistry == null)
            {
                capi.Logger.Error("[alegacyvsquest] networkChannelRegistry was null in StartClientSide(). Recreating it (mod may have had an earlier startup error).");
                networkChannelRegistry = new QuestNetworkChannelRegistry(this);
            }

            networkChannelRegistry.RegisterClient(capi);

            journalHotkeyHandler = new QuestJournalHotkeyHandler(capi);
            journalHotkeyHandler.Register();

            capi.RegisterVtmlTagConverter("qhover", (clientApi, token, fontStack, onClick) =>
            {
                if (token == null) return null;

                string displayText = token.ContentText;
                string hoverText = null;
                if (token.Attributes != null && token.Attributes.TryGetValue("text", out var attrText))
                {
                    hoverText = attrText;
                }

                if (string.IsNullOrWhiteSpace(hoverText))
                {
                    return new RichTextComponent(clientApi, displayText, fontStack.Peek());
                }

                return new RichTextComponentQuestHover(clientApi, displayText, hoverText, fontStack.Peek());
            });

            try
            {
                discoveryHud = new VsQuestDiscoveryHud(capi);
            }
            catch
            {
                discoveryHud = null;
            }

            notificationHandler = new QuestNotificationHandler(discoveryHud);
            questSelectGuiManager = new QuestSelectGuiManager(Config);
        }

        public override void StartServerSide(ICoreServerAPI sapi)
        {
            base.StartServerSide(sapi);

            sapi.Logger.VerboseDebug($"[alegacyvsquest] QuestSystem.StartServerSide loaded ({DateTime.UtcNow:O})");

            persistenceManager = new QuestPersistenceManager(sapi);
            lifecycleManager = new QuestLifecycleManager(QuestRegistry, ActionRegistry, api);
            eventHandler = new QuestEventHandler(QuestRegistry, persistenceManager, sapi);

            if (networkChannelRegistry == null)
            {
                sapi.Logger.Error("[alegacyvsquest] networkChannelRegistry was null in StartServerSide(). Recreating it (mod may have had an earlier startup error).");
                networkChannelRegistry = new QuestNetworkChannelRegistry(this);
            }

            networkChannelRegistry.RegisterServer(sapi);

            actionRegistry = new QuestActionRegistry(ActionRegistry, api, sapi, OnQuestAccepted);
            actionRegistry.Register();

            eventHandler.RegisterEventHandlers();

            chatCommandRegistry = new QuestChatCommandRegistry(sapi, api, this);
            chatCommandRegistry.Register();
        }

        public override void AssetsLoaded(ICoreAPI api)
        {
            base.AssetsLoaded(api);

            MobLocalizationUtils.LoadFromAssets(api);

            LocalizationUtils.LoadFromAssets(api);
            foreach (var mod in api.ModLoader.Mods)
            {
                var questAssets = api.Assets.GetMany<Quest>(api.Logger, "config/quests", mod.Info.ModID);
                foreach (var questAsset in questAssets)
                {
                    try
                    {
                        TryRegisterQuest(api, questAsset.Value, questAsset.Key);
                    }
                    catch (Exception e)
                    {
                        api.Logger.Error($"Failed to load quest from {questAsset.Key}: {e.Message}");
                    }
                }

                LoadQuestAssetsFromFile(api, mod.Info.ModID);
            }
        }

        private void LoadQuestAssetsFromFile(ICoreAPI api, string domain)
        {
            if (api == null || string.IsNullOrWhiteSpace(domain)) return;

            var assets = api.Assets;
            var asset = assets.TryGet(new AssetLocation(domain, "config/quests.json"))
                ?? assets.TryGet(new AssetLocation(domain, "config/quest.json"));

            if (asset == null) return;

            try
            {
                var root = asset.ToObject<JsonObject>();
                if (root == null) return;

                if (root.IsArray())
                {
                    var array = root.AsArray();
                    if (array == null) return;

                    foreach (var entry in array)
                    {
                        if (entry == null || !entry.Exists) continue;
                        var quest = entry.AsObject<Quest>();
                        TryRegisterQuest(api, quest, asset.Location?.ToString() ?? "config/quests.json");
                    }

                    return;
                }

                var singleQuest = root.AsObject<Quest>();
                TryRegisterQuest(api, singleQuest, asset.Location?.ToString() ?? "config/quests.json");
            }
            catch (Exception e)
            {
                api.Logger.Error($"Failed to load quests from {asset.Location}: {e.Message}");
            }
        }

        private void TryRegisterQuest(ICoreAPI api, Quest quest, string source)
        {
            if (quest == null) return;
            if (string.IsNullOrWhiteSpace(quest.id)) return;

            if (QuestRegistry.ContainsKey(quest.id)) return;

            QuestRegistry.Add(quest.id, quest);
        }

        public List<ActiveQuest> GetPlayerQuests(string playerUID)
        {
            var quests = persistenceManager.GetPlayerQuests(playerUID);
            if (quests == null || quests.Count == 0) return quests;

            bool changed = false;
            foreach (var quest in quests)
            {
                if (quest == null || string.IsNullOrWhiteSpace(quest.questId)) continue;

                string normalized = QuestJournalMigration.NormalizeQuestId(quest.questId, QuestRegistry);
                if (!string.Equals(normalized, quest.questId, StringComparison.OrdinalIgnoreCase))
                {
                    quest.questId = normalized;
                    changed = true;
                }
            }

            if (changed)
            {
                persistenceManager.SavePlayerQuests(playerUID, quests);
            }

            return quests;
        }

        public void SavePlayerQuests(string playerUID, List<ActiveQuest> activeQuests)
        {
            persistenceManager.SavePlayerQuests(playerUID, activeQuests);
        }

        internal string NormalizeQuestId(string questId)
        {
            return QuestJournalMigration.NormalizeQuestId(questId, QuestRegistry);
        }

        internal string[] GetNormalizedCompletedQuestIds(IPlayer player)
        {
            return QuestJournalMigration.GetNormalizedCompletedQuestIds(player, QuestRegistry);
        }

        internal bool ForceCompleteQuestInternal(IServerPlayer player, QuestCompletedMessage message, ICoreServerAPI sapi)
        {
            return lifecycleManager.ForceCompleteQuest(player, message, sapi, GetPlayerQuests);
        }

        internal void OnQuestAccepted(IServerPlayer fromPlayer, QuestAcceptedMessage message, ICoreServerAPI sapi)
        {
            lifecycleManager.OnQuestAccepted(fromPlayer, message, sapi, GetPlayerQuests);
        }

        internal void OnQuestCompleted(IServerPlayer fromPlayer, QuestCompletedMessage message, ICoreServerAPI sapi)
        {
            lifecycleManager.OnQuestCompleted(fromPlayer, message, sapi, GetPlayerQuests);
        }

        internal void OnQuestInfoMessage(QuestInfoMessage message, ICoreClientAPI capi)
        {
            questSelectGuiManager.HandleQuestInfoMessage(message, capi);
        }

        internal void OnShowNotificationMessage(ShowNotificationMessage message, ICoreClientAPI capi)
        {
            notificationHandler.HandleNotificationMessage(message, capi);
        }

        internal void OnShowDiscoveryMessage(ShowDiscoveryMessage message, ICoreClientAPI capi)
        {
            notificationHandler.HandleDiscoveryMessage(message, capi);
        }

        internal void OnExecutePlayerCommand(ExecutePlayerCommandMessage message, ICoreClientAPI capi)
        {
            ClientCommandExecutor.Execute(message, capi);
        }

        internal void OnVanillaBlockInteract(IServerPlayer player, VanillaBlockInteractMessage message, ICoreServerAPI sapi)
        {
            eventHandler.HandleVanillaBlockInteract(player, message);
        }

        internal void OnShowQuestDialogMessage(ShowQuestDialogMessage message, ICoreClientAPI capi)
        {
            QuestFinalDialogGui.ShowFromMessage(message, capi);
        }

        internal void OnShowQuestQuestionMessage(ShowQuestQuestionMessage message, ICoreClientAPI capi)
        {
            QuestQuestionDialogGui.ShowFromMessage(message, capi);
        }

        internal void OnPreloadBossMusicMessage(PreloadBossMusicMessage message, ICoreClientAPI capi)
        {
            try
            {
                var sys = capi?.ModLoader?.GetModSystem<BossMusicUrlSystem>();
                sys?.Preload(message?.Url);
            }
            catch
            {
            }
        }

        internal void OnQuestQuestionAnswerMessage(IServerPlayer player, QuestQuestionAnswerMessage message, ICoreServerAPI sapi)
        {
            if (sapi == null || player == null || message == null) return;

            var wa = player.Entity?.WatchedAttributes;
            if (wa == null) return;

            string token = message.Token;
            if (string.IsNullOrWhiteSpace(token)) return;

            string answeredKey = QuestQuestionStateUtil.AnsweredKey(token);
            if (wa.GetBool(answeredKey, false)) return;

            int correctIndex = wa.GetInt(QuestQuestionStateUtil.CorrectIndexKey(token), -1);
            bool isCorrect = message.SelectedIndex == correctIndex;

            wa.SetBool(answeredKey, true);
            wa.MarkPathDirty(answeredKey);

            wa.SetBool(QuestQuestionStateUtil.CorrectKey(token), isCorrect);
            wa.MarkPathDirty(QuestQuestionStateUtil.CorrectKey(token));

            string correctValueKey = QuestQuestionStateUtil.CorrectValueKey(token);
            if (wa.GetInt(correctValueKey, 0) != (isCorrect ? 1 : 0))
            {
                wa.SetInt(correctValueKey, isCorrect ? 1 : 0);
                wa.MarkPathDirty(correctValueKey);
            }

            string correctStringKey = QuestQuestionStateUtil.CorrectStringKey(token);
            string correctStringValue = isCorrect ? "1" : "0";
            if (wa.GetString(correctStringKey, null) != correctStringValue)
            {
                wa.SetString(correctStringKey, correctStringValue);
                wa.MarkPathDirty(correctStringKey);
            }

            string actionString = isCorrect
                ? wa.GetString(QuestQuestionStateUtil.SuccessActionsKey(token), null)
                : wa.GetString(QuestQuestionStateUtil.FailActionsKey(token), null);

            if (!string.IsNullOrWhiteSpace(actionString))
            {
                string questId = wa.GetString(QuestQuestionStateUtil.QuestIdKey(token), null);
                long questGiverId = wa.GetLong(QuestQuestionStateUtil.QuestGiverIdKey(token), 0L);

                var execMessage = new QuestAcceptedMessage
                {
                    questGiverId = questGiverId,
                    questId = string.IsNullOrWhiteSpace(questId) ? "dialog-action" : questId
                };

                ActionStringExecutor.Execute(sapi, execMessage, player, actionString);
            }
        }

        internal void OnClaimReputationRewardsMessage(IServerPlayer player, ClaimReputationRewardsMessage message, ICoreServerAPI sapi)
        {
            if (sapi == null || player == null || message == null) return;

            var repSystem = sapi.ModLoader.GetModSystem<ReputationSystem>();
            if (repSystem == null) return;

            if (!repSystem.TryResolveQuestGiverReputation(sapi, message.questGiverId, out string repNpcId, out string repFactionId))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(message.scope) || string.Equals(message.scope, "npc", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(repNpcId))
                {
                    repSystem.ClaimPendingRewards(sapi, player, ReputationScope.Npc, repNpcId);
                }
            }

            if (string.IsNullOrWhiteSpace(message.scope) || string.Equals(message.scope, "faction", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(repFactionId))
                {
                    repSystem.ClaimPendingRewards(sapi, player, ReputationScope.Faction, repFactionId);
                }
            }

            var questGiver = sapi.World.GetEntityById(message.questGiverId);
            var questGiverBehavior = questGiver?.GetBehavior<EntityBehaviorQuestGiver>();
            if (questGiverBehavior != null && player.Entity is EntityPlayer entityPlayer)
            {
                questGiverBehavior.SendQuestInfoMessageToClient(sapi, entityPlayer);
            }
        }
    }
}