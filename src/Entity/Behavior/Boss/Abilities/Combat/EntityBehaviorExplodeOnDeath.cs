using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsQuest
{
    public class EntityBehaviorExplodeOnDeath : EntityBehavior
    {
        private ICoreServerAPI sapi;
        private int fuseMs;
        private float explosionRadius;
        private float explosionDamage;
        private int damageTier;
        private EnumDamageType damageType;
        private AssetLocation explodeSound;
        private float explodeSoundRange;
        private float explodeSoundVolume;
        
        private bool scheduled;
        private long explodeCallbackId;

        public EntityBehaviorExplodeOnDeath(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "explodeondeath";

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            sapi = entity?.Api as ICoreServerAPI;
            
            fuseMs = attributes["fuseMs"].AsInt(2000);
            explosionRadius = attributes["explosionRadius"].AsFloat(3f);
            explosionDamage = attributes["explosionDamage"].AsFloat(10f);
            damageTier = attributes["damageTier"].AsInt(1);
            damageType = (EnumDamageType)attributes["damageType"].AsInt((int)EnumDamageType.PiercingAttack);
            
            string soundPath = attributes["explodeSound"].AsString("effect/smallexplosion");
            explodeSound = new AssetLocation(soundPath);
            explodeSoundRange = attributes["explodeSoundRange"].AsFloat(16f);
            explodeSoundVolume = attributes["explodeSoundVolume"].AsFloat(0.5f);

            scheduled = false;
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            base.OnEntityDeath(damageSourceForDeath);
            
            if (sapi == null || scheduled) return;
            
            // Schedule explosion after fuse time
            scheduled = true;
            explodeCallbackId = sapi.Event.RegisterCallback((_) =>
            {
                TryExplode();
            }, fuseMs);
            
            // Play ticking fuse sound periodically
            int tickInterval = 500; // ms
            int totalTicks = fuseMs / tickInterval;
            for (int i = 1; i <= totalTicks; i++)
            {
                int tickDelay = i * tickInterval;
                if (tickDelay < fuseMs)
                {
                    sapi.Event.RegisterCallback((_) =>
                    {
                        if (entity != null && entity.Alive == false)
                        {
                            sapi.World.PlaySoundAt(
                                new AssetLocation("game:sounds/tick"),
                                entity,
                                null,
                                randomizePitch: true,
                                8,
                                0.3f
                            );
                        }
                    }, tickDelay);
                }
            }
        }

        private void TryExplode()
        {
            if (sapi == null) return;
            
            var pos = entity.ServerPos.XYZ;
            
            // Play explosion sound
            sapi.World.PlaySoundAt(
                explodeSound,
                entity,
                null,
                randomizePitch: false,
                (int)explodeSoundRange,
                explodeSoundVolume
            );
            
            // Create explosion
            var blockPos = new BlockPos((int)pos.X, (int)pos.Y, (int)pos.Z);
            sapi.World.CreateExplosion(
                blockPos, 
                EnumBlastType.EntityBlast, 
                explosionRadius, 
                explosionDamage
            );
            
            // Remove the entity
            entity.Die(EnumDespawnReason.Removed);
        }
    }
}
