using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace VsQuest.Harmony.Items
{
    /// <summary>
    /// Cached wearable stats to avoid recalculating on every stat check.
    /// Uses time-based expiration (500ms) instead of invalidation.
    /// </summary>
    public static class WearableStatsCache
    {
        private const string CacheKey = "vsquest:wearablestatscache:v3"; // v3: combined storage
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
            public float AttackPower { get; set; }
            public float RangedDamageMult { get; set; }
            public long Timestamp { get; set; }

            // Fast check if cache is still valid
            public bool IsValid(long nowMs) => nowMs > 0 && Timestamp > 0 && (nowMs - Timestamp) < CacheValidityMs;
        }

        public static CachedStats GetCachedStats(EntityPlayer player)
        {
            if (player?.Player?.InventoryManager == null) return null;
            var inv = player.Player.InventoryManager.GetOwnInventory("character");
            var backpackInv = player.Player.InventoryManager.GetOwnInventory("backpack");
            if (inv == null && backpackInv == null) return null;

            long nowMs = player.World?.ElapsedMilliseconds ?? 0;

            // Try get cached stats in single read
            var cached = GetStatsFromCache(player, nowMs);
            if (cached != null) return cached;

            // Recalculate and cache
            var stats = CalculateStats(inv, backpackInv);
            stats.Timestamp = nowMs;
            StoreStatsInCache(player, stats);
            return stats;
        }

        public static void InvalidateCache(EntityPlayer player)
        {
            // Set timestamp to 0 to invalidate without clearing data
            var tree = player.WatchedAttributes?.GetTreeAttribute(CacheKey);
            tree?.SetLong("t", 0);
        }

        private static CachedStats CalculateStats(IInventory inv, IInventory backpackInv)
        {
            var stats = new CachedStats();

            // Check character slots for ItemWearable
            if (inv != null)
            {
                foreach (ItemSlot slot in inv)
                {
                    if (slot.Empty || slot.Itemstack?.Item is not ItemWearable) continue;
                    AddItemAttributes(stats, slot.Itemstack);
                }
            }

            // Check backpack slots for attachments (quiver, etc.)
            if (backpackInv != null)
            {
                foreach (ItemSlot slot in backpackInv)
                {
                    if (slot.Empty || slot.Itemstack?.Collectible == null) continue;
                    var backpackAttr = slot.Itemstack.Collectible.Attributes?["backpack"];
                    if (backpackAttr == null || !backpackAttr.Exists) continue;
                    AddItemAttributes(stats, slot.Itemstack);
                }
            }

            return stats;
        }

        private static void AddItemAttributes(CachedStats stats, ItemStack stack)
        {
            stats.Stealth += ItemAttributeUtils.GetAttributeFloatScaled(stack, ItemAttributeUtils.AttrStealth);
            stats.FallDamageReduction += ItemAttributeUtils.GetAttributeFloatScaled(stack, ItemAttributeUtils.AttrFallDamageMult);
            stats.TemporalDrainReduction += ItemAttributeUtils.GetAttributeFloatScaled(stack, ItemAttributeUtils.AttrTemporalDrainMult);
            stats.MeleeAttackSpeed += ItemAttributeUtils.GetAttributeFloatScaled(stack, ItemAttributeUtils.AttrMeleeAttackSpeed);
            stats.MiningSpeedMult += ItemAttributeUtils.GetAttributeFloatScaled(stack, ItemAttributeUtils.AttrMiningSpeedMult);
            stats.FlatProtection += ItemAttributeUtils.GetAttributeFloatScaled(stack, ItemAttributeUtils.AttrProtection);
            stats.PercProtection += ItemAttributeUtils.GetAttributeFloatScaled(stack, ItemAttributeUtils.AttrProtectionPerc);
            stats.KnockbackMult += ItemAttributeUtils.GetAttributeFloatScaled(stack, ItemAttributeUtils.AttrKnockbackMult);
            stats.AttackPower += ItemAttributeUtils.GetAttributeFloatScaled(stack, ItemAttributeUtils.AttrAttackPower);
            stats.RangedDamageMult += ItemAttributeUtils.GetAttributeFloatScaled(stack, ItemAttributeUtils.AttrRangedDamageMult);
        }

        private static CachedStats GetStatsFromCache(EntityPlayer player, long nowMs)
        {
            try
            {
                var tree = player.WatchedAttributes?.GetTreeAttribute(CacheKey);
                if (tree == null) return null;

                long timestamp = tree.GetLong("t");
                // Check validity inline to avoid second WatchedAttributes read
                if (nowMs > 0 && timestamp > 0 && (nowMs - timestamp) < CacheValidityMs)
                {
                    return new CachedStats
                    {
                        Stealth = tree.GetFloat("s"),
                        FallDamageReduction = tree.GetFloat("f"),
                        TemporalDrainReduction = tree.GetFloat("tp"),
                        MeleeAttackSpeed = tree.GetFloat("a"),
                        MiningSpeedMult = tree.GetFloat("m"),
                        FlatProtection = tree.GetFloat("pf"),
                        PercProtection = tree.GetFloat("pp"),
                        KnockbackMult = tree.GetFloat("k"),
                        AttackPower = tree.GetFloat("ap"),
                        RangedDamageMult = tree.GetFloat("rd"),
                        Timestamp = timestamp
                    };
                }
                return null;
            }
            catch { return null; }
        }

        private static void StoreStatsInCache(EntityPlayer player, CachedStats stats)
        {
            try
            {
                var tree = new TreeAttribute();
                // Short keys for smaller serialization
                tree.SetFloat("s", stats.Stealth);
                tree.SetFloat("f", stats.FallDamageReduction);
                tree.SetFloat("tp", stats.TemporalDrainReduction);
                tree.SetFloat("a", stats.MeleeAttackSpeed);
                tree.SetFloat("m", stats.MiningSpeedMult);
                tree.SetFloat("pf", stats.FlatProtection);
                tree.SetFloat("pp", stats.PercProtection);
                tree.SetFloat("k", stats.KnockbackMult);
                tree.SetFloat("ap", stats.AttackPower);
                tree.SetFloat("rd", stats.RangedDamageMult);
                tree.SetLong("t", stats.Timestamp);
                player.WatchedAttributes.SetAttribute(CacheKey, tree);
            }
            catch { }
        }
    }
}
