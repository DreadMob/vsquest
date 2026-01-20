using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Config;

namespace VsQuest
{
    public partial class BossHuntSystem : ModSystem
    {
        public const string LastBossDamageTotalHoursKey = "alegacyvsquest:bosshunt:lastBossDamageTotalHours";

        private const string SaveKey = "alegacyvsquest:bosshunt:state";
        private const bool DebugBossHunt = true;

        private ICoreServerAPI sapi;
        private readonly List<BossHuntConfig> configs = new();
        private BossHuntWorldState state;
        private bool stateDirty;

        private long tickListenerId;

        private readonly Dictionary<string, List<BossHuntAnchorPoint>> orderedAnchorsCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> orderedAnchorsDirty = new(StringComparer.OrdinalIgnoreCase);

        private Entity cachedBossEntity;
        private string cachedBossKey;
        private double nextBossEntityScanTotalHours;
        private double nextDebugLogTotalHours;


        private void OnTick(float dt)
        {
            if (sapi == null) return;
            if (configs == null || configs.Count == 0) return;

            double nowHours = sapi.World.Calendar.TotalHours;

            var activeCfg = GetActiveBossConfig(nowHours);
            if (activeCfg == null) return;

            var cfg = activeCfg;
            if (!cfg.IsValid()) return;

            var bossKey = cfg.bossKey;
            var st = GetOrCreateState(bossKey);
            NormalizeState(cfg, st);

            if (st.nextRelocateAtTotalHours <= 0)
            {
                st.nextRelocateAtTotalHours = nowHours + cfg.GetRelocateIntervalHours();
                stateDirty = true;
            }
            Entity bossEntity = FindBossEntity(cfg, nowHours);
            bool bossAlive = bossEntity != null && bossEntity.Alive;

            // Handle relocation
            if (nowHours >= st.nextRelocateAtTotalHours)
            {
                if (bossAlive && !IsSafeToRelocate(cfg, bossEntity, nowHours))
                {
                    // Postpone a bit
                    st.nextRelocateAtTotalHours = nowHours + 0.25;
                    stateDirty = true;
                }
                else
                {
                    int nextIndex = PickAnotherIndex(st.currentPointIndex, GetPointCount(cfg, st));
                    st.currentPointIndex = nextIndex;
                    st.nextRelocateAtTotalHours = nowHours + cfg.GetRelocateIntervalHours();
                    stateDirty = true;

                    // If boss is currently alive in the world and it's safe, remove it so it effectively "moves".
                    if (bossAlive)
                    {
                        try
                        {
                            sapi.World.DespawnEntity(bossEntity, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                        }
                        catch
                        {
                        }
                    }

                    bossEntity = null;
                    bossAlive = false;
                }
            }

            // Handle respawn timer
            if (st.deadUntilTotalHours > nowHours)
            {
                SaveStateIfDirty();
                return;
            }

            // Ensure boss is spawned when a player comes close to its current point.
            if (!bossAlive)
            {
                if (TryGetPoint(cfg, st, st.currentPointIndex, out var point, out int pointDim, out var anchorPoint)
                    && AnyPlayerNear(point.X, point.Y, point.Z, pointDim, cfg.GetActivationRange()))
                {
                    DebugLog($"Spawn attempt: bossKey={bossKey} point={point.X:0.0},{point.Y:0.0},{point.Z:0.0} dim={pointDim} anchors={st.anchorPoints?.Count ?? 0} deadUntil={st.deadUntilTotalHours:0.00} now={nowHours:0.00}");
                    TrySpawnBoss(cfg, point, pointDim, anchorPoint);
                }
            }

            SaveStateIfDirty();
        }

    }
}
