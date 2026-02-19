using System;
using System.Collections.Generic;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using VsQuest.Systems.Performance;

namespace VsQuest.Harmony.Items
{
    /// <summary>
    /// Wearable stats patches - handles stat calculations from equipped items.
    /// Optimized with caching and batching.
    /// </summary>
    public class WearableStatsPatches
    {
        private const string MeleeAttackCooldownKey = "alegacyvsquest:meleeattackspeed:last";
        private const int BaseMeleeAttackCooldownMs = 650;
        private static readonly bool EnableTemporalStabilityWearablePatch = true;

        [HarmonyPatch(typeof(ModSystemWearableStats), "onFootStep")]
        public class ModSystemWearableStats_onFootStep_Patch
        {
            public static bool Prefix(EntityPlayer entity)
            {
                if (!VsQuest.HarmonyPatchSwitches.ItemEnabled(VsQuest.HarmonyPatchSwitches.Item_ModSystemWearableStats_onFootStep)) return true;
                var stats = WearableStatsCache.GetCachedStats(entity);
                return stats == null || stats.Stealth <= 0f;
            }
        }

        [HarmonyPatch(typeof(EntityBehaviorHealth), "OnFallToGround")]
        public class EntityBehaviorHealth_OnFallToGround_Patch
        {
            public static void Prefix(EntityBehaviorHealth __instance, ref float __state)
            {
                if (!VsQuest.HarmonyPatchSwitches.ItemEnabled(VsQuest.HarmonyPatchSwitches.Item_EntityBehaviorHealth_OnFallToGround)) return;
                if (__instance?.entity is not EntityPlayer player) return;

                var stats = WearableStatsCache.GetCachedStats(player);
                if (stats == null || stats.FallDamageReduction == 0f) return;

                __state = __instance.entity.Properties.FallDamageMultiplier;
                float mult = GameMath.Clamp(1f - stats.FallDamageReduction, 0f, 2f);
                __instance.entity.Properties.FallDamageMultiplier = __state * mult;
            }

            public static void Postfix(EntityBehaviorHealth __instance, float __state)
            {
                if (!VsQuest.HarmonyPatchSwitches.ItemEnabled(VsQuest.HarmonyPatchSwitches.Item_EntityBehaviorHealth_OnFallToGround)) return;
                if (__instance?.entity == null) return;
                if (__state <= 0f) return;

                __instance.entity.Properties.FallDamageMultiplier = __state;
            }
        }

        [HarmonyPatch(typeof(EntityBehaviorTemporalStabilityAffected), "OnGameTick")]
        public class EntityBehaviorTemporalStabilityAffected_OnGameTick_Patch
        {
            public static void Prefix(EntityBehaviorTemporalStabilityAffected __instance, ref double __state)
            {
                if (!VsQuest.HarmonyPatchSwitches.ItemEnabled(VsQuest.HarmonyPatchSwitches.Item_EntityBehaviorTemporalStabilityAffected_OnGameTick)) return;
                if (!EnableTemporalStabilityWearablePatch) return;
                __state = __instance?.OwnStability ?? 0.0;
            }

            public static void Postfix(EntityBehaviorTemporalStabilityAffected __instance, double __state)
            {
                if (!VsQuest.HarmonyPatchSwitches.ItemEnabled(VsQuest.HarmonyPatchSwitches.Item_EntityBehaviorTemporalStabilityAffected_OnGameTick)) return;
                if (!EnableTemporalStabilityWearablePatch) return;
                if (__instance?.entity is not EntityPlayer player) return;

                var stats = WearableStatsCache.GetCachedStats(player);
                if (stats == null || Math.Abs(stats.TemporalDrainReduction) <= 0.0001f) return;

                double delta = __instance.OwnStability - __state;
                if (delta >= 0) return;

                float mult = GameMath.Clamp(1f - stats.TemporalDrainReduction, 0f, 3f);
                double adjusted = __state + delta * mult;
                __instance.OwnStability = GameMath.Clamp(adjusted, 0.0, 1.0);
            }
        }

        [HarmonyPatch(typeof(CollectibleObject), "GetAttackPower")]
        public class CollectibleObject_GetAttackPower_Patch
        {
            public static void Postfix(CollectibleObject __instance, IItemStack withItemStack, ref float __result)
            {
                if (!VsQuest.HarmonyPatchSwitches.ItemEnabled(VsQuest.HarmonyPatchSwitches.Item_CollectibleObject_GetAttackPower)) return;
                if (withItemStack is ItemStack stack)
                {
                    float bonus = ItemAttributeUtils.GetAttributeFloatScaled(stack, ItemAttributeUtils.AttrAttackPower);
                    __result += bonus;

                    // Some vanilla logic can break interactions (attacks/mining) if attack power becomes <= 0.
                    // Allow debuffs, but never let the final value drop below a tiny positive threshold.
                    if (__result <= 0.001f)
                    {
                        __result = 0.001f;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(CollectibleObject), "OnHeldAttackStart")]
        public class CollectibleObject_OnHeldAttackStart_AttackSpeed_Patch
        {
            public static bool Prefix(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
            {
                if (!VsQuest.HarmonyPatchSwitches.ItemEnabled(VsQuest.HarmonyPatchSwitches.Item_CollectibleObject_OnHeldAttackStart_AttackSpeed)) return true;
                if (byEntity is not EntityPlayer player) return true;

                var stats = WearableStatsCache.GetCachedStats(player);
                if (stats == null || Math.Abs(stats.MeleeAttackSpeed) < 0.001f) return true;

                float mult = GameMath.Clamp(1f - stats.MeleeAttackSpeed, 0.15f, 3f);
                long nowMs = byEntity.World.ElapsedMilliseconds;
                long lastMs = byEntity.WatchedAttributes.GetLong(MeleeAttackCooldownKey, 0);
                long cooldownMs = (long)(BaseMeleeAttackCooldownMs * mult);

                if (nowMs - lastMs < cooldownMs)
                {
                    handling = EnumHandHandling.PreventDefault;
                    return false;
                }

                byEntity.WatchedAttributes.SetLong(MeleeAttackCooldownKey, nowMs);
                return true;
            }
        }

        [HarmonyPatch(typeof(ItemWearable), "GetWarmth")]
        public class ItemWearable_GetWarmth_Patch
        {
            public static void Postfix(ItemWearable __instance, ItemSlot inslot, ref float __result)
            {
                if (!VsQuest.HarmonyPatchSwitches.ItemEnabled(VsQuest.HarmonyPatchSwitches.Item_ItemWearable_GetWarmth)) return;
                if (inslot.Itemstack is ItemStack stack)
                {
                    float bonus = ItemAttributeUtils.GetAttributeFloatScaled(stack, ItemAttributeUtils.AttrWarmth);
                    __result += bonus;
                }
            }
        }

        [HarmonyPatch(typeof(CollectibleObject), "GetMiningSpeed")]
        public class CollectibleObject_GetMiningSpeed_MiningSpeedMult_Patch
        {
            public static void Postfix(CollectibleObject __instance, IItemStack itemstack, BlockSelection blockSel, Block block, IPlayer forPlayer, ref float __result)
            {
                if (!VsQuest.HarmonyPatchSwitches.ItemEnabled(VsQuest.HarmonyPatchSwitches.Item_CollectibleObject_GetMiningSpeed_MiningSpeedMult)) return;
                if (__result <= 0f) return;
                if (forPlayer?.Entity is not EntityPlayer player) return;

                var stats = WearableStatsCache.GetCachedStats(player);
                if (stats == null || Math.Abs(stats.MiningSpeedMult) < 0.0001f) return;

                float mult = GameMath.Clamp(1f + stats.MiningSpeedMult, 0f, 10f);
                __result *= mult;
            }
        }

        [HarmonyPatch(typeof(ModSystemWearableStats), "handleDamaged")]
        public class ModSystemWearableStats_handleDamaged_Patch
        {
            public static void Postfix(ModSystemWearableStats __instance, IPlayer player, float damage, DamageSource dmgSource, ref float __result)
            {
                if (!VsQuest.HarmonyPatchSwitches.ItemEnabled(VsQuest.HarmonyPatchSwitches.Item_ModSystemWearableStats_handleDamaged)) return;
                if (__result <= 0f) return;
                if (player?.Entity is not EntityPlayer entity) return;

                var stats = WearableStatsCache.GetCachedStats(entity);
                if (stats == null) return;

                float newDamage = __result;
                newDamage = System.Math.Max(0f, newDamage - stats.FlatProtection);
                newDamage *= (1f - System.Math.Max(0f, stats.PercProtection));

                __result = newDamage;
            }
        }

        [HarmonyPatch(typeof(ModSystemWearableStats), "updateWearableStats")]
        public class ModSystemWearableStats_updateWearableStats_Patch
        {
            // Throttling for walkSpeed updates to prevent jitter when updateWearableStats is called frequently (e.g. on damage)
            private static readonly Dictionary<long, long> LastWalkSpeedUpdateByEntityId = new Dictionary<long, long>();
            private const int WalkSpeedUpdateThrottleMs = 500;

            private const string LastAppliedPrefix = "vsquest:wearablestats:last:";
            private const float CompareEpsilon = 0.0001f;

            private static bool ApproximatelyEqual(float a, float b)
            {
                if (float.IsNaN(a) && float.IsNaN(b)) return true;
                if (float.IsNaN(a) || float.IsNaN(b)) return false;
                return Math.Abs(a - b) <= CompareEpsilon;
            }

            public static void Postfix(ModSystemWearableStats __instance, IInventory inv, IServerPlayer player)
            {
                if (!VsQuest.HarmonyPatchSwitches.ItemEnabled(VsQuest.HarmonyPatchSwitches.Item_ModSystemWearableStats_updateWearableStats)) return;
                if (player == null || player.Entity == null || player.Entity.Stats == null) return;

                // Early exit: Check if inventory actually changed using fast fingerprinting
                if (player.Entity is EntityPlayer entityPlayer)
                {
                    if (!InventoryFingerprintSystem.ShouldRecalculateWearableStats(entityPlayer))
                    {
                        // Inventory hasn't meaningfully changed, skip all calculations
                        return;
                    }
                }

                StatModifiers bonusMods = new StatModifiers();
                bonusMods.walkSpeed = 0f;
                bonusMods.healingeffectivness = 0f;
                bonusMods.hungerrate = 0f;
                bonusMods.rangedWeaponsAcc = 0f;
                bonusMods.rangedWeaponsSpeed = 0f;
                float miningSpeedMult = 0f;
                float jumpHeightMult = 0f;
                float maxHealthFlat = 0f;
                float maxOxygenBonus = 0f;

                foreach (ItemSlot slot in inv)
                {
                    if (!slot.Empty && slot.Itemstack?.Item is ItemWearable)
                    {
                        bonusMods.walkSpeed += ItemAttributeUtils.GetAttributeFloatScaled(slot.Itemstack, ItemAttributeUtils.AttrWalkSpeed);
                        bonusMods.hungerrate += ItemAttributeUtils.GetAttributeFloatScaled(slot.Itemstack, ItemAttributeUtils.AttrHungerRate);
                        bonusMods.healingeffectivness += ItemAttributeUtils.GetAttributeFloatScaled(slot.Itemstack, ItemAttributeUtils.AttrHealingEffectiveness);
                        bonusMods.rangedWeaponsAcc += ItemAttributeUtils.GetAttributeFloatScaled(slot.Itemstack, ItemAttributeUtils.AttrRangedAccuracy);
                        bonusMods.rangedWeaponsSpeed += ItemAttributeUtils.GetAttributeFloatScaled(slot.Itemstack, ItemAttributeUtils.AttrRangedSpeed);
                        miningSpeedMult += ItemAttributeUtils.GetAttributeFloatScaled(slot.Itemstack, ItemAttributeUtils.AttrMiningSpeedMult);
                        jumpHeightMult += ItemAttributeUtils.GetAttributeFloatScaled(slot.Itemstack, ItemAttributeUtils.AttrJumpHeightMul);
                        maxHealthFlat += ItemAttributeUtils.GetAttributeFloatScaled(slot.Itemstack, ItemAttributeUtils.AttrMaxHealthFlat);
                        maxOxygenBonus += ItemAttributeUtils.GetAttributeFloatScaled(slot.Itemstack, ItemAttributeUtils.AttrMaxOxygen);
                    }
                }

                // If nothing changed since last apply, do nothing. This avoids repeated stat writes when vanilla calls updateWearableStats frequently (e.g. on damage).
                var wa = player.Entity.WatchedAttributes;
                float lastWalkSpeed = wa.GetFloat(LastAppliedPrefix + "walkspeed", float.NaN);
                float lastHealing = wa.GetFloat(LastAppliedPrefix + "healingeffectivness", float.NaN);
                float lastHunger = wa.GetFloat(LastAppliedPrefix + "hungerrate", float.NaN);
                float lastAcc = wa.GetFloat(LastAppliedPrefix + "rangedWeaponsAcc", float.NaN);
                float lastRangedSpeed = wa.GetFloat(LastAppliedPrefix + "rangedWeaponsSpeed", float.NaN);
                float lastMining = wa.GetFloat(LastAppliedPrefix + "miningSpeedMul", float.NaN);
                float lastJump = wa.GetFloat(LastAppliedPrefix + "jumpHeightMul", float.NaN);
                float lastMaxHealthFlat = wa.GetFloat(LastAppliedPrefix + "maxHealthFlat", float.NaN);
                float lastMaxOxygenBonus = wa.GetFloat(LastAppliedPrefix + "maxOxygenBonus", float.NaN);

                bool changed =
                    !ApproximatelyEqual(lastWalkSpeed, bonusMods.walkSpeed) ||
                    !ApproximatelyEqual(lastHealing, bonusMods.healingeffectivness) ||
                    !ApproximatelyEqual(lastHunger, bonusMods.hungerrate) ||
                    !ApproximatelyEqual(lastAcc, bonusMods.rangedWeaponsAcc) ||
                    !ApproximatelyEqual(lastRangedSpeed, bonusMods.rangedWeaponsSpeed) ||
                    !ApproximatelyEqual(lastMining, miningSpeedMult) ||
                    !ApproximatelyEqual(lastJump, jumpHeightMult) ||
                    !ApproximatelyEqual(lastMaxHealthFlat, maxHealthFlat) ||
                    !ApproximatelyEqual(lastMaxOxygenBonus, maxOxygenBonus);

                if (!changed)
                {
                    return;
                }

                // Use StatCoalescingEngine for batched updates instead of individual Stats.Set calls
                // This reduces network traffic by coalescing multiple stat changes into one sync
                if (player.Entity is EntityPlayer ep && ep.Api is ICoreServerAPI sapi)
                {
                    var statsToUpdate = new Dictionary<string, float>
                    {
                        ["walkspeed"] = bonusMods.walkSpeed,
                        ["healingeffectivness"] = bonusMods.healingeffectivness,
                        ["hungerrate"] = bonusMods.hungerrate,
                        ["rangedWeaponsAcc"] = bonusMods.rangedWeaponsAcc,
                        ["rangedWeaponsSpeed"] = bonusMods.rangedWeaponsSpeed,
                        ["miningSpeedMul"] = miningSpeedMult,
                        ["jumpHeightMul"] = jumpHeightMult
                    };

                    StatCoalescingEngine.QueueStatUpdates(sapi, ep, statsToUpdate);
                }
                else
                {
                    // Fallback to direct updates if coalescing not available
                    player.Entity.Stats.Set("walkspeed", "vsquestmod", bonusMods.walkSpeed, true);
                    player.Entity.Stats.Set("healingeffectivness", "vsquestmod", bonusMods.healingeffectivness, true);
                    player.Entity.Stats.Set("hungerrate", "vsquestmod", bonusMods.hungerrate, true);
                    player.Entity.Stats.Set("rangedWeaponsAcc", "vsquestmod", bonusMods.rangedWeaponsAcc, true);
                    player.Entity.Stats.Set("rangedWeaponsSpeed", "vsquestmod", bonusMods.rangedWeaponsSpeed, true);
                    player.Entity.Stats.Set("miningSpeedMul", "vsquestmod", miningSpeedMult, true);
                    player.Entity.Stats.Set("jumpHeightMul", "vsquestmod", jumpHeightMult, true);
                }

                var healthBehavior = player.Entity.GetBehavior<EntityBehaviorHealth>();
                if (healthBehavior != null)
                {
                    healthBehavior.SetMaxHealthModifiers("vsquestmod:attr:maxhealth", maxHealthFlat);
                }

                var oxygenBehavior = player.Entity.GetBehavior<EntityBehaviorBreathe>();
                if (oxygenBehavior != null)
                {
                    const string AppliedBonusKey = "alegacyvsquest:attr:maxoxygenbonusapplied";

                    float lastAppliedBonus = player.Entity.WatchedAttributes.GetFloat(AppliedBonusKey, 0f);

                    // Calculate a stable base that doesn't stack when updateWearableStats runs multiple times.
                    // We treat the current MaxOxygen as (base + lastAppliedBonus).
                    float baseOxygen = oxygenBehavior.MaxOxygen - lastAppliedBonus;
                    if (baseOxygen < 1f) baseOxygen = 1f;

                    float newMaxOxygen = Math.Max(1f, baseOxygen + maxOxygenBonus);
                    oxygenBehavior.MaxOxygen = newMaxOxygen;

                    // If max oxygen decreased (e.g. accessory removed), ensure current oxygen cannot exceed the new max.
                    float currentOxygen = oxygenBehavior.Oxygen;
                    if (currentOxygen > newMaxOxygen)
                    {
                        oxygenBehavior.Oxygen = newMaxOxygen;
                    }
                    player.Entity.WatchedAttributes.SetFloat(AppliedBonusKey, maxOxygenBonus);
                }

                // Throttle walkSpeed updates to reduce rubberbanding under frequent updateWearableStats calls
                long nowMs = player.Entity.World?.ElapsedMilliseconds ?? 0;
                long entityId = player.Entity.EntityId;
                bool shouldUpdateWalkSpeed = true;

                if (LastWalkSpeedUpdateByEntityId.TryGetValue(entityId, out long lastUpdate))
                {
                    if ((nowMs - lastUpdate) < WalkSpeedUpdateThrottleMs)
                    {
                        shouldUpdateWalkSpeed = false;
                    }
                }

                if (shouldUpdateWalkSpeed)
                {
                    BossBehaviorUtils.UpdatePlayerWalkSpeed(player.Entity);
                    LastWalkSpeedUpdateByEntityId[entityId] = nowMs;

                    // Cleanup to prevent memory bloat
                    if (LastWalkSpeedUpdateByEntityId.Count > 512)
                    {
                        LastWalkSpeedUpdateByEntityId.Clear();
                    }
                }
                
                // Invalidate cache when wearable stats are updated
                if (player.Entity is EntityPlayer entityPlayer2)
                {
                    WearableStatsCache.InvalidateCache(entityPlayer2);
                }

                // Persist last applied values so we can skip redundant writes next time.
                wa.SetFloat(LastAppliedPrefix + "walkspeed", bonusMods.walkSpeed);
                wa.SetFloat(LastAppliedPrefix + "healingeffectivness", bonusMods.healingeffectivness);
                wa.SetFloat(LastAppliedPrefix + "hungerrate", bonusMods.hungerrate);
                wa.SetFloat(LastAppliedPrefix + "rangedWeaponsAcc", bonusMods.rangedWeaponsAcc);
                wa.SetFloat(LastAppliedPrefix + "rangedWeaponsSpeed", bonusMods.rangedWeaponsSpeed);
                wa.SetFloat(LastAppliedPrefix + "miningSpeedMul", miningSpeedMult);
                wa.SetFloat(LastAppliedPrefix + "jumpHeightMul", jumpHeightMult);
                wa.SetFloat(LastAppliedPrefix + "maxHealthFlat", maxHealthFlat);
                wa.SetFloat(LastAppliedPrefix + "maxOxygenBonus", maxOxygenBonus);
            }
        }
    }
}
