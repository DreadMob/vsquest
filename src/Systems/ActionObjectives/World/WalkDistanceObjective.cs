using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class WalkDistanceObjective : ActionObjectiveBase
    {
        public static string HaveKey(string questId, int slot) => $"alegacyvsquest:walkdist:{questId}:slot{slot}:have";

        private class PlayerWalkCache
        {
            public bool HasLast;
            public double LastX;
            public double LastZ;
        }

        private static readonly Dictionary<string, PlayerWalkCache> playerCacheByKey = new Dictionary<string, PlayerWalkCache>();

        private static int updateCounter = 0;

        private static string CacheKey(string playerUid, string questId, int slot)
        {
            return $"{playerUid}|{questId}|{slot}";
        }

        public static void ClearPlayerCache(string playerUid)
        {
            if (string.IsNullOrWhiteSpace(playerUid)) return;

            // Remove all caches for that player across quests/slots.
            // (Using string keys; enumerate and remove safely.)
            List<string> remove = null;
            foreach (var key in playerCacheByKey.Keys)
            {
                if (key != null && key.StartsWith(playerUid + "|"))
                {
                    remove ??= new List<string>();
                    remove.Add(key);
                }
            }

            if (remove != null)
            {
                for (int i = 0; i < remove.Count; i++)
                {
                    playerCacheByKey.Remove(remove[i]);
                }
            }
        }

        public override bool IsCompletable(IPlayer byPlayer, params string[] args)
        {
            if (byPlayer?.Entity?.WatchedAttributes == null) return false;
            if (!TryParseArgs(args, out string questId, out int slot, out int needMeters)) return false;

            float have = byPlayer.Entity.WatchedAttributes.GetFloat(HaveKey(questId, slot), 0f);
            if (have < 0f) have = 0f;

            return needMeters > 0 && have >= needMeters;
        }

        public override List<int> GetProgress(IPlayer byPlayer, params string[] args)
        {
            if (byPlayer?.Entity?.WatchedAttributes == null) return new List<int>(new int[] { 0, 0 });
            if (!TryParseArgs(args, out string questId, out int slot, out int needMeters)) return new List<int>(new int[] { 0, 0 });

            float have = byPlayer.Entity.WatchedAttributes.GetFloat(HaveKey(questId, slot), 0f);
            if (have < 0f) have = 0f;

            int haveInt = (int)Math.Floor(have);
            if (haveInt > needMeters && needMeters > 0) haveInt = needMeters;
            if (needMeters < 0) needMeters = 0;

            return new List<int>(new int[] { haveInt, needMeters });
        }

        public void OnTick(IServerPlayer player, ActiveQuest activeQuest, int objectiveIndex, string[] args, ICoreServerAPI sapi, float dt)
        {
            if (player?.Entity == null) return;

            var wa = player.Entity.WatchedAttributes;
            if (wa == null) return;

            if (!TryParseArgs(args, out string questId, out int slot, out int needMeters)) return;
            if (needMeters <= 0) return;

            var entity = player.Entity;
            var controls = entity.Controls;
            if (controls == null) return;

            if (!controls.TriesToMove) { EnsureLastPosInitialized(entity, wa, questId, slot); return; }
            if (!entity.OnGround) { EnsureLastPosInitialized(entity, wa, questId, slot); return; }
            if (entity.Swimming) { EnsureLastPosInitialized(entity, wa, questId, slot); return; }
            if (controls.IsFlying || controls.Gliding || controls.DetachedMode) { EnsureLastPosInitialized(entity, wa, questId, slot); return; }

            double curX = entity.ServerPos.X;
            double curZ = entity.ServerPos.Z;

            string cacheKey = CacheKey(player.PlayerUID, questId, slot);

            if (!playerCacheByKey.TryGetValue(cacheKey, out var cache) || cache == null)
            {
                cache = new PlayerWalkCache();
                playerCacheByKey[cacheKey] = cache;
            }

            if (!cache.HasLast)
            {
                cache.LastX = curX;
                cache.LastZ = curZ;
                cache.HasLast = true;
                return;
            }

            double lastX = cache.LastX;
            double lastZ = cache.LastZ;

            double dx = curX - lastX;
            double dz = curZ - lastZ;
            double dist = Math.Sqrt(dx * dx + dz * dz);

            if (dist > 20)
            {
                cache.LastX = curX;
                cache.LastZ = curZ;
                return;
            }

            if (dist < 0.05)
            {
                cache.LastX = curX;
                cache.LastZ = curZ;
                return;
            }

            float have = wa.GetFloat(HaveKey(questId, slot), 0f);
            if (have < 0f) have = 0f;
            if (have >= needMeters)
            {
                cache.LastX = curX;
                cache.LastZ = curZ;
                return;
            }

            have += (float)dist;
            if (have > needMeters) have = needMeters;

            if (++updateCounter % 20 == 0) wa.SetFloat(HaveKey(questId, slot), have);

            // Intentionally do not MarkPathDirty(HaveKey): this progress is used server-side,
            // and client UI receives progress via ActiveQuest.ProgressText.

            if (have >= needMeters)
            {
                try
                {
                    var questSystem = sapi?.ModLoader?.GetModSystem<QuestSystem>();
                    if (questSystem?.QuestRegistry != null && questSystem.QuestRegistry.TryGetValue(activeQuest.questId, out var questDef))
                    {
                        var objectiveDef = questDef?.actionObjectives != null && objectiveIndex >= 0 && objectiveIndex < questDef.actionObjectives.Count
                            ? questDef.actionObjectives[objectiveIndex]
                            : null;

                        if (objectiveDef != null)
                        {
                            QuestActionObjectiveCompletionUtil.TryFireOnComplete(sapi, player, activeQuest, objectiveDef, objectiveDef.objectiveId, true);
                        }
                    }
                }
                catch
                {
                }
            }

            cache.LastX = curX;
            cache.LastZ = curZ;
        }

        private static void EnsureLastPosInitialized(Entity entity, SyncedTreeAttribute wa, string questId, int slot)
        {
            if (entity == null || wa == null) return;
            var player = entity as EntityPlayer;
            if (player == null) return;
            if (player.Player == null || string.IsNullOrWhiteSpace(player.Player.PlayerUID)) return;
            if (string.IsNullOrWhiteSpace(questId)) return;

            string key = CacheKey(player.Player.PlayerUID, questId, slot);
            if (!playerCacheByKey.TryGetValue(key, out var cache) || cache == null)
            {
                cache = new PlayerWalkCache();
                playerCacheByKey[key] = cache;
            }

            if (cache.HasLast) return;

            cache.LastX = entity.ServerPos.X;
            cache.LastZ = entity.ServerPos.Z;
            cache.HasLast = true;
        }

        public static bool TryParseArgs(string[] args, out string questId, out int slot, out int needMeters)
        {
            questId = null;
            slot = 0;
            needMeters = 0;

            if (args == null || args.Length < 2) return false;

            questId = args[0];
            if (string.IsNullOrWhiteSpace(questId)) return false;

            if (args.Length >= 3 && int.TryParse(args[1], out int parsedSlot))
            {
                slot = parsedSlot;
                if (!int.TryParse(args[2], out needMeters)) needMeters = 0;
            }
            else
            {
                slot = 0;
                if (!int.TryParse(args[1], out needMeters)) needMeters = 0;
            }

            if (slot < 0) slot = 0;
            if (needMeters < 0) needMeters = 0;

            return true;
        }
    }
}
