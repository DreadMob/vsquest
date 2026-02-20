using System;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using VsQuest;
using VsQuest.Harmony.Items;

namespace VsQuest.Harmony.Players
{
    [HarmonyPatch(typeof(EntityAgent), "ReceiveDamage")]
    public class EntityAgent_ReceiveDamage_Patch
    {
        // Cached keys to avoid string allocations
        private static class AttrKeys
        {
            public const string BossCloneInvulnerable = "alegacyvsquest:bossclone:invulnerable";
            public const string BossClone = "alegacyvsquest:bossclone";
            public const string BossCloneOwnerId = "alegacyvsquest:bossclone:ownerid";
            public const string BossCloneDamageMult = "alegacyvsquest:bossclone:damagemult";
            public const string BossGrowthRitualDamageMult = "alegacyvsquest:bossgrowthritual:damagemult";
            public const string AdminAttackPower = "vsquestadmin:attr:attackpower";
            public const string FiredBy = "firedBy";
        }

        public static bool Prefix(EntityAgent __instance, DamageSource damageSource, ref float damage, ref bool __result)
        {
            // Ultra-fast rejects first (3 simple comparisons)
            if (damage <= 0f) return true; // Run original
            if (damageSource == null) return true; // Run original
            if (__instance?.Api?.Side != EnumAppSide.Server) return true; // Run original

            var watchedAttrs = __instance.WatchedAttributes;
            if (watchedAttrs == null) return true; // Run original

            // Boss clone invulnerability check
            if (watchedAttrs.GetBool(AttrKeys.BossCloneInvulnerable, false))
            {
                damage = 0f;
                damageSource.KnockbackStrength = 0f;
                __result = false; // Damage was handled (blocked)
                return false; // Skip original
            }

            // Single cached computation of source/cause entities
            var sourceEntity = damageSource.SourceEntity;
            var causeEntity = damageSource.GetCauseEntity() ?? sourceEntity;
            var sourceAttrs = sourceEntity?.WatchedAttributes ?? causeEntity?.WatchedAttributes;

            // Boss knockback immunity (zero knockback for bosses)
            if (damageSource.KnockbackStrength > 0f && BossDetection.IsBossTarget(__instance))
            {
                damageSource.KnockbackStrength = 0f;
            }

            // Knockback bonus from attacker gear (only for player attackers)
            if (causeEntity is EntityPlayer attacker && attacker.Player?.InventoryManager != null)
            {
                var cached = WearableStatsCache.GetCachedStats(attacker);
                if (cached?.KnockbackMult != 0f && damageSource.KnockbackStrength > 0f)
                {
                    damageSource.KnockbackStrength *= GameMath.Clamp(1f + cached.KnockbackMult, 0f, 5f);
                }
            }

            // Boss clone damage handling (single block)
            if (sourceAttrs != null && sourceAttrs.GetBool(AttrKeys.BossClone, false))
            {
                long ownerId = sourceAttrs.GetLong(AttrKeys.BossCloneOwnerId, 0);
                if (ownerId > 0 && __instance.EntityId == ownerId)
                {
                    damage = 0f;
                    damageSource.KnockbackStrength = 0f;
                    __result = false; // Damage was handled (blocked)
                    return false; // Skip original
                }

                // Damage multiplier from clone
                float mult = sourceAttrs.GetFloat(AttrKeys.BossCloneDamageMult, 1f);
                if (mult > 0f && mult < 0.999f)
                {
                    damage *= mult;
                }
            }

            // Fired by boss clone check (rare, keep simple)
            if (sourceEntity?.WatchedAttributes?.HasAttribute(AttrKeys.FiredBy) == true)
            {
                long firedById = sourceEntity.WatchedAttributes.GetLong(AttrKeys.FiredBy, 0);
                if (firedById > 0 && __instance.World?.GetEntityById(firedById) is Entity firedBy)
                {
                    var fbAttrs = firedBy.WatchedAttributes;
                    if (fbAttrs?.GetBool(AttrKeys.BossClone, false) == true)
                    {
                        long ownerId = fbAttrs.GetLong(AttrKeys.BossCloneOwnerId, 0);
                        if (ownerId > 0 && __instance.EntityId == ownerId)
                        {
                            damage = 0f;
                            damageSource.KnockbackStrength = 0f;
                            __result = false; // Damage was handled (blocked)
                            return false; // Skip original
                        }
                    }
                }
            }

            // Boss damage invulnerability check - blocks damage for X ms after each hit
            if (__instance.HasBehavior<EntityBehaviorBossDamageInvulnerability>())
            {
                var behavior = __instance.GetBehavior<EntityBehaviorBossDamageInvulnerability>();
                long now = __instance.Api.World.ElapsedMilliseconds;
                
                // Check if currently invulnerable
                if (EntityBehaviorBossDamageInvulnerability.ShouldBlockDamage(__instance, now))
                {
                    damage = 0f;
                    damageSource.KnockbackStrength = 0f;
                    __result = false;
                    return false; // Skip original - damage blocked
                }
                
                // Not invulnerable - allow damage but start invulnerability period
                int durationMs = behavior?.DurationMs ?? 1000;
                EntityBehaviorBossDamageInvulnerability.OnDamageReceived(__instance, durationMs);
            }

            // Continue to original method
            return true;
        }
    }

    public static class BossDetection
    {
        private const string BossTargetKey = "vsquest:isBossTarget";
        private const string BossTag = "albase-boss";

        public static bool IsBossTarget(EntityAgent target)
        {
            if (target == null) return false;

            // Fast path: cached attribute
            if (target.WatchedAttributes?.GetBool(BossTargetKey, false) == true)
                return true;

            // Check for boss tag
            if (target.Properties?.Attributes?["alegacy-boss"].AsBool(false) == true)
                return true;
            if (target.Properties?.Attributes?["boss"].AsBool(false) == true)
                return true;

            // Fallback: entity code patterns
            string codePath = target.Code?.Path;
            if (codePath != null)
            {
                if (codePath.Contains("boss") ||
                    codePath.Contains("bosshunt") ||
                    codePath.Contains("miniboss") ||
                    codePath.Contains("raidboss"))
                    return true;
            }

            // Last resort: HasBehavior - check for actual boss behaviors
            return target.HasBehavior<EntityBehaviorBossCombatMarker>() ||
                   target.HasBehavior<EntityBehaviorBossHuntCombatMarker>() ||
                   target.HasBehavior<EntityBehaviorQuestBoss>();
        }
    }
}
