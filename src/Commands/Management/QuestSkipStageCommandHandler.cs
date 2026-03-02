using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class QuestSkipStageCommandHandler
    {
        private readonly ICoreServerAPI sapi;
        private readonly QuestSystem questSystem;

        public QuestSkipStageCommandHandler(ICoreServerAPI sapi, QuestSystem questSystem)
        {
            this.sapi = sapi;
            this.questSystem = questSystem;
        }

        public TextCommandResult Handle(TextCommandCallingArgs args)
        {
            string playerName = (string)args[0];
            string questId = (string)args[1];

            IServerPlayer target;

            // Determine target player
            if (string.IsNullOrWhiteSpace(playerName))
            {
                target = args.Caller?.Player as IServerPlayer;
                if (target == null)
                {
                    return TextCommandResult.Error("No player specified and command caller is not a player.");
                }
            }
            else
            {
                target = sapi.World.AllOnlinePlayers
                    .FirstOrDefault(p => p.PlayerName.Equals(playerName, StringComparison.OrdinalIgnoreCase)) as IServerPlayer;

                if (target == null)
                {
                    return TextCommandResult.Error($"Player '{playerName}' not found online.");
                }
            }

            // Get player's active quests
            var activeQuests = questSystem.GetPlayerQuests(target.PlayerUID);
            if (activeQuests == null || activeQuests.Count == 0)
            {
                return TextCommandResult.Error($"Player '{target.PlayerName}' has no active quests.");
            }

            ActiveQuest activeQuest = null;

            // Determine which quest to skip stage for
            if (string.IsNullOrWhiteSpace(questId))
            {
                // Use currently active quest (first in list)
                activeQuest = activeQuests.FirstOrDefault();
                if (activeQuest == null)
                {
                    return TextCommandResult.Error($"Player '{target.PlayerName}' has no active quest.");
                }
                questId = activeQuest.questId;
            }
            else
            {
                // Find specific quest by ID
                activeQuest = activeQuests.FirstOrDefault(q => q.questId.Equals(questId, StringComparison.OrdinalIgnoreCase));
                if (activeQuest == null)
                {
                    return TextCommandResult.Error($"Player '{target.PlayerName}' does not have quest '{questId}' active.");
                }
            }

            // Get quest definition
            if (!questSystem.QuestRegistry.TryGetValue(questId, out var questDef) || questDef == null)
            {
                return TextCommandResult.Error($"Quest '{questId}' not found in registry.");
            }

            // Check if quest has stages
            if (questDef.stages == null || questDef.stages.Count == 0)
            {
                return TextCommandResult.Error($"Quest '{questId}' has no stages.");
            }

            int currentStage = activeQuest.currentStageIndex;
            int totalStages = questDef.stages.Count;

            // Check if already at last stage
            if (currentStage >= totalStages - 1)
            {
                return TextCommandResult.Error($"Quest '{questId}' is already at the last stage ({currentStage + 1}/{totalStages}).");
            }

            // Skip to next stage
            int newStage = currentStage + 1;
            activeQuest.currentStageIndex = newStage;

            // Execute onStageCompleteActions for the current stage
            var currentStageDef = questDef.stages[currentStage];
            if (currentStageDef.onStageCompleteActions != null && currentStageDef.onStageCompleteActions.Count > 0)
            {
                foreach (var action in currentStageDef.onStageCompleteActions)
                {
                    try
                    {
                        if (questSystem.ActionRegistry.TryGetValue(action.id, out var actionImpl))
                        {
                            actionImpl.Execute(sapi, null, target, action.args);
                        }
                    }
                    catch (Exception ex)
                    {
                        sapi.Logger.Error($"[QuestSkipStage] Error executing onStageCompleteAction '{action.id}': {ex.Message}");
                    }
                }
            }

            // Mark player data as dirty
            questSystem.SavePlayerQuests(target.PlayerUID, activeQuests);

            return TextCommandResult.Success($"Skipped stage {currentStage + 1} -> {newStage + 1} for quest '{questId}' for player '{target.PlayerName}'.");
        }
    }
}
