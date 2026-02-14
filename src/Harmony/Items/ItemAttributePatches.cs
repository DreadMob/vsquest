using System;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsQuest.Harmony
{
    public static class WearableStatsCache
    {
        private const string CacheKey = "vsquest:wearablestatscache:v2";
        private const string CacheTimestampKey = "vsquest:wearablecache:timestamp";
        private const int CacheValidityMs = 500; // Cache valid for 500ms

        public class CachedStats
        {
            public float Stealth { get; set; }
            public float FallDamageReduction { get; set; }
            public float TemporalDrainReduction { get; set; }
            public float MeleeAttackSpeed { get; set; }
            public float MiningSpeedMult { get; set; }
            public float FlatProtection { get; set; }
            public float PercProtection { get; set; }
            public float KnockbackMult { get; set; }
            public long Timestamp { get; set; }
        }

        public static CachedStats GetCachedStats(EntityPlayer player)
        {
            if (player?.Player?.InventoryManager == null) return null;
            var inv = player.Player.InventoryManager.GetOwnInventory("character");
            if (inv == null) return null;

            long nowMs = player.World?.ElapsedMilliseconds ?? 0;
            long cachedTime = player.WatchedAttributes.GetLong(CacheTimestampKey, 0);

            // If cache is still fresh, use it
            if (nowMs > 0 && cachedTime > 0 && (nowMs - cachedTime) < CacheValidityMs)
            {
                var cached = GetStatsFromCache(player);
                if (cached != null) return cached;
            }

            // Recalculate and cache
            var stats = CalculateStats(inv);
            stats.Timestamp = nowMs;
            StoreStatsInCache(player, stats);
            return stats;
        }

        public static void InvalidateCache(EntityPlayer player)
        {
            player.WatchedAttributes.SetLong(CacheTimestampKey, 0);
        }

        private static CachedStats CalculateStats(IInventory inv)
        {
            var stats = new CachedStats();
            foreach (ItemSlot slot in inv)
            {
                if (slot.Empty || slot.Itemstack?.Item is not ItemWearable) continue;
                var stack = slot.Itemstack;
                stats.Stealth += ItemAttributeUtils.GetAttributeFloatScaled(stack, ItemAttributeUtils.AttrStealth);
                stats.FallDamageReduction += ItemAttributeUtils.GetAttributeFloatScaled(stack, ItemAttributeUtils.AttrFallDamageMult);
                stats.TemporalDrainReduction += ItemAttributeUtils.GetAttributeFloatScaled(stack, ItemAttributeUtils.AttrTemporalDrainMult);
                stats.MeleeAttackSpeed += ItemAttributeUtils.GetAttributeFloatScaled(stack, ItemAttributeUtils.AttrMeleeAttackSpeed);
                stats.MiningSpeedMult += ItemAttributeUtils.GetAttributeFloatScaled(stack, ItemAttributeUtils.AttrMiningSpeedMult);
                stats.FlatProtection += ItemAttributeUtils.GetAttributeFloatScaled(stack, ItemAttributeUtils.AttrProtection);
                stats.PercProtection += ItemAttributeUtils.GetAttributeFloatScaled(stack, ItemAttributeUtils.AttrProtectionPerc);
                stats.KnockbackMult += ItemAttributeUtils.GetAttributeFloatScaled(stack, ItemAttributeUtils.AttrKnockbackMult);
            }
            return stats;
        }

        private static CachedStats GetStatsFromCache(EntityPlayer player)
        {
            try
            {
                var tree = player.WatchedAttributes.GetTreeAttribute(CacheKey);
                if (tree == null) return null;
                return new CachedStats
                {
                    Stealth = tree.GetFloat("stealth"),
                    FallDamageReduction = tree.GetFloat("fall"),
                    TemporalDrainReduction = tree.GetFloat("temporal"),
                    MeleeAttackSpeed = tree.GetFloat("attackspeed"),
                    MiningSpeedMult = tree.GetFloat("mining"),
                    FlatProtection = tree.GetFloat("protflat"),
                    PercProtection = tree.GetFloat("protperc"),
                    KnockbackMult = tree.GetFloat("knockback"),
                    Timestamp = tree.GetLong("timestamp")
                };
            }
            catch { return null; }
        }

        private static void StoreStatsInCache(EntityPlayer player, CachedStats stats)
        {
            try
            {
                var tree = new TreeAttribute();
                tree.SetFloat("stealth", stats.Stealth);
                tree.SetFloat("fall", stats.FallDamageReduction);
                tree.SetFloat("temporal", stats.TemporalDrainReduction);
                tree.SetFloat("attackspeed", stats.MeleeAttackSpeed);
                tree.SetFloat("mining", stats.MiningSpeedMult);
                tree.SetFloat("protflat", stats.FlatProtection);
                tree.SetFloat("protperc", stats.PercProtection);
                tree.SetFloat("knockback", stats.KnockbackMult);
                tree.SetLong("timestamp", stats.Timestamp);
                player.WatchedAttributes.SetAttribute(CacheKey, tree);
                player.WatchedAttributes.SetLong(CacheTimestampKey, stats.Timestamp);
            }
            catch { }
        }
    }

    public class ItemAttributePatches
    {
        private const string MeleeAttackCooldownKey = "alegacyvsquest:meleeattackspeed:last";
        private const int BaseMeleeAttackCooldownMs = 650;
        private static readonly bool EnableTemporalStabilityWearablePatch = true;

        [HarmonyPatch(typeof(CollectibleObject), "GetHeldItemName")]
        public class CollectibleObject_GetHeldItemName_ActionItem_ItemizerName_Patch
        {
            public static void Postfix(ItemStack itemStack, ref string __result)
            {
                if (itemStack?.Attributes == null) return;

                string actions = itemStack.Attributes.GetString(ItemAttributeUtils.ActionItemActionsKey);
                if (string.IsNullOrWhiteSpace(actions)) return;

                string customName = itemStack.Attributes.GetString(ItemAttributeUtils.QuestNameKey);
                if (string.IsNullOrWhiteSpace(customName)) return;

                // Preserve VTML/color markup if the stored name already contains it.
                if (customName.IndexOf('<') >= 0)
                {
                    __result = customName;
                    return;
                }

                __result = $"<i>{customName}</i>";
            }
        }

        [HarmonyPatch(typeof(ModSystemWearableStats), "onFootStep")]
        public class ModSystemWearableStats_onFootStep_Patch
        {
            public static bool Prefix(EntityPlayer entity)
            {
                var stats = WearableStatsCache.GetCachedStats(entity);
                return stats == null || stats.Stealth <= 0f;
            }
        }

        [HarmonyPatch(typeof(EntityBehaviorHealth), "OnFallToGround")]
        public class EntityBehaviorHealth_OnFallToGround_Patch
        {
            public static void Prefix(EntityBehaviorHealth __instance, ref float __state)
            {
                if (__instance?.entity is not EntityPlayer player) return;

                var stats = WearableStatsCache.GetCachedStats(player);
                if (stats == null || stats.FallDamageReduction == 0f) return;

                __state = __instance.entity.Properties.FallDamageMultiplier;
                float mult = GameMath.Clamp(1f - stats.FallDamageReduction, 0f, 2f);
                __instance.entity.Properties.FallDamageMultiplier = __state * mult;
            }

            public static void Postfix(EntityBehaviorHealth __instance, float __state)
            {
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
                if (!EnableTemporalStabilityWearablePatch) return;
                __state = __instance?.OwnStability ?? 0.0;
            }

            public static void Postfix(EntityBehaviorTemporalStabilityAffected __instance, double __state)
            {
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
            public static void Postfix(ModSystemWearableStats __instance, IInventory inv, IServerPlayer player)
            {
                if (player == null || player.Entity == null || player.Entity.Stats == null) return;

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

                player.Entity.Stats.Set("walkspeed", "vsquestmod", bonusMods.walkSpeed, true);
                player.Entity.Stats.Set("healingeffectivness", "vsquestmod", bonusMods.healingeffectivness, true);
                player.Entity.Stats.Set("hungerrate", "vsquestmod", bonusMods.hungerrate, true);
                player.Entity.Stats.Set("rangedWeaponsAcc", "vsquestmod", bonusMods.rangedWeaponsAcc, true);
                player.Entity.Stats.Set("rangedWeaponsSpeed", "vsquestmod", bonusMods.rangedWeaponsSpeed, true);
                player.Entity.Stats.Set("miningSpeedMul", "vsquestmod", miningSpeedMult, true);
                player.Entity.Stats.Set("jumpHeightMul", "vsquestmod", jumpHeightMult, true);

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

                float weightLimit = GetWeightLimit(inv);
                float weightPenalty = GetInventoryFillRatio(player) * weightLimit;
                if (weightPenalty > 0f)
                {
                    player.Entity.Stats.Set("walkspeed", "vsquestmod:weightlimit", -weightPenalty, true);
                }
                else
                {
                    player.Entity.Stats.Set("walkspeed", "vsquestmod:weightlimit", 0f, true);
                }

                BossBehaviorUtils.UpdatePlayerWalkSpeed(player.Entity);
                
                // Invalidate cache when wearable stats are updated
                if (player.Entity is EntityPlayer entityPlayer)
                {
                    WearableStatsCache.InvalidateCache(entityPlayer);
                }
            }

            private static float GetWeightLimit(IInventory inv)
            {
                float total = 0f;
                foreach (ItemSlot slot in inv)
                {
                    if (!slot.Empty && slot.Itemstack?.Item is ItemWearable)
                    {
                        total += ItemAttributeUtils.GetAttributeFloatScaled(slot.Itemstack, ItemAttributeUtils.AttrWeightLimit);
                    }
                }

                return Math.Max(0f, total);
            }

            private static float GetInventoryFillRatio(IServerPlayer player)
            {
                var invManager = player?.InventoryManager;
                if (invManager?.Inventories == null) return 0f;

                int totalSlots = 0;
                int filledSlots = 0;
                foreach (var kvp in invManager.Inventories)
                {
                    var inventory = kvp.Value;
                    if (inventory == null) continue;
                    if (inventory.ClassName == GlobalConstants.creativeInvClassName) continue;

                    for (int i = 0; i < inventory.Count; i++)
                    {
                        var slot = inventory[i];
                        if (slot == null) continue;
                        totalSlots++;
                        if (!slot.Empty) filledSlots++;
                    }
                }

                if (totalSlots == 0) return 0f;
                return Math.Min(1f, Math.Max(0f, filledSlots / (float)totalSlots));
            }
        }

        // Cache invalidation patch removed - cache uses time-based expiration instead

        [HarmonyPatch(typeof(CollectibleObject), "TryMergeStacks")]
        public class CollectibleObject_TryMergeStacks_SecondChanceCharge_Patch
        {
            public static bool Prefix(CollectibleObject __instance, ItemStackMergeOperation op)
            {
                if (TryHandleSecondChanceCharge(op)) return false;
                if (TryHandleUraniumMaskCharge(op)) return false;
                return true;
            }
        }

        [HarmonyPatch(typeof(ItemWearable), "TryMergeStacks")]
        public class ItemWearable_TryMergeStacks_SecondChanceCharge_Patch
        {
            public static bool Prefix(ItemWearable __instance, ItemStackMergeOperation op)
            {
                if (TryHandleSecondChanceCharge(op)) return false;
                if (TryHandleUraniumMaskCharge(op)) return false;
                return true;
            }
        }

        [HarmonyPatch(typeof(ItemWearable), "GetMergableQuantity")]
        public class ItemWearable_GetMergableQuantity_SecondChanceCharge_Patch
        {
            public static bool Prefix(ItemWearable __instance, ItemStack sinkStack, ItemStack sourceStack, EnumMergePriority priority, ref int __result)
            {
                if (CanChargeSecondChance(sinkStack, sourceStack) || CanChargeUraniumMask(sinkStack, sourceStack))
                {
                    __result = 1;
                    return false;
                }

                return true;
            }
        }

        private static bool TryHandleSecondChanceCharge(ItemStackMergeOperation op)
        {
            if (op?.SinkSlot?.Itemstack == null || op.SourceSlot?.Itemstack == null) return false;

            var sinkStack = op.SinkSlot.Itemstack;
            if (!CanChargeSecondChance(sinkStack, op.SourceSlot.Itemstack)) return false;

            string chargeKey = ItemAttributeUtils.GetKey(ItemAttributeUtils.AttrSecondChanceCharges);
            sinkStack.Attributes.SetFloat(chargeKey, 1f);
            op.MovedQuantity = 1;
            op.SourceSlot.TakeOut(1);
            op.SinkSlot.MarkDirty();
            return true;
        }

        private static bool TryHandleUraniumMaskCharge(ItemStackMergeOperation op)
        {
            if (op?.SinkSlot?.Itemstack == null || op.SourceSlot?.Itemstack == null) return false;

            var sinkStack = op.SinkSlot.Itemstack;
            if (!CanChargeUraniumMask(sinkStack, op.SourceSlot.Itemstack)) return false;

            string chargeKey = ItemAttributeUtils.GetKey(ItemAttributeUtils.AttrUraniumMaskChargeHours);
            float hours = sinkStack.Attributes.GetFloat(chargeKey, 0f);
            hours = Math.Min(100f, hours + 8f);
            sinkStack.Attributes.SetFloat(chargeKey, hours);
            op.MovedQuantity = 1;
            op.SourceSlot.TakeOut(1);
            op.SinkSlot.MarkDirty();
            return true;
        }

        private static bool CanChargeSecondChance(ItemStack sinkStack, ItemStack sourceStack)
        {
            if (sinkStack?.Attributes == null || sourceStack?.Collectible?.Code == null) return false;

            string chargeKey = ItemAttributeUtils.GetKey(ItemAttributeUtils.AttrSecondChanceCharges);
            if (!sinkStack.Attributes.HasAttribute(chargeKey)) return false;

            if (!IsDiamondRough(sourceStack.Collectible.Code)) return false;
            if (!HasHighPotential(sourceStack)) return false;

            float charges = ItemAttributeUtils.GetAttributeFloat(sinkStack, ItemAttributeUtils.AttrSecondChanceCharges, 0f);
            return charges < 0.5f;
        }

        private static bool CanChargeUraniumMask(ItemStack sinkStack, ItemStack sourceStack)
        {
            if (sinkStack?.Attributes == null || sourceStack?.Collectible?.Code == null) return false;

            string chargeKey = ItemAttributeUtils.GetKey(ItemAttributeUtils.AttrUraniumMaskChargeHours);
            if (!sinkStack.Attributes.HasAttribute(chargeKey)) return false;

            if (!IsUraniumChargeItem(sourceStack.Collectible.Code)) return false;

            float hours = sinkStack.Attributes.GetFloat(chargeKey, 0f);
            return hours < 100f - 0.01f;
        }

        private static bool IsUraniumChargeItem(AssetLocation code)
        {
            return code != null
                && string.Equals(code.Domain, "game", StringComparison.OrdinalIgnoreCase)
                && code.Path != null
                && string.Equals(code.Path, "nugget-uranium", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsDiamondRough(AssetLocation code)
        {
            return code != null
                && string.Equals(code.Domain, "game", StringComparison.OrdinalIgnoreCase)
                && code.Path != null
                && string.Equals(code.Path, "gem-diamond-rough", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasHighPotential(ItemStack stack)
        {
            var attrs = stack?.Attributes;
            if (attrs == null) return false;

            string potentialText = attrs.GetString("potential", null);
            if (!string.IsNullOrWhiteSpace(potentialText))
            {
                return string.Equals(potentialText, "high", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(potentialText, "veryhigh", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(potentialText, "высокий", StringComparison.OrdinalIgnoreCase);
            }

            int potentialInt = attrs.GetInt("potential", int.MinValue);
            if (potentialInt != int.MinValue)
            {
                return potentialInt >= 3;
            }

            float potentialFloat = attrs.GetFloat("potential", float.NaN);
            if (!float.IsNaN(potentialFloat))
            {
                return potentialFloat >= 3f;
            }

            return false;
        }
    }
}
