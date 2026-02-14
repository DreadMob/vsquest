using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class BlockEntityAshFloor : BlockEntity
    {
        private const string AttrDespawnAtMs = "vsquest:ashfloor:despawnAtMs";
        private const string AttrOwnerId = "vsquest:ashfloor:ownerId";
        private const string AttrTickIntervalMs = "vsquest:ashfloor:tickIntervalMs";
        private const string AttrVictimWalkSpeedMult = "vsquest:ashfloor:victimWalkSpeedMult";
        
        private const int MinTickIntervalMs = 1000; // Увеличили с 100 до 1000мс (1 секунда)
        private const int BaseTickIntervalMs = 2000; // Увеличили с 500 до 2000мс (2 секунды)
        private const double DebuffDurationMultiplier = 6; // Увеличили множитель длительности

        private const string VictimUntilKey = "alegacyvsquest:ashfloor:until";
        private const string VictimWalkSpeedMultKey = "alegacyvsquest:ashfloor:walkspeedmult";
        

        private long despawnAtMs;
        private long ownerId;
        private int tickIntervalMs;

        private float victimWalkSpeedMult;

        private bool ticking;
        private long nextTickAtMs;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (Api?.Side != EnumAppSide.Server) return;

            if (!ticking)
            {
                ticking = true;
                RegisterGameTickListener(OnServerTick, 500); // Увеличили с 250 до 500мс
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            despawnAtMs = tree.GetLong(AttrDespawnAtMs, despawnAtMs);
            ownerId = tree.GetLong(AttrOwnerId, ownerId);
            tickIntervalMs = tree.GetInt(AttrTickIntervalMs, tickIntervalMs);

            victimWalkSpeedMult = tree.GetFloat(AttrVictimWalkSpeedMult, victimWalkSpeedMult);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetLong(AttrDespawnAtMs, despawnAtMs);
            tree.SetLong(AttrOwnerId, ownerId);
            tree.SetInt(AttrTickIntervalMs, tickIntervalMs);

            tree.SetFloat(AttrVictimWalkSpeedMult, victimWalkSpeedMult);
        }

        public void Arm(long ownerId, long despawnAtMs, int tickIntervalMs, float victimWalkSpeedMult)
        {
            if (Api == null) return;

            this.ownerId = ownerId;
            this.despawnAtMs = despawnAtMs;
            this.tickIntervalMs = tickIntervalMs;

            this.victimWalkSpeedMult = victimWalkSpeedMult;

            nextTickAtMs = 0;
            MarkDirty(true);
        }

        private void OnServerTick(float dt)
        {
            if (Api?.Side != EnumAppSide.Server) return;
            if (Pos == null) return;

            var sapi = Api as ICoreServerAPI;
            if (sapi == null) return;

            long now = sapi.World.ElapsedMilliseconds;

            if (despawnAtMs > 0 && now >= despawnAtMs)
            {
                TryRemoveSelf(sapi);
                return;
            }

            int interval = Math.Max(MinTickIntervalMs, tickIntervalMs <= 0 ? BaseTickIntervalMs : tickIntervalMs);
            if (nextTickAtMs != 0 && now < nextTickAtMs) return;
            nextTickAtMs = now + interval;

            try
            {
                var players = sapi.World.AllOnlinePlayers;
                if (players == null || players.Length == 0) return;

                for (int i = 0; i < players.Length; i++)
                {
                    var plr = players[i] as EntityPlayer;
                    if (plr == null || !plr.Alive) continue;
                    if (plr.ServerPos.Dimension != Pos.dimension) continue;

                    if (IsPlayerOnThisBlock(plr))
                    {
                        ApplyVictimDebuffs(sapi, plr, interval);
                    }
                }
            }
            catch
            {
            }
        }

        private bool IsPlayerOnThisBlock(EntityPlayer player)
        {
            if (player?.ServerPos == null || Pos == null) return false;
            
            // Используем координаты ступней игрока
            int px = (int)Math.Floor(player.ServerPos.X);
            int py = (int)Math.Floor(player.ServerPos.Y);
            int pz = (int)Math.Floor(player.ServerPos.Z);

            // Проверяем блок под ногами и блок, в котором ноги (на случай если блок чуть ниже поверхности)
            bool atLevel = px == Pos.X && py == Pos.Y && pz == Pos.Z;
            bool slightlyAbove = px == Pos.X && (py - 1) == Pos.Y && pz == Pos.Z;

            return atLevel || slightlyAbove;
        }

        public void OnEntityCollision(Entity entity)
        {
            // Метод оставлен пустым, так как мы вернулись к OnServerTick для стабильности
        }

        private void ApplyVictimDebuffs(ICoreServerAPI sapi, EntityPlayer player, int intervalMs)
        {
            if (sapi == null || player?.WatchedAttributes == null) return;

            double nowHours;
            try
            {
                nowHours = sapi.World.Calendar.TotalHours;
            }
            catch
            {
                nowHours = 0;
            }

            if (nowHours <= 0) return;

            // Увеличиваем длительность дебаффа для стабильности (редуцированная синхронизация)
            double until = nowHours + (Math.Max(MinTickIntervalMs, intervalMs) * DebuffDurationMultiplier / 3600000.0);

            try
            {
                double prev = player.WatchedAttributes.GetDouble(VictimUntilKey, 0);
                // Only update if value changed significantly
                if (Math.Abs(prev - until) > 0.0001)
                {
                    player.WatchedAttributes.SetDouble(VictimUntilKey, until);
                    player.WatchedAttributes.MarkPathDirty(VictimUntilKey);
                }
            }
            catch
            {
            }

            try
            {
                float mult = GameMath.Clamp(victimWalkSpeedMult <= 0f ? 0.4f : victimWalkSpeedMult, 0.05f, 1f);
                float prev = player.WatchedAttributes.GetFloat(VictimWalkSpeedMultKey, float.NaN);
                // Only update if value changed significantly or not set
                if (float.IsNaN(prev) || Math.Abs(prev - mult) > 0.01f)
                {
                    player.WatchedAttributes.SetFloat(VictimWalkSpeedMultKey, mult);
                    player.WatchedAttributes.MarkPathDirty(VictimWalkSpeedMultKey);
                }
            }
            catch
            {
            }
        }

        private void TryRemoveSelf(ICoreServerAPI sapi)
        {
            if (sapi?.World?.BlockAccessor == null || Pos == null) return;

            try
            {
                var myBlock = sapi.World.BlockAccessor.GetBlock(Pos);
                if (myBlock?.EntityClass == null) return;

                var expectedClass = sapi.World.ClassRegistry.GetBlockEntityClass(typeof(BlockEntityAshFloor));
                if (expectedClass == null) return;

                if (myBlock.EntityClass != expectedClass) return;

                sapi.World.BlockAccessor.SetBlock(0, Pos);
                sapi.World.BlockAccessor.RemoveBlockEntity(Pos);
            }
            catch
            {
            }
        }
    }
}
