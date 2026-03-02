using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Config;

namespace VsQuest
{
    public static class QuestInteractAtUtil
    {
        public static string InteractionKey(int x, int y, int z) => $"interactat_{x}_{y}_{z}";

        /// <summary>
        /// Checks if player is holding the required item in active hotbar slot
        /// </summary>
        private static bool IsHoldingItem(IServerPlayer player, string requiredCodeOrActionItemId)
        {
            if (player?.InventoryManager == null) return false;
            if (string.IsNullOrWhiteSpace(requiredCodeOrActionItemId)) return true;

            var slot = player.InventoryManager.ActiveHotbarSlot;
            if (slot?.Itemstack == null)
            {
                player.Entity.Api.Logger.Debug($"[IsHoldingItem] No item in active slot");
                return false;
            }

            player.Entity.Api.Logger.Debug($"[IsHoldingItem] Checking for '{requiredCodeOrActionItemId}', holding: {slot.Itemstack.Item?.Code}");

            // 1) Vanilla item code check (e.g. game:amethyst)
            if (slot.Itemstack.Item?.Code != null
                && slot.Itemstack.Item.Code.ToString().Equals(requiredCodeOrActionItemId, StringComparison.OrdinalIgnoreCase))
            {
                player.Entity.Api.Logger.Debug($"[IsHoldingItem] Match by item code");
                return true;
            }

            // 2) Action item id check (e.g. alstory-keepers-key)
            string actionItemId = slot.Itemstack.Attributes?.GetString(ItemAttributeUtils.ActionItemIdKey);
            player.Entity.Api.Logger.Debug($"[IsHoldingItem] Action item ID: '{actionItemId}'");
            
            if (!string.IsNullOrWhiteSpace(actionItemId)
                && actionItemId.Equals(requiredCodeOrActionItemId, StringComparison.OrdinalIgnoreCase))
            {
                player.Entity.Api.Logger.Debug($"[IsHoldingItem] Match by action item ID");
                return true;
            }

            player.Entity.Api.Logger.Debug($"[IsHoldingItem] No match");
            return false;
        }

        public static bool TryParsePos(string coordString, out int x, out int y, out int z)
        {
            x = y = z = 0;
            if (string.IsNullOrWhiteSpace(coordString)) return false;

            var coords = coordString.Split(',');
            if (coords.Length != 3) return false;

            return int.TryParse(coords[0], out x)
                && int.TryParse(coords[1], out y)
                && int.TryParse(coords[2], out z);
        }

        public static string[] GetCompletedInteractions(IPlayer player)
        {
            var wa = player?.Entity?.WatchedAttributes;
            if (wa == null) return Array.Empty<string>();

            string completedInteractions = wa.GetString("completedInteractions", "");
            return completedInteractions.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        }

        public static bool HasInteraction(IPlayer player, int x, int y, int z)
        {
            var completed = GetCompletedInteractions(player);
            return completed.Contains(InteractionKey(x, y, z));
        }

        public static int CountCompleted(IPlayer player, string[] coordArgs)
        {
            if (player?.Entity?.WatchedAttributes == null) return 0;
            if (coordArgs == null || coordArgs.Length == 0) return 0;

            var completed = GetCompletedInteractions(player);
            int count = 0;

            foreach (var coordString in coordArgs)
            {
                if (!TryParsePos(coordString, out int x, out int y, out int z)) continue;
                if (completed.Contains(InteractionKey(x, y, z))) count++;
            }

            return count;
        }

        public static string[] GetCompletedInteractions(IServerPlayer serverPlayer)
        {
            var wa = serverPlayer?.Entity?.WatchedAttributes;
            if (wa == null) return Array.Empty<string>();

            string completedInteractions = wa.GetString("completedInteractions", "");
            return completedInteractions.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        }

        public static int CountCompleted(IServerPlayer serverPlayer, string[] coordArgs)
        {
            if (serverPlayer?.Entity?.WatchedAttributes == null) return 0;
            if (coordArgs == null || coordArgs.Length == 0) return 0;

            var completed = GetCompletedInteractions(serverPlayer);
            int count = 0;

            foreach (var coordString in coordArgs)
            {
                if (!TryParsePos(coordString, out int x, out int y, out int z)) continue;
                if (completed.Contains(InteractionKey(x, y, z))) count++;
            }

            return count;
        }

        public static bool HasInteraction(IServerPlayer serverPlayer, int x, int y, int z)
        {
            var completed = GetCompletedInteractions(serverPlayer);
            return completed.Contains(InteractionKey(x, y, z));
        }

        public static bool TryMarkInteraction(IServerPlayer serverPlayer, int x, int y, int z)
        {
            var wa = serverPlayer?.Entity?.WatchedAttributes;
            if (wa == null) return false;

            string interactionKey = InteractionKey(x, y, z);
            string completedInteractions = wa.GetString("completedInteractions", "");
            var completed = completedInteractions.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();

            if (completed.Contains(interactionKey)) return false;

            completed.Add(interactionKey);
            wa.SetString("completedInteractions", string.Join(",", completed));
            wa.MarkPathDirty("completedInteractions");
            return true;
        }

        public static void ResetCompletedInteractAtObjectives(Quest quest, IServerPlayer serverPlayer)
        {
            if (serverPlayer?.Entity?.WatchedAttributes == null) return;

            var wa = serverPlayer.Entity.WatchedAttributes;

            try
            {
                // Simply clear all completedInteractions for this quest
                // This is more reliable than trying to parse and match coordinates
                wa.SetString("completedInteractions", "");
                wa.MarkPathDirty("completedInteractions");
            }
            catch
            {
            }
        }

                public static void TryHandleInteractAtObjectives(Quest quest, ActiveQuest activeQuest, IServerPlayer serverPlayer, int[] position, ICoreServerAPI sapi)
        {
            if (serverPlayer?.Entity?.WatchedAttributes == null) return;
            if (position == null || position.Length != 3) return;

            // Get action objectives from current stage using centralized method
            var actionObjectives = quest?.GetActionObjectives(activeQuest.currentStageIndex);
            
            if (actionObjectives == null || actionObjectives.Count == 0) return;

            var wa = serverPlayer.Entity.WatchedAttributes;
            bool anyChanged = false;

            sapi.Logger.Debug($"[QuestInteractAt] Player {serverPlayer.PlayerName} interacted at {position[0]},{position[1]},{position[2]}");
            sapi.Logger.Debug($"[QuestInteractAt] Quest: {quest?.id}, Stage: {activeQuest.currentStageIndex}, ActionObjectives count: {actionObjectives.Count}");

            for (int i = 0; i < actionObjectives.Count; i++)
            {
                var ao = actionObjectives[i];
                if (ao?.id != "interactat" || ao.args == null || ao.args.Length < 1) continue;

                var coordString = ao.args[0];
                if (string.IsNullOrWhiteSpace(coordString)) continue;

                // Support multiple coordinates separated by | for multiblock structures
                var coords = coordString.Split('|');
                bool matchedAnyCoord = false;
                int matchedX = 0, matchedY = 0, matchedZ = 0;

                foreach (var coord in coords)
                {
                    if (string.IsNullOrWhiteSpace(coord)) continue;
                    if (!TryParsePos(coord.Trim(), out int targetX, out int targetY, out int targetZ)) continue;

                    if (position[0] == targetX && position[1] == targetY && position[2] == targetZ)
                    {
                        matchedAnyCoord = true;
                        matchedX = targetX;
                        matchedY = targetY;
                        matchedZ = targetZ;
                        break;
                    }
                }

                if (!matchedAnyCoord) continue;

                // Check required item if specified (args[1] = item code)
                string requiredItem = ao.args.Length >= 2 ? ao.args[1] : null;
                
                sapi.Logger.Debug($"[QuestInteractAt] Position matches! Objective: {ao.objectiveId}, requiredItem: {requiredItem}");

                if (!string.IsNullOrWhiteSpace(requiredItem))
                {
                    sapi.Logger.Debug($"[QuestInteractAt] Required item: {requiredItem}");

                    if (!IsHoldingItem(serverPlayer, requiredItem))
                    {
                        sapi.Logger.Debug($"[QuestInteractAt] Player not holding required item: {requiredItem}");
                        sapi.SendMessage(serverPlayer, GlobalConstants.InfoLogChatGroup, 
                            Lang.Get("alegacyvsquest:interactat-hold-item", requiredItem), 
                            EnumChatType.Notification);
                        continue;
                    }
                    sapi.Logger.Debug($"[QuestInteractAt] Player is holding required item");
                }

                // Check if this interaction requires holding (args[2] = hold duration in seconds)
                double requiredHoldSeconds = 0;
                if (ao.args.Length >= 3 && double.TryParse(ao.args[2], out double holdDuration))
                {
                    requiredHoldSeconds = holdDuration;
                }

                // Get max click gap (args[3] = max seconds between clicks, default 1.0)
                double maxClickGapSeconds = 1.0;
                if (ao.args.Length >= 4 && double.TryParse(ao.args[3], out double clickGap))
                {
                    maxClickGapSeconds = clickGap;
                }

                if (requiredHoldSeconds > 0)
                {
                    // This interaction requires holding
                    // Use objectiveId as the hold key so all blocks in the same objective share one timer
                    string holdKey = !string.IsNullOrWhiteSpace(ao.objectiveId) 
                        ? ao.objectiveId 
                        : $"{matchedX}_{matchedY}_{matchedZ}";
                    
                    string holdStartKey = $"alegacyvsquest:hold_start:{activeQuest.questId}:{holdKey}";
                    string lastClickKey = $"alegacyvsquest:hold_lastclick:{activeQuest.questId}:{holdKey}";
                    string lastPosKey = $"alegacyvsquest:hold_lastpos:{activeQuest.questId}:{holdKey}";
                    
                    double nowHours = sapi.World.Calendar.TotalHours;
                    double holdStartHours = wa.GetDouble(holdStartKey, -1);
                    double lastClickHours = wa.GetDouble(lastClickKey, -1);
                    string lastPos = wa.GetString(lastPosKey, "");
                    string currentPosKey = $"{matchedX}_{matchedY}_{matchedZ}";
                    
                    // Check if too much time passed since last click
                    double maxClickGapHours = maxClickGapSeconds / 3600.0;
                    if (lastClickHours > 0 && (nowHours - lastClickHours) > maxClickGapHours)
                    {
                        sapi.Logger.Debug($"[QuestInteractAt] Too much time since last click ({(nowHours - lastClickHours) * 3600:F1}s > {maxClickGapSeconds}s) - resetting hold timer");
                        wa.RemoveAttribute(holdStartKey);
                        holdStartHours = -1;
                    }
                    
                    // Check if player clicked on a different block - reset progress
                    if (!string.IsNullOrEmpty(lastPos) && lastPos != currentPosKey)
                    {
                        sapi.Logger.Debug($"[QuestInteractAt] Player switched from {lastPos} to {currentPosKey} - resetting hold timer");
                        // Clear timer
                        wa.RemoveAttribute(holdStartKey);
                        holdStartHours = -1; // Force restart
                    }
                    
                    // Update last click time
                    wa.SetDouble(lastClickKey, nowHours);
                    wa.MarkPathDirty(lastClickKey);
                    
                    double requiredHoldHours = requiredHoldSeconds / 3600.0;

                    if (holdStartHours < 0)
                    {
                        // First click - start holding timer
                        sapi.Logger.Debug($"[QuestInteractAt] First click - starting hold timer for {holdKey} at position {currentPosKey} (duration: {requiredHoldSeconds}s, max gap: {maxClickGapSeconds}s)");
                        wa.SetDouble(holdStartKey, nowHours);
                        wa.SetString(lastPosKey, currentPosKey);
                        wa.MarkPathDirty(holdStartKey);
                        wa.MarkPathDirty(lastPosKey);
                        
                        // Play sound to indicate holding started
                        sapi.World.PlaySoundAt(new AssetLocation("game:sounds/effect/translocate-breakdimension"), matchedX, matchedY, matchedZ, null, false, 8, 0.5f);
                        
                        // Show lore message
                        string loreKey = $"{activeQuest.questId}-obj-{ao.objectiveId}-hold";
                        string loreMessage = Lang.GetIfExists(loreKey);
                        if (string.IsNullOrWhiteSpace(loreMessage))
                        {
                            loreMessage = "Удержание...";
                        }
                        
                        // Show colored message in chat
                        string coloredMessage = $"<font color=\"#9370DB\">{loreMessage}</font>";
                        sapi.SendMessage(serverPlayer, GlobalConstants.CurrentChatGroup, coloredMessage, EnumChatType.Notification);
                        
                        return;
                    }

                    double heldHours = nowHours - holdStartHours;
                    
                    if (heldHours < requiredHoldHours)
                    {
                        // Still holding, not enough time yet
                        sapi.Logger.Debug($"[QuestInteractAt] Holding in progress for {holdKey} at {currentPosKey}: {heldHours * 3600:F1}s / {requiredHoldSeconds}s");
                        float progress = (float)(heldHours / requiredHoldHours);
                        int progressPercent = (int)(progress * 100);
                        
                        // Play sound during holding
                        sapi.World.PlaySoundAt(new AssetLocation("game:sounds/effect/translocate-breakdimension"), matchedX, matchedY, matchedZ, null, false, 8, 0.3f);
                        
                        // Show lore message with progress
                        string loreKey = $"{activeQuest.questId}-obj-{ao.objectiveId}-hold";
                        string loreMessage = Lang.GetIfExists(loreKey);
                        if (string.IsNullOrWhiteSpace(loreMessage))
                        {
                            loreMessage = "Удержание";
                        }
                        
                        // Create progress bar using unicode blocks
                        int barLength = 20;
                        int filledLength = (int)(progress * barLength);
                        string progressBar = new string('█', filledLength) + new string('░', barLength - filledLength);
                        
                        // Color: purple for message, cyan for progress bar, yellow for percentage
                        string displayMessage = $"<font color=\"#9370DB\">{loreMessage}</font> <font color=\"#00CED1\">[{progressBar}]</font> <font color=\"#FFD700\">{progressPercent}%</font>";
                        
                        sapi.SendMessage(serverPlayer, GlobalConstants.CurrentChatGroup, displayMessage, EnumChatType.Notification);
                        
                        return;
                    }

                    // Held long enough - clear timer and continue to process interaction
                    sapi.Logger.Debug($"[QuestInteractAt] Hold completed for {holdKey}! Executing cooldownblock...");
                    wa.RemoveAttribute(holdStartKey);
                    wa.RemoveAttribute(lastPosKey);
                    wa.RemoveAttribute(lastClickKey);
                    
                    // Execute cooldownblock actions for all coordinates in this objective
                    // Parse coordinates from args[0]
                    var cooldownCoords = coordString.Split('|');
                    foreach (var cooldownCoord in cooldownCoords)
                    {
                        if (string.IsNullOrWhiteSpace(cooldownCoord)) continue;
                        if (!TryParsePos(cooldownCoord.Trim(), out int cx, out int cy, out int cz)) continue;
                        
                        sapi.Logger.Debug($"[QuestInteractAt] Executing cooldownblock for {cx},{cy},{cz}");
                        
                        // Execute cooldownblock action programmatically
                        try
                        {
                            var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();
                            if (questSystem?.ActionRegistry != null && questSystem.ActionRegistry.TryGetValue("cooldownblock", out var cooldownAction))
                            {
                                // Args: [delayHours, coordinates]
                                string[] cooldownArgs = new string[] { "2", $"{cx},{cy},{cz}" };
                                cooldownAction.Execute(sapi, null, serverPlayer, cooldownArgs);
                            }
                        }
                        catch (Exception ex)
                        {
                            sapi.Logger.Error($"[QuestInteractAt] Failed to execute cooldownblock: {ex.Message}");
                        }
                    }
                    
                    // Don't return here - let it fall through to mark interaction and fire completion
                }

                bool changed = TryMarkInteraction(serverPlayer, matchedX, matchedY, matchedZ);
                sapi.Logger.Debug($"[QuestInteractAt] TryMarkInteraction result: {changed}");
                
                if (!changed) continue;

                anyChanged = true;
                bool completableNow = true;
                try
                {
                    var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();
                    if (questSystem?.ActionObjectiveRegistry != null && questSystem.ActionObjectiveRegistry.TryGetValue("interactat", out var impl) && impl != null)
                    {
                        completableNow = impl.IsCompletable(serverPlayer, ao.args);
                    }
                }
                catch
                {
                    completableNow = true;
                }

                string completionKey = !string.IsNullOrWhiteSpace(ao.objectiveId)
                    ? ao.objectiveId
                    : InteractionKey(matchedX, matchedY, matchedZ);

                sapi.Logger.Debug($"[QuestInteractAt] Firing OnComplete for {completionKey}, completable: {completableNow}");
                QuestActionObjectiveCompletionUtil.TryFireOnComplete(sapi, serverPlayer, activeQuest, ao, completionKey, completableNow);
            }

            if (!anyChanged) return;

            for (int i = 0; i < actionObjectives.Count; i++)
            {
                var ao = actionObjectives[i];
                if (ao?.id != "interactcount") continue;

                bool completableNow;
                try
                {
                    var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();
                    if (questSystem?.ActionObjectiveRegistry != null && questSystem.ActionObjectiveRegistry.TryGetValue("interactcount", out var impl) && impl != null)
                    {
                        completableNow = impl.IsCompletable(serverPlayer, ao.args);
                    }
                    else
                    {
                        completableNow = false;
                    }
                }
                catch
                {
                    completableNow = false;
                }

                QuestActionObjectiveCompletionUtil.TryFireOnComplete(sapi, serverPlayer, activeQuest, ao, ao.objectiveId, completableNow);
            }
        }
    }
}
