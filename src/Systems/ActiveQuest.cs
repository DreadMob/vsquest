using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32.SafeHandles;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    // Simple LRU cache with size limit to prevent memory leaks
    internal class SimpleLRUCache<TKey, TValue>
    {
        private readonly Dictionary<TKey, TValue> cache;
        private readonly LinkedList<TKey> lruList;
        private readonly int maxSize;
        private readonly object syncLock = new object();

        public SimpleLRUCache(int maxSize, IEqualityComparer<TKey> comparer = null)
        {
            this.maxSize = maxSize;
            this.cache = new Dictionary<TKey, TValue>(maxSize, comparer);
            this.lruList = new LinkedList<TKey>();
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            lock (syncLock)
            {
                if (cache.TryGetValue(key, out value))
                {
                    // Move to front (most recently used)
                    lruList.Remove(key);
                    lruList.AddFirst(key);
                    return true;
                }
                return false;
            }
        }

        public void Add(TKey key, TValue value)
        {
            lock (syncLock)
            {
                if (cache.ContainsKey(key))
                {
                    // Update existing
                    cache[key] = value;
                    lruList.Remove(key);
                    lruList.AddFirst(key);
                }
                else
                {
                    // Evict if at capacity
                    if (cache.Count >= maxSize && lruList.Last != null)
                    {
                        var evictKey = lruList.Last.Value;
                        lruList.RemoveLast();
                        cache.Remove(evictKey);
                    }
                    
                    cache.Add(key, value);
                    lruList.AddFirst(key);
                }
            }
        }

        public bool Remove(TKey key)
        {
            lock (syncLock)
            {
                if (cache.Remove(key))
                {
                    lruList.Remove(key);
                    return true;
                }
                return false;
            }
        }

        public void Clear()
        {
            lock (syncLock)
            {
                cache.Clear();
                lruList.Clear();
            }
        }

        public int Count 
        { 
            get 
            { 
                lock (syncLock) return cache.Count; 
            } 
        }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class ActiveQuest
    {
        public long questGiverId { get; set; }
        public string questId { get; set; }
        public List<EventTracker> killTrackers { get; set; } = new List<EventTracker>();
        public List<EventTracker> blockPlaceTrackers { get; set; } = new List<EventTracker>();
        public List<EventTracker> blockBreakTrackers { get; set; } = new List<EventTracker>();
        public List<EventTracker> interactTrackers { get; set; } = new List<EventTracker>();
        public bool IsCompletableOnClient { get; set; }
        public bool IsCurrentStageCompleteOnClient { get; set; }
        public string ProgressText { get; set; }

        // Stage system properties
        public int currentStageIndex { get; set; } = 0;
        public List<int> completedStageIndices { get; set; } = new List<int>();

        private const int LastInteractDebounceMs = 100;

        private class LastInteractCache
        {
            public long LastWriteMs;
            public int X;
            public int Y;
            public int Z;
            public int Dim;
        }

        private static readonly SimpleLRUCache<string, LastInteractCache> lastInteractCacheByPlayerUid = new SimpleLRUCache<string, LastInteractCache>(1000, StringComparer.OrdinalIgnoreCase);

        private class ParsedPos
        {
            public bool Ok;
            public int X;
            public int Y;
            public int Z;
        }

        private static readonly SimpleLRUCache<string, ParsedPos> parsedPosCacheByString = new SimpleLRUCache<string, ParsedPos>(5000, StringComparer.Ordinal);

        private Dictionary<int, int> gatherCache = new Dictionary<int, int>();
        private long gatherCacheTimestamp = 0;
        private const int GatherCacheValidMs = 5000; // 5 секунд

        public void OnEntityKilled(string entityCode, IPlayer byPlayer)
        {
            var questSystem = QuestSystemCache.GetFromEntity(byPlayer.Entity);
            if (questSystem?.QuestRegistry == null || string.IsNullOrWhiteSpace(questId)) return;
            if (!questSystem.QuestRegistry.TryGetValue(questId, out var quest) || quest == null) return;

            if (!QuestTimeGateUtil.AllowsProgress(byPlayer, quest, questSystem?.ActionObjectiveRegistry, "kill")) return;

            // Use current stage objectives for multi-stage quests
            var currentStage = quest.GetStage(currentStageIndex);
            var killObjectives = currentStage?.killObjectives ?? quest.killObjectives;

            if (killTrackers == null || killTrackers.Count == 0 || killObjectives == null || killObjectives.Count == 0)
            {
                return;
            }

            // Kill objectives: play sounds on progress and on completion (server-side only)
            var serverPlayer = byPlayer as IServerPlayer;
            var sapi = byPlayer?.Entity?.Api as ICoreServerAPI;

            string progressSound = questSystem?.Config?.killObjectiveProgressSound;
            float progressPitch = questSystem?.Config?.killObjectiveProgressSoundPitch ?? 1f;
            float progressVolume = questSystem?.Config?.killObjectiveProgressSoundVolume ?? 0.25f;
            string completeSound = !string.IsNullOrWhiteSpace(quest.killObjectiveCompleteSound)
                ? quest.killObjectiveCompleteSound
                : questSystem?.Config?.killObjectiveCompleteSound;
            float completePitch = quest.killObjectiveCompleteSoundPitch
                ?? (questSystem?.Config?.killObjectiveCompleteSoundPitch ?? 1f);
            float completeVolume = quest.killObjectiveCompleteSoundVolume
                ?? (questSystem?.Config?.killObjectiveCompleteSoundVolume ?? 0.7f);

            int count = Math.Min(killTrackers.Count, killObjectives.Count);
            for (int i = 0; i < count; i++)
            {
                var tracker = killTrackers[i];

                var objective = killObjectives[i];
                if (tracker == null || objective == null) continue;

                if (!trackerMatches(tracker, entityCode)) continue;

                // Only count up to demand (prevents sound spam beyond completion)
                int demand = objective.demand;
                if (demand <= 0) continue;

                int before = tracker.count;
                if (before >= demand) continue;

                tracker.count++;

                if (sapi != null && serverPlayer != null)
                {
                    if (!string.IsNullOrWhiteSpace(progressSound))
                    {
                        sapi.World.PlaySoundFor(new AssetLocation(progressSound), serverPlayer, progressPitch, 32f, progressVolume);
                    }

                    if (tracker.count >= demand)
                    {
                        if (!string.IsNullOrWhiteSpace(completeSound))
                        {
                            sapi.World.PlaySoundFor(new AssetLocation(completeSound), serverPlayer, completePitch, 32f, completeVolume);
                        }
                    }
                }
            }
        }

        public void OnBlockPlaced(string blockCode, int[] position, IPlayer byPlayer)
        {
            if (blockPlaceTrackers == null || blockPlaceTrackers.Count == 0) return;

            var questSystem = QuestSystemCache.GetFromEntity(byPlayer.Entity);
            if (questSystem?.QuestRegistry == null || string.IsNullOrWhiteSpace(questId)) return;
            if (!questSystem.QuestRegistry.TryGetValue(questId, out var quest) || quest == null) return;

            if (!QuestTimeGateUtil.AllowsProgress(byPlayer, quest, questSystem?.ActionObjectiveRegistry, "blockplace")) return;

            // Use current stage objectives for multi-stage quests
            var currentStage = quest.GetStage(currentStageIndex);
            var blockPlaceObjectives = currentStage?.blockPlaceObjectives ?? quest.blockPlaceObjectives;

            checkEventTrackers(blockPlaceTrackers, blockCode, position, blockPlaceObjectives);
        }

        public void OnBlockBroken(string blockCode, int[] position, IPlayer byPlayer)
        {
            if (blockBreakTrackers == null || blockBreakTrackers.Count == 0) return;

            var questSystem = QuestSystemCache.GetFromEntity(byPlayer.Entity);
            if (questSystem?.QuestRegistry == null || string.IsNullOrWhiteSpace(questId)) return;
            if (!questSystem.QuestRegistry.TryGetValue(questId, out var quest) || quest == null) return;

            if (!QuestTimeGateUtil.AllowsProgress(byPlayer, quest, questSystem?.ActionObjectiveRegistry, "blockbreak")) return;

            // Use current stage objectives for multi-stage quests
            var currentStage = quest.GetStage(currentStageIndex);
            var blockBreakObjectives = currentStage?.blockBreakObjectives ?? quest.blockBreakObjectives;

            checkEventTrackers(blockBreakTrackers, blockCode, position, blockBreakObjectives);
        }

        public void OnBlockUsed(string blockCode, int[] position, IPlayer byPlayer, ICoreServerAPI sapi)
        {
            var questSystem = QuestSystemCache.GetFromEntity(byPlayer.Entity);
            if (questSystem?.QuestRegistry == null || string.IsNullOrWhiteSpace(questId)) return;
            if (!questSystem.QuestRegistry.TryGetValue(questId, out var quest) || quest == null) return;

            if (byPlayer?.Entity?.WatchedAttributes != null && position != null && position.Length == 3)
            {
                var wa = byPlayer.Entity.WatchedAttributes;
                int x = position[0];
                int y = position[1];
                int z = position[2];
                int dim = byPlayer.Entity?.Pos?.Dimension ?? 0;

                bool shouldWrite = true;
                try
                {
                    string uid = byPlayer?.PlayerUID;
                    if (!string.IsNullOrWhiteSpace(uid))
                    {
                        if (!lastInteractCacheByPlayerUid.TryGetValue(uid, out var cache) || cache == null)
                        {
                            cache = new LastInteractCache();
                            cache.X = int.MinValue;
                            cache.Y = int.MinValue;
                            cache.Z = int.MinValue;
                            cache.Dim = int.MinValue;
                            lastInteractCacheByPlayerUid.Add(uid, cache);
                        }

                        long now = Environment.TickCount64;
                        if ((now - cache.LastWriteMs) < LastInteractDebounceMs
                            && cache.X == x && cache.Y == y && cache.Z == z && cache.Dim == dim)
                        {
                            shouldWrite = false;
                        }
                        else
                        {
                            cache.LastWriteMs = now;
                            cache.X = x;
                            cache.Y = y;
                            cache.Z = z;
                            cache.Dim = dim;
                        }
                    }
                }
                catch
                {
                    shouldWrite = true;
                }

                if (shouldWrite)
                {
                    if (wa.GetInt("alegacyvsquest:lastinteract:x", int.MinValue) != x)
                    {
                        wa.SetInt("alegacyvsquest:lastinteract:x", x);
                        wa.MarkPathDirty("alegacyvsquest:lastinteract:x");
                    }
                    if (wa.GetInt("alegacyvsquest:lastinteract:y", int.MinValue) != y)
                    {
                        wa.SetInt("alegacyvsquest:lastinteract:y", y);
                        wa.MarkPathDirty("alegacyvsquest:lastinteract:y");
                    }
                    if (wa.GetInt("alegacyvsquest:lastinteract:z", int.MinValue) != z)
                    {
                        wa.SetInt("alegacyvsquest:lastinteract:z", z);
                        wa.MarkPathDirty("alegacyvsquest:lastinteract:z");
                    }
                    if (wa.GetInt("alegacyvsquest:lastinteract:dim", int.MinValue) != dim)
                    {
                        wa.SetInt("alegacyvsquest:lastinteract:dim", dim);
                        wa.MarkPathDirty("alegacyvsquest:lastinteract:dim");
                    }

                    // Backfill legacy keys for compatibility with older code paths.
                    if (wa.GetInt("vsquest:lastinteract:x", int.MinValue) != x)
                    {
                        wa.SetInt("vsquest:lastinteract:x", x);
                        wa.MarkPathDirty("vsquest:lastinteract:x");
                    }
                    if (wa.GetInt("vsquest:lastinteract:y", int.MinValue) != y)
                    {
                        wa.SetInt("vsquest:lastinteract:y", y);
                        wa.MarkPathDirty("vsquest:lastinteract:y");
                    }
                    if (wa.GetInt("vsquest:lastinteract:z", int.MinValue) != z)
                    {
                        wa.SetInt("vsquest:lastinteract:z", z);
                        wa.MarkPathDirty("vsquest:lastinteract:z");
                    }
                    if (wa.GetInt("vsquest:lastinteract:dim", int.MinValue) != dim)
                    {
                        wa.SetInt("vsquest:lastinteract:dim", dim);
                        wa.MarkPathDirty("vsquest:lastinteract:dim");
                    }
                }
            }

            if (!QuestTimeGateUtil.AllowsProgress(byPlayer, quest, questSystem?.ActionObjectiveRegistry, "interact")) return;

            // Use current stage objectives for multi-stage quests
            var currentStage = quest.GetStage(currentStageIndex);
            var interactObjectives = currentStage?.interactObjectives ?? quest.interactObjectives;

            var serverPlayer = byPlayer as IServerPlayer;
            if (serverPlayer != null)
            {
                QuestInteractAtUtil.TryHandleInteractAtObjectives(quest, this, serverPlayer, position, sapi);
            }

            checkEventTrackers(interactTrackers, blockCode, position, interactObjectives);
            for (int i = 0; i < interactObjectives.Count; i++)
            {
                var objective = interactObjectives[i];

                bool matches = QuestObjectiveMatchUtil.InteractObjectiveMatches(objective, blockCode, position);
                if (!matches) continue;

                if (serverPlayer != null)
                {
                    var message = new QuestAcceptedMessage { questGiverId = questGiverId, questId = questId };
                    foreach (var actionReward in objective.actionRewards)
                    {
                        if (questSystem.ActionRegistry.TryGetValue(actionReward.id, out var action))
                        {
                            action.Execute(sapi, message, serverPlayer, actionReward.args);
                        }
                    }
                }
            }
        }

        private void checkEventTrackers(List<EventTracker> trackers, string code, int[] position, List<Objective> objectives)
        {
            if (trackers == null || trackers.Count == 0) return;

            if (position == null)
            {
                for (int i = 0; i < trackers.Count; i++)
                {
                    var tracker = trackers[i];
                    if (trackerMatches(tracker, code))
                    {
                        tracker.count++;
                    }
                }
                return;
            }

            if (objectives == null || objectives.Count == 0) return;

            int count = Math.Min(trackers.Count, objectives.Count);
            for (int i = 0; i < count; i++)
            {
                var tracker = trackers[i];
                if (trackerMatches(objectives[i], tracker, code, position))
                {
                    tracker.count++;
                }
            }
        }

        private static bool trackerMatches(EventTracker tracker, string code)
        {
            foreach (var candidate in tracker.relevantCodes)
            {
                if (LocalizationUtils.MobCodeMatches(candidate, code))
                {
                    return true;
                }

                if (candidate.EndsWith("*") && code.StartsWith(candidate.Remove(candidate.Length - 1)))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool trackerMatches(Objective objective, EventTracker tracker, string code, int[] position)
        {
            if (objective.positions != null && objective.positions.Count > 0)
            {
                foreach (var candidate in objective.positions)
                {
                    if (!TryParsePosCached(candidate, out int cx, out int cy, out int cz)) continue;
                    if (cx == position[0] && cy == position[1] && cz == position[2])
                    {
                        // If validCodes is missing, treat position match as sufficient.
                        if (objective.validCodes == null || objective.validCodes.Count == 0)
                        {
                            if (tracker.placedPositions.Contains(candidate)) return false;
                            tracker.placedPositions.Add(candidate);
                            return true;
                        }

                        foreach (var codeCandidate in objective.validCodes)
                        {
                            if (LocalizationUtils.MobCodeMatches(codeCandidate, code))
                            {
                                if (tracker.placedPositions.Contains(candidate)) return false;
                                tracker.placedPositions.Add(candidate);
                                return true;
                            }

                            if (codeCandidate.EndsWith("*") && code.StartsWith(codeCandidate.Remove(codeCandidate.Length - 1)))
                            {
                                if (tracker.placedPositions.Contains(candidate)) return false;
                                tracker.placedPositions.Add(candidate);
                                return true;
                            }
                        }
                    }
                }
                return false;
            }
            else
            {
                foreach (var candidate in tracker.relevantCodes)
                {
                    if (LocalizationUtils.MobCodeMatches(candidate, code))
                    {
                        return true;
                    }

                    if (candidate.EndsWith("*") && code.StartsWith(candidate.Remove(candidate.Length - 1)))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        private static bool TryParsePosCached(string coordString, out int x, out int y, out int z)
        {
            x = y = z = 0;
            if (string.IsNullOrWhiteSpace(coordString)) return false;

            try
            {
                if (parsedPosCacheByString.TryGetValue(coordString, out var cached) && cached != null)
                {
                    if (!cached.Ok) return false;
                    x = cached.X;
                    y = cached.Y;
                    z = cached.Z;
                    return true;
                }

                int comma1 = coordString.IndexOf(',');
                if (comma1 < 0) { parsedPosCacheByString.Add(coordString, new ParsedPos { Ok = false }); return false; }
                int comma2 = coordString.IndexOf(',', comma1 + 1);
                if (comma2 < 0) { parsedPosCacheByString.Add(coordString, new ParsedPos { Ok = false }); return false; }

                if (!int.TryParse(coordString.Substring(0, comma1), out x)
                    || !int.TryParse(coordString.Substring(comma1 + 1, comma2 - comma1 - 1), out y)
                    || !int.TryParse(coordString.Substring(comma2 + 1), out z))
                {
                    parsedPosCacheByString.Add(coordString, new ParsedPos { Ok = false });
                    return false;
                }

                parsedPosCacheByString.Add(coordString, new ParsedPos { Ok = true, X = x, Y = y, Z = z });
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the current stage for this quest. Returns null if quest not found.
        /// </summary>
        public QuestStage GetCurrentStage(Quest quest)
        {
            if (quest == null) return null;
            return quest.GetStage(currentStageIndex);
        }

        /// <summary>
        /// Checks if the current stage is completable (not the entire quest)
        /// </summary>
        public bool IsCurrentStageCompletable(IPlayer byPlayer, Quest quest)
        {
            var stage = GetCurrentStage(quest);
            if (stage == null) return false;

            var questSystem = QuestSystemCache.GetFromEntity(byPlayer.Entity);
            if (questSystem?.ActionObjectiveRegistry == null) return false;

            var activeActionObjectives = stage.actionObjectives?.ConvertAll<ActionObjectiveBase>(
                objective => questSystem.ActionObjectiveRegistry.TryGetValue(objective.id, out var impl) ? impl : null
            ) ?? new List<ActionObjectiveBase>();

            bool completable = true;

            // Ensure trackers exist for current stage objectives
            while (blockPlaceTrackers.Count < stage.blockPlaceObjectives.Count)
            {
                blockPlaceTrackers.Add(new EventTracker());
            }
            while (blockBreakTrackers.Count < stage.blockBreakObjectives.Count)
            {
                blockBreakTrackers.Add(new EventTracker());
            }
            while (killTrackers.Count < stage.killObjectives.Count)
            {
                killTrackers.Add(new EventTracker());
            }
            while (interactTrackers.Count < stage.interactObjectives.Count)
            {
                interactTrackers.Add(new EventTracker());
            }

            for (int i = 0; i < stage.blockPlaceObjectives.Count; i++)
            {
                if (stage.blockPlaceObjectives[i].positions != null && stage.blockPlaceObjectives[i].positions.Count > 0)
                {
                    completable &= stage.blockPlaceObjectives[i].positions.Count <= blockPlaceTrackers[i].placedPositions.Count;
                }
                else
                {
                    completable &= stage.blockPlaceObjectives[i].demand <= blockPlaceTrackers[i].count;
                }
            }
            for (int i = 0; i < stage.blockBreakObjectives.Count; i++)
            {
                completable &= stage.blockBreakObjectives[i].demand <= blockBreakTrackers[i].count;
            }
            for (int i = 0; i < stage.interactObjectives.Count; i++)
            {
                if (stage.interactObjectives[i].positions != null && stage.interactObjectives[i].positions.Count > 0)
                {
                    int demand = stage.interactObjectives[i].demand > 0 ? stage.interactObjectives[i].demand : stage.interactObjectives[i].positions.Count;
                    completable &= demand <= interactTrackers[i].count;
                }
                else
                {
                    completable &= stage.interactObjectives[i].demand <= interactTrackers[i].count;
                }
            }
            for (int i = 0; i < stage.killObjectives.Count; i++)
            {
                completable &= stage.killObjectives[i].demand <= killTrackers[i].count;
            }
            foreach (var gatherObjective in stage.gatherObjectives)
            {
                int itemsFound = itemsGathered(byPlayer, gatherObjective, stage.gatherObjectives.IndexOf(gatherObjective));
                completable &= itemsFound >= gatherObjective.demand;
            }
            for (int i = 0; i < activeActionObjectives.Count; i++)
            {
                if (activeActionObjectives[i] != null)
                {
                    completable &= activeActionObjectives[i].IsCompletable(byPlayer, stage.actionObjectives[i].args);
                }
            }
            return completable;
        }

        /// <summary>
        /// Checks if the entire quest is completable (all stages complete)
        /// </summary>
        public bool IsCompletable(IPlayer byPlayer)
        {
            var questSystem = QuestSystemCache.GetFromEntity(byPlayer.Entity);
            if (questSystem?.QuestRegistry == null || string.IsNullOrWhiteSpace(questId)) return false;
            if (!questSystem.QuestRegistry.TryGetValue(questId, out var quest) || quest == null) return false;

            // If quest has stages, check if we're on the final stage and it's complete
            if (quest.HasStages)
            {
                // Quest is completable only when on the last stage and that stage is complete
                if (currentStageIndex < quest.stages.Count - 1)
                {
                    return false;
                }
                return IsCurrentStageCompletable(byPlayer, quest);
            }

            // Legacy quest (no stages) - use original logic
            var activeActionObjectives = quest.actionObjectives.ConvertAll<ActionObjectiveBase>(objective => questSystem.ActionObjectiveRegistry[objective.id]);
            bool completable = true;

            while (blockPlaceTrackers.Count < quest.blockPlaceObjectives.Count)
            {
                blockPlaceTrackers.Add(new EventTracker());
            }
            while (blockBreakTrackers.Count < quest.blockBreakObjectives.Count)
            {
                blockBreakTrackers.Add(new EventTracker());
            }
            while (killTrackers.Count < quest.killObjectives.Count)
            {
                killTrackers.Add(new EventTracker());
            }
            while (interactTrackers.Count < quest.interactObjectives.Count)
            {
                interactTrackers.Add(new EventTracker());
            }

            for (int i = 0; i < quest.blockPlaceObjectives.Count; i++)
            {
                if (quest.blockPlaceObjectives[i].positions != null && quest.blockPlaceObjectives[i].positions.Count > 0)
                {
                    completable &= quest.blockPlaceObjectives[i].positions.Count <= blockPlaceTrackers[i].placedPositions.Count;
                }
                else
                {
                    completable &= quest.blockPlaceObjectives[i].demand <= blockPlaceTrackers[i].count;
                }
            }
            for (int i = 0; i < quest.blockBreakObjectives.Count; i++)
            {
                completable &= quest.blockBreakObjectives[i].demand <= blockBreakTrackers[i].count;
            }
            for (int i = 0; i < quest.interactObjectives.Count; i++)
            {
                if (quest.interactObjectives[i].positions != null && quest.interactObjectives[i].positions.Count > 0)
                {
                    int demand = quest.interactObjectives[i].demand > 0 ? quest.interactObjectives[i].demand : quest.interactObjectives[i].positions.Count;
                    completable &= demand <= interactTrackers[i].count;
                }
                else
                {
                    completable &= quest.interactObjectives[i].demand <= interactTrackers[i].count;
                }
            }
            for (int i = 0; i < quest.killObjectives.Count; i++)
            {
                completable &= quest.killObjectives[i].demand <= killTrackers[i].count;
            }
            foreach (var gatherObjective in quest.gatherObjectives)
            {
                int itemsFound = itemsGathered(byPlayer, gatherObjective, quest.gatherObjectives.IndexOf(gatherObjective));
                completable &= itemsFound >= gatherObjective.demand;
            }
            for (int i = 0; i < activeActionObjectives.Count; i++)
            {
                completable &= activeActionObjectives[i].IsCompletable(byPlayer, quest.actionObjectives[i].args);
            }
            return completable;
        }

        /// <summary>
        /// Advances to the next stage. Returns true if advanced, false if already on final stage.
        /// </summary>
        public bool AdvanceStage(Quest quest)
        {
            if (quest == null) return false;

            // Mark current stage as completed
            if (!completedStageIndices.Contains(currentStageIndex))
            {
                completedStageIndices.Add(currentStageIndex);
            }

            // Check if there's a next stage
            if (currentStageIndex >= quest.StageCount - 1)
            {
                return false; // Already on final stage
            }

            // Advance to next stage
            currentStageIndex++;

            // Reset trackers for new stage
            killTrackers.Clear();
            blockPlaceTrackers.Clear();
            blockBreakTrackers.Clear();
            interactTrackers.Clear();
            gatherCache.Clear();

            return true;
        }

        /// <summary>
        /// Returns true if all stages are complete (quest can be turned in)
        /// </summary>
        public bool AreAllStagesComplete(IPlayer byPlayer, Quest quest)
        {
            if (quest == null) return false;
            if (!quest.HasStages) return IsCompletable(byPlayer);

            // On final stage and it's complete
            return currentStageIndex >= quest.stages.Count - 1 && IsCurrentStageCompletable(byPlayer, quest);
        }

        public void completeQuest(IPlayer byPlayer)
        {
            var questSystem = QuestSystemCache.GetFromEntity(byPlayer.Entity);
            if (questSystem?.QuestRegistry == null || string.IsNullOrWhiteSpace(questId)) return;
            if (!questSystem.QuestRegistry.TryGetValue(questId, out var quest) || quest == null) return;

            // Use current stage objectives for multi-stage quests
            var currentStage = quest.GetStage(currentStageIndex);
            var gatherObjectives = currentStage?.gatherObjectives ?? quest.gatherObjectives;
            var blockPlaceObjectives = currentStage?.blockPlaceObjectives ?? quest.blockPlaceObjectives;

            foreach (var gatherObjective in gatherObjectives)
            {
                handOverItems(byPlayer, gatherObjective);
            }
            for (int i = 0; i < blockPlaceObjectives.Count; i++)
            {
                if (blockPlaceObjectives[i].removeAfterFinished && i < blockPlaceTrackers.Count)
                {
                    int maxRemovals = 100;
                    int removed = 0;
                    foreach (var posStr in blockPlaceTrackers[i].placedPositions)
                    {
                        if (++removed > maxRemovals) break;
                        if (!TryParsePosCached(posStr, out int x, out int y, out int z)) continue;
                        byPlayer.Entity.World.BlockAccessor.SetBlock(0, new Vintagestory.API.MathTools.BlockPos(x, y, z));
                    }
                }
            }
        }

        public List<int> trackerProgress()
        {
            var result = new List<int>();
            foreach (var trackerList in new List<EventTracker>[] { killTrackers, blockPlaceTrackers, blockBreakTrackers, interactTrackers })
            {
                if (trackerList != null)
                {
                    result.AddRange(trackerList.ConvertAll<int>(tracker => tracker.count));
                }
            }
            return result;
        }

        public List<int> gatherProgress(IPlayer byPlayer)
        {
            var questSystem = QuestSystemCache.GetFromEntity(byPlayer.Entity);
            if (questSystem?.QuestRegistry == null || string.IsNullOrWhiteSpace(questId)) return new List<int>();
            if (!questSystem.QuestRegistry.TryGetValue(questId, out var quest) || quest == null) return new List<int>();

            // Use current stage objectives for multi-stage quests
            var currentStage = quest.GetStage(currentStageIndex);
            var gatherObjectives = currentStage?.gatherObjectives ?? quest.gatherObjectives;

            var result = new List<int>();
            for (int i = 0; i < gatherObjectives.Count; i++)
            {
                result.Add(itemsGathered(byPlayer, gatherObjectives[i], i));
            }
            return result;
        }

        public List<int> GetProgress(IPlayer byPlayer)
        {
            var questSystem = QuestSystemCache.GetFromEntity(byPlayer.Entity);
            if (questSystem?.QuestRegistry == null || string.IsNullOrWhiteSpace(questId)) return new List<int>();
            if (!questSystem.QuestRegistry.TryGetValue(questId, out var quest) || quest == null) return new List<int>();

            // Use current stage objectives for multi-stage quests
            var currentStage = quest.GetStage(currentStageIndex);
            var actionObjectives = currentStage?.actionObjectives ?? quest.actionObjectives;

            var activeActionObjectives = actionObjectives.ConvertAll<ActionObjectiveBase>(objective => questSystem.ActionObjectiveRegistry[objective.id]);

            var result = gatherProgress(byPlayer);
            result.AddRange(trackerProgress());

            for (int i = 0; i < activeActionObjectives.Count; i++)
            {
                result.AddRange(activeActionObjectives[i].GetProgress(byPlayer, actionObjectives[i].args));
            }

            return result;
        }

        private int itemsGathered(IPlayer byPlayer, Objective gatherObjective, int objectiveIndex)
        {
            long now = Environment.TickCount64;
            if (now - gatherCacheTimestamp > GatherCacheValidMs)
            {
                gatherCache.Clear();
                gatherCacheTimestamp = now;
            }

            int itemsFound;
            if (gatherCache.TryGetValue(objectiveIndex, out itemsFound)) return itemsFound;
            itemsFound = 0;
            foreach (var inventory in byPlayer.InventoryManager.Inventories.Values)
            {
                if (inventory.ClassName == GlobalConstants.creativeInvClassName)
                {
                    continue;
                }
                foreach (var slot in inventory)
                {
                    if (gatherObjectiveMatches(slot, gatherObjective))
                    {
                        itemsFound += slot.Itemstack.StackSize;
                    }
                }
            }
            gatherCache[objectiveIndex] = itemsFound;
            return itemsFound;
        }

        private bool gatherObjectiveMatches(ItemSlot slot, Objective gatherObjective)
        {
            if (slot.Empty) return false;

            var stack = slot.Itemstack;
            var code = stack.Collectible.Code.Path;

            foreach (var candidate in gatherObjective.validCodes)
            {
                // Check base item code
                if (candidate == code || candidate.EndsWith("*") && code.StartsWith(candidate.Remove(candidate.Length - 1)))
                {
                    return true;
                }

                // Check action item ID from attributes
                if (stack.Attributes != null)
                {
                    string actionItemId = stack.Attributes.GetString(ItemAttributeUtils.ActionItemIdKey);
                    if (!string.IsNullOrWhiteSpace(actionItemId) && actionItemId.Equals(candidate, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public void handOverItems(IPlayer byPlayer, Objective gatherObjective)
        {
            int itemsFound = 0;
            foreach (var inventory in byPlayer.InventoryManager.Inventories.Values)
            {
                if (inventory.ClassName == GlobalConstants.creativeInvClassName)
                {
                    continue;
                }
                foreach (var slot in inventory)
                {
                    if (gatherObjectiveMatches(slot, gatherObjective))
                    {
                        var stack = slot.TakeOut(Math.Min(slot.Itemstack.StackSize, gatherObjective.demand - itemsFound));
                        slot.MarkDirty();
                        itemsFound += stack.StackSize;
                    }
                    if (itemsFound > gatherObjective.demand) { return; }
                }
            }
        }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class EventTracker
    {
        public List<string> relevantCodes { get; set; } = new List<string>();
        public int count { get; set; }
        public List<string> placedPositions { get; set; } = new List<string>();
    }
}