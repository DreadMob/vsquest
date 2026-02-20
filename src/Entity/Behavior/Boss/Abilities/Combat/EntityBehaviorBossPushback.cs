using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class EntityBehaviorBossPushback : EntityBehavior
    {
        private const string LastPushKey = "alegacyvsquest:bosspushback:lastPushMs";
        private const string PlayerCooldownKey = "alegacyvsquest:bosspushback:playerCooldown:";

        private ICoreServerAPI sapi;
        private float triggerRange;
        private float knockbackStrength;
        private int cooldownMs;
        private int playerCooldownMs;
        private int detectionRange;

        private long lastTickMs;

        public EntityBehaviorBossPushback(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bosspushback";

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            sapi = entity?.Api as ICoreServerAPI;
            triggerRange = attributes["range"].AsFloat(4f);
            knockbackStrength = attributes["knockback"].AsFloat(0.45f);
            cooldownMs = attributes["cooldownSeconds"].AsInt(6) * 1000;
            playerCooldownMs = attributes["playerCooldownSeconds"].AsInt(8) * 1000;
            detectionRange = attributes["detectionRange"].AsInt(60);

            lastTickMs = sapi?.World.ElapsedMilliseconds ?? 0;
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            if (sapi == null || !entity.Alive) return;

            long now = sapi.World.ElapsedMilliseconds;
            if (now - lastTickMs < 1000) return; // Check every second
            lastTickMs = now;

            // Check if cooldown expired
            long lastPush = entity.WatchedAttributes.GetLong(LastPushKey, 0);
            if (now - lastPush < cooldownMs) return;

            // Find nearby players
            List<EntityPlayer> nearbyPlayers = new List<EntityPlayer>();
            foreach (var player in sapi.World.AllOnlinePlayers)
            {
                if (player.Entity?.ServerPos == null) continue;
                if (player.Entity.ServerPos.Dimension != entity.ServerPos.Dimension) continue;

                double dist = player.Entity.ServerPos.DistanceTo(entity.ServerPos);
                if (dist <= triggerRange)
                {
                    // Check player-specific cooldown
                    string playerKey = PlayerCooldownKey + player.Entity.EntityId;
                    long playerLastPush = entity.WatchedAttributes.GetLong(playerKey, 0);
                    if (now - playerLastPush >= playerCooldownMs)
                    {
                        nearbyPlayers.Add(player.Entity);
                    }
                }
            }

            if (nearbyPlayers.Count == 0) return;

            // Perform pushback
            entity.WatchedAttributes.SetLong(LastPushKey, now);

            foreach (var player in nearbyPlayers)
            {
                // Set player cooldown
                string playerKey = PlayerCooldownKey + player.EntityId;
                entity.WatchedAttributes.SetLong(playerKey, now);

                // Calculate knockback direction
                Vec3d dir = player.ServerPos.XYZ - entity.ServerPos.XYZ;
                dir.Normalize();
                dir.Y = 0.3; // Slightly up
                dir.Mul(knockbackStrength);

                // Apply knockback
                player.ServerPos.Motion.Add(dir);

                // Visual effect
                var playerPos = player.ServerPos.XYZ;
                sapi.World.SpawnParticles(
                    new SimpleParticleProperties(
                        5, 10,
                        ColorUtil.ToRgba(200, 255, 200, 100),
                        playerPos,
                        playerPos.Add(0.5, 0.5, 0.5),
                        new Vec3f(-0.2f, 0.1f, -0.2f),
                        new Vec3f(0.2f, 0.3f, 0.2f),
                        0.5f,
                        0.3f,
                        0.5f,
                        0.5f,
                        EnumParticleModel.Quad
                    )
                );
            }
        }
    }
}
