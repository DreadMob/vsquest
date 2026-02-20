using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsQuest
{
    public class EntityBehaviorBossDynamicScaling : EntityBehavior
    {
        private const string BaseHealthKey = "alegacyvsquest:bossdynam scaling:baseHealth";
        private const string ScaledHealthKey = "alegacyvsquest:bossdynamicscaling:scaledHealth";
        private const string LastPlayerCountKey = "alegacyvsquest:bossdynamicscaling:lastPlayerCount";
        private const string LastUpdateMsKey = "alegacyvsquest:bossdynamicscaling:lastUpdateMs";

        private ICoreServerAPI sapi;
        private float baseHealthMult;
        private float playerScaling;
        private float maxMultiplier;
        private float regenPerSecond;
        private int checkIntervalMs;
        private int playerDetectionRange;
        private int regenIntervalMs = 500;
        private long lastRegenMs;

        private EntityBehaviorHealth healthBehavior;

        public EntityBehaviorBossDynamicScaling(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bossdynamicscaling";

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            sapi = entity?.Api as ICoreServerAPI;

            baseHealthMult = attributes["baseHealthMult"].AsFloat(1.6f);
            playerScaling = attributes["hpPerPlayer"].AsFloat(0.30f);
            maxMultiplier = attributes["maxMultiplier"].AsFloat(4.0f);
            regenPerSecond = attributes["regenPerPlayer"].AsFloat(0.2f);
            checkIntervalMs = attributes["checkIntervalMs"].AsInt(2000);
            playerDetectionRange = attributes["playerDetectionRange"].AsInt(60);

            healthBehavior = entity.GetBehavior<EntityBehaviorHealth>();

            if (sapi != null && healthBehavior != null)
            {
                ApplyInitialScaling();
            }
        }

        private void ApplyInitialScaling()
        {
            var wa = entity.WatchedAttributes;
            float storedBaseHealth = wa.GetFloat(BaseHealthKey, 0f);

            if (storedBaseHealth <= 0f)
            {
                storedBaseHealth = healthBehavior.MaxHealth / baseHealthMult;
                wa.SetFloat(BaseHealthKey, storedBaseHealth);
            }

            int playerCount = CountNearbyPlayers();
            float multiplier = CalculateMultiplier(playerCount);
            float newMaxHealth = storedBaseHealth * multiplier;

            healthBehavior.MaxHealth = newMaxHealth;
            healthBehavior.Health = newMaxHealth;

            wa.SetFloat(ScaledHealthKey, newMaxHealth);
            wa.SetInt(LastPlayerCountKey, playerCount);
            wa.SetLong(LastUpdateMsKey, sapi.World.ElapsedMilliseconds);

            entity.Api.Logger.Debug($"[BossDynamicScaling] {entity.Code} scaled to {newMaxHealth} HP with {playerCount} players (x{multiplier})");
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            if (sapi == null || healthBehavior == null) return;
            if (!entity.Alive) return;

            long now = sapi.World.ElapsedMilliseconds;
            var wa = entity.WatchedAttributes;
            long lastUpdate = wa.GetLong(LastUpdateMsKey, 0);

            if (now - lastUpdate < checkIntervalMs) return;

            int playerCount = CountNearbyPlayers();
            int lastPlayerCount = wa.GetInt(LastPlayerCountKey, 1);

            if (playerCount != lastPlayerCount)
            {
                wa.SetInt(LastPlayerCountKey, playerCount);
                wa.SetLong(LastUpdateMsKey, now);
            }

            // Regeneration - only every 500ms to reduce server load
            if (playerCount > 0 && regenPerSecond > 0 && now - lastRegenMs >= regenIntervalMs)
            {
                lastRegenMs = now;
                float regenMult = 1f + (playerCount - 1) * 0.20f;
                float totalRegen = regenPerSecond * regenMult * (regenIntervalMs / 1000f);

                if (totalRegen > 0 && healthBehavior.Health < healthBehavior.MaxHealth)
                {
                    healthBehavior.Health = Math.Min(healthBehavior.MaxHealth, healthBehavior.Health + totalRegen);
                }
            }
        }

        private int CountNearbyPlayers()
        {
            int count = 0;
            foreach (var player in sapi.World.AllOnlinePlayers)
            {
                if (player.Entity?.ServerPos == null) continue;
                if (player.Entity.ServerPos.Dimension != entity.ServerPos.Dimension) continue;
                if (player.Entity.ServerPos.DistanceTo(entity.ServerPos) <= playerDetectionRange)
                {
                    count++;
                }
            }
            return Math.Max(1, count);
        }

        private float CalculateMultiplier(int playerCount)
        {
            if (playerCount <= 1) return baseHealthMult;

            float multiplier = baseHealthMult * (1f + (playerCount - 1) * playerScaling);

            if (playerCount > 4)
            {
                multiplier *= (1f + (playerCount - 4) * 0.10f);
            }

            return Math.Min(multiplier, baseHealthMult * maxMultiplier);
        }
    }
}
