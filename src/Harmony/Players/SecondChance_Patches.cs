using System;
using System.Collections.Generic;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using VsQuest.Systems.Performance;

namespace VsQuest.Harmony.Players
{
    [HarmonyPatch(typeof(EntityBehaviorHealth), "OnEntityReceiveDamage")]
    public class EntityBehaviorHealth_OnEntityReceiveDamage_SecondChance_Patch
    {
        public static void Prefix(EntityBehaviorHealth __instance, DamageSource damageSource, ref float damage)
        {
            if (!HarmonyPatchSwitches.PlayerEnabled(HarmonyPatchSwitches.Player_EntityBehaviorHealth_OnEntityReceiveDamage_SecondChance)) return;
            if (damageSource?.Type == EnumDamageType.Heal) return;

            if (__instance?.entity is not EntityPlayer player) return;

            if (player.World?.Side == EnumAppSide.Client) return;

            if (player.Player?.InventoryManager == null) return;

            if (damage <= 0f) return;

            // Use EntityBehaviorHealth.Health for current health value
            float health = __instance.Health;

            // Only trigger if this damage would kill the player
            if (health - damage > 0f) return;

            // Fast check: use cached flag instead of inventory scan
            const string cacheKey = "alegacyvsquest:secondchance:hasmask";
            if (!player.WatchedAttributes.GetBool(cacheKey)) return;

            // Now get the actual slot and charges
            if (!SecondChanceHelper.TryGetSecondChanceSlot(player, out var slot)) return;

            float charges = SecondChanceHelper.GetSecondChanceCharges(slot.Itemstack);

            if (charges < 1f) return;

            // Heal player to 70% max health
            float targetHealth = Math.Max(0.1f, __instance.MaxHealth * 0.7f);
            __instance.Health = targetHealth;

            damage = 0f;

            SecondChanceHelper.SetSecondChanceCharges(slot.Itemstack, charges - 1f);

            slot.MarkDirty();

            SecondChanceHelper.ApplySecondChanceDebuff(player);
            SecondChanceHelper.TryPlaySecondChanceSound(player);
        }
    }

    [HarmonyPatch(typeof(EntityBehaviorHealth), "OnEntityDeath")]
    public class EntityBehaviorHealth_OnEntityDeath_SecondChanceReset_Patch
    {
        public static void Prefix(EntityBehaviorHealth __instance, DamageSource damageSourceForDeath)
        {
            if (!HarmonyPatchSwitches.PlayerEnabled(HarmonyPatchSwitches.Player_EntityBehaviorHealth_OnEntityDeath_SecondChanceReset)) return;
            if (__instance?.entity is not EntityPlayer player) return;

            if (player.Player?.InventoryManager == null) return;

            try
            {
                if (player.Api?.Side != EnumAppSide.Server) return;

                var sapi = player.Api as ICoreServerAPI;

                var system = sapi?.ModLoader?.GetModSystem<VsQuest.BossHuntArenaSystem>();

                system?.TryHandlePlayerDeath(player);
            }
            catch (Exception e)
            {
                player?.Api?.Logger?.Error($"[vsquest] EntityBehaviorHealth.OnEntityDeath Prefix: {e}");
            }

            if (!SecondChanceHelper.TryGetSecondChanceSlot(player, out var slot)) return;

            SecondChanceHelper.SetSecondChanceCharges(slot.Itemstack, 0f);

            slot.MarkDirty();
        }
    }

    public static class SecondChanceHelper
    {
        private const string SecondChanceDebuffUntilKey = "alegacyvsquest:secondchance:debuffuntil";
        private const string SecondChanceDebuffStatKey = "alegacyvsquest:secondchance:debuff";
        private const string SecondChanceProcSound = "albase:sounds/atmospheric-metallic-swipe";
        private const float SecondChanceProcSoundRange = 24f;
        private const float SecondChanceDebuffWalkspeed = -0.2f;
        private const float SecondChanceDebuffHungerRate = 0.4f;
        private const float SecondChanceDebuffHealing = -0.3f;
        
        // Cached slot lookups to avoid scanning inventory every damage tick
        private static readonly System.Collections.Generic.Dictionary<long, ItemSlot> CachedSlots = new();
        private static readonly System.Collections.Generic.Dictionary<long, int> SlotCheckCounters = new();
        private const int SlotRecheckInterval = 100; // Recheck every 100 calls to handle item moves

        public static bool TryGetSecondChanceSlot(EntityPlayer player, out ItemSlot slot)
        {
            slot = null;
            
            // Only check the face slot (mask must be equipped)
            var inv = player.Player?.InventoryManager?.GetOwnInventory("character");
            if (inv == null) return false;

            // Face slot is typically index 9 in character inventory
            // Also check other wearable slots that might contain face items
            var faceSlot = inv[(int)EnumCharacterDressType.Face];
            
            if (faceSlot?.Empty == false && faceSlot.Itemstack?.Attributes != null)
            {
                string key = ItemAttributeUtils.GetKey(ItemAttributeUtils.AttrSecondChanceCharges);
                if (faceSlot.Itemstack.Attributes.HasAttribute(key))
                {
                    slot = faceSlot;
                    return true;
                }
            }

            return false;
        }

        public static void InvalidateCache(long entityId)
        {
            CachedSlots.Remove(entityId);
            SlotCheckCounters.Remove(entityId);
        }

        public static float GetSecondChanceCharges(ItemStack stack)
        {
            if (stack?.Attributes == null) return 0f;

            string key = ItemAttributeUtils.GetKey(ItemAttributeUtils.AttrSecondChanceCharges);

            return stack.Attributes.GetFloat(key, 0f);
        }

        public static void SetSecondChanceCharges(ItemStack stack, float value)
        {
            if (stack?.Attributes == null) return;

            string key = ItemAttributeUtils.GetKey(ItemAttributeUtils.AttrSecondChanceCharges);

            stack.Attributes.SetFloat(key, Math.Clamp(value, 0f, 3f));
        }

        public static void ApplySecondChanceDebuff(EntityPlayer player)
        {
            if (player?.Api is not ICoreServerAPI api) return;

            // Use ZeroPollEffectSystem for event-driven debuff with automatic cleanup
            PlayerEffectScheduler.ScheduleSecondChanceDebuff(api, player);
        }

        public static void TryPlaySecondChanceSound(EntityPlayer player)
        {
            if (player?.World == null) return;
            if (string.IsNullOrWhiteSpace(SecondChanceProcSound)) return;

            try
            {
                AssetLocation soundLoc = AssetLocation.Create(SecondChanceProcSound, "game").WithPathPrefixOnce("sounds/");
                player.World.PlaySoundAt(soundLoc, player.ServerPos.X, player.ServerPos.Y, player.ServerPos.Z, null, randomizePitch: true, SecondChanceProcSoundRange);
            }
            catch (Exception e)
            {
                player?.Api?.Logger?.Error($"[vsquest] TryPlaySecondChanceSound: {e}");
            }
        }

        public static void ApplyDebuffStats(EntityPlayer player)
        {
            player.Stats.Set("walkspeed", SecondChanceDebuffStatKey, SecondChanceDebuffWalkspeed, true);
            player.Stats.Set("hungerrate", SecondChanceDebuffStatKey, SecondChanceDebuffHungerRate, true);
            player.Stats.Set("healingeffectivness", SecondChanceDebuffStatKey, SecondChanceDebuffHealing, true);
            player.walkSpeed = player.Stats.GetBlended("walkspeed");
        }

        public static void ClearDebuff(EntityPlayer player)
        {
            player.Stats.Set("walkspeed", SecondChanceDebuffStatKey, 0f, true);
            player.Stats.Set("hungerrate", SecondChanceDebuffStatKey, 0f, true);
            player.Stats.Set("healingeffectivness", SecondChanceDebuffStatKey, 0f, true);
            player.walkSpeed = player.Stats.GetBlended("walkspeed");
        }
    }
}
