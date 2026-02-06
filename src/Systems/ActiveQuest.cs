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
        public string ProgressText { get; set; }

        private const int LastInteractDebounceMs = 100;

        private class LastInteractCache
        {
            public long LastWriteMs;
            public int X;
            public int Y;
            public int Z;
            public int Dim;
        }

        private static readonly Dictionary<string, LastInteractCache> lastInteractCacheByPlayerUid = new Dictionary<string, LastInteractCache>(StringComparer.OrdinalIgnoreCase);

        private class ParsedPos
        {
            public bool Ok;
            public int X;
            public int Y;
            public int Z;
        }

        private static readonly Dictionary<string, ParsedPos> parsedPosCacheByString = new Dictionary<string, ParsedPos>(StringComparer.Ordinal);

        private Dictionary<int, int> gatherCache = new Dictionary<int, int>();

        public void OnEntityKilled(string entityCode, IPlayer byPlayer)
        {
            var questSystem = byPlayer.Entity.Api.ModLoader.GetModSystem<QuestSystem>();
            if (questSystem?.QuestRegistry == null || string.IsNullOrWhiteSpace(questId)) return;
            if (!questSystem.QuestRegistry.TryGetValue(questId, out var quest) || quest == null) return;

            if (!QuestTimeGateUtil.AllowsProgress(byPlayer, quest, questSystem?.ActionObjectiveRegistry, "kill")) return;

            checkEventTrackers(killTrackers, entityCode, null, quest.killObjectives);
        }

        public void OnBlockPlaced(string blockCode, int[] position, IPlayer byPlayer)
        {
            if (blockPlaceTrackers == null || blockPlaceTrackers.Count == 0) return;

            var questSystem = byPlayer.Entity.Api.ModLoader.GetModSystem<QuestSystem>();
            if (questSystem?.QuestRegistry == null || string.IsNullOrWhiteSpace(questId)) return;
            if (!questSystem.QuestRegistry.TryGetValue(questId, out var quest) || quest == null) return;

            if (!QuestTimeGateUtil.AllowsProgress(byPlayer, quest, questSystem?.ActionObjectiveRegistry, "blockplace")) return;

            checkEventTrackers(blockPlaceTrackers, blockCode, position, quest.blockPlaceObjectives);
        }

        public void OnBlockBroken(string blockCode, int[] position, IPlayer byPlayer)
        {
            if (blockBreakTrackers == null || blockBreakTrackers.Count == 0) return;

            var questSystem = byPlayer.Entity.Api.ModLoader.GetModSystem<QuestSystem>();
            if (questSystem?.QuestRegistry == null || string.IsNullOrWhiteSpace(questId)) return;
            if (!questSystem.QuestRegistry.TryGetValue(questId, out var quest) || quest == null) return;

            if (!QuestTimeGateUtil.AllowsProgress(byPlayer, quest, questSystem?.ActionObjectiveRegistry, "blockbreak")) return;

            checkEventTrackers(blockBreakTrackers, blockCode, position, quest.blockBreakObjectives);
        }

        public void OnBlockUsed(string blockCode, int[] position, IPlayer byPlayer, ICoreServerAPI sapi)
        {
            var questSystem = byPlayer.Entity.Api.ModLoader.GetModSystem<QuestSystem>();
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
                            lastInteractCacheByPlayerUid[uid] = cache;
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

            var serverPlayer = byPlayer as IServerPlayer;
            if (serverPlayer != null)
            {
                QuestInteractAtUtil.TryHandleInteractAtObjectives(quest, this, serverPlayer, position, sapi);
            }

            checkEventTrackers(interactTrackers, blockCode, position, quest.interactObjectives);
            for (int i = 0; i < quest.interactObjectives.Count; i++)
            {
                var objective = quest.interactObjectives[i];

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
                if (comma1 < 0) { parsedPosCacheByString[coordString] = new ParsedPos { Ok = false }; return false; }
                int comma2 = coordString.IndexOf(',', comma1 + 1);
                if (comma2 < 0) { parsedPosCacheByString[coordString] = new ParsedPos { Ok = false }; return false; }

                if (!int.TryParse(coordString.Substring(0, comma1), out x)
                    || !int.TryParse(coordString.Substring(comma1 + 1, comma2 - comma1 - 1), out y)
                    || !int.TryParse(coordString.Substring(comma2 + 1), out z))
                {
                    parsedPosCacheByString[coordString] = new ParsedPos { Ok = false };
                    return false;
                }

                parsedPosCacheByString[coordString] = new ParsedPos { Ok = true, X = x, Y = y, Z = z };
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool IsCompletable(IPlayer byPlayer)
        {
            var questSystem = byPlayer.Entity.Api.ModLoader.GetModSystem<QuestSystem>();
            if (questSystem?.QuestRegistry == null || string.IsNullOrWhiteSpace(questId)) return false;
            if (!questSystem.QuestRegistry.TryGetValue(questId, out var quest) || quest == null) return false;
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

        public void completeQuest(IPlayer byPlayer)
        {
            var questSystem = byPlayer.Entity.Api.ModLoader.GetModSystem<QuestSystem>();
            if (questSystem?.QuestRegistry == null || string.IsNullOrWhiteSpace(questId)) return;
            if (!questSystem.QuestRegistry.TryGetValue(questId, out var quest) || quest == null) return;
            foreach (var gatherObjective in quest.gatherObjectives)
            {
                handOverItems(byPlayer, gatherObjective);
            }
            for (int i = 0; i < quest.blockPlaceObjectives.Count; i++)
            {
                if (quest.blockPlaceObjectives[i].removeAfterFinished && i < blockPlaceTrackers.Count)
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
            var questSystem = byPlayer.Entity.Api.ModLoader.GetModSystem<QuestSystem>();
            if (questSystem?.QuestRegistry == null || string.IsNullOrWhiteSpace(questId)) return new List<int>();
            if (!questSystem.QuestRegistry.TryGetValue(questId, out var quest) || quest == null) return new List<int>();
            var result = new List<int>();
            for (int i = 0; i < quest.gatherObjectives.Count; i++)
            {
                result.Add(itemsGathered(byPlayer, quest.gatherObjectives[i], i));
            }
            return result;
        }

        public List<int> GetProgress(IPlayer byPlayer)
        {
            var questSystem = byPlayer.Entity.Api.ModLoader.GetModSystem<QuestSystem>();
            if (questSystem?.QuestRegistry == null || string.IsNullOrWhiteSpace(questId)) return new List<int>();
            if (!questSystem.QuestRegistry.TryGetValue(questId, out var quest) || quest == null) return new List<int>();
            var activeActionObjectives = quest.actionObjectives.ConvertAll<ActionObjectiveBase>(objective => questSystem.ActionObjectiveRegistry[objective.id]);

            var result = gatherProgress(byPlayer);
            result.AddRange(trackerProgress());

            for (int i = 0; i < activeActionObjectives.Count; i++)
            {
                result.AddRange(activeActionObjectives[i].GetProgress(byPlayer, quest.actionObjectives[i].args));
            }

            return result;
        }

        private int itemsGathered(IPlayer byPlayer, Objective gatherObjective, int objectiveIndex)
        {
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

            var code = slot.Itemstack.Collectible.Code.Path;
            foreach (var candidate in gatherObjective.validCodes)
            {
                if (candidate == code || candidate.EndsWith("*") && code.StartsWith(candidate.Remove(candidate.Length - 1)))
                {
                    return true;
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