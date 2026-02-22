using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using VsQuest.Harmony.Items;

namespace VsQuest.Systems.Items
{
    /// <summary>
    /// Periodic system for draining charge from items with *chargehours attributes.
    /// Runs once per minute - efficient for multiplayer servers.
    /// Works with ANY item that has attributes ending in "chargehours".
    /// </summary>
    public class ItemChargeDrainSystem : ModSystem
    {
        private ICoreServerAPI sapi;
        private long timerId;
        
        // Check interval in milliseconds (60 seconds)
        private const int CheckIntervalMs = 60000;
        
        // Key prefix for tracking last check time per attribute
        private const string LastCheckKeyPrefix = "vsquest:chargedrain:";

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Server;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            
            // Register periodic timer
            timerId = api.Event.RegisterCallback(OnTimerTick, CheckIntervalMs);
            
            api.Event.PlayerDisconnect += OnPlayerDisconnect;
            api.Logger.Notification("[vsquest] ItemChargeDrainSystem started (60s interval)");
        }

        public override void Dispose()
        {
            if (sapi != null && timerId != 0)
            {
                sapi.Event.UnregisterCallback(timerId);
            }
        }

        private void OnPlayerDisconnect(IServerPlayer player)
        {
            // Clean up stored times when player leaves - just clear the prefix
            var attrs = player.Entity?.WatchedAttributes;
            if (attrs == null) return;
            
            // Remove all charge drain tracking keys
            var tree = attrs as TreeAttribute;
            if (tree == null) return;
            
            var keysToRemove = new List<string>();
            foreach (var val in tree)
            {
                if (val.Key.StartsWith(LastCheckKeyPrefix))
                {
                    keysToRemove.Add(val.Key);
                }
            }
            foreach (var key in keysToRemove)
            {
                attrs.RemoveAttribute(key);
            }
        }

        private void OnTimerTick(float dt)
        {
            if (sapi == null) return;
            
            try
            {
                ProcessAllPlayers();
            }
            catch (Exception ex)
            {
                sapi.Logger.Error($"[vsquest] ItemChargeDrainSystem error: {ex.Message}");
            }
            
            // Re-register for next interval
            timerId = sapi.Event.RegisterCallback(OnTimerTick, CheckIntervalMs);
        }

        private void ProcessAllPlayers()
        {
            var players = sapi.World.AllPlayers;
            if (players == null || players.Length == 0) return;
            
            double currentTotalHours = sapi.World.Calendar.TotalHours;
            
            foreach (var player in players)
            {
                if (player?.Entity == null) continue;
                if (player.Entity.Player?.InventoryManager == null) continue;
                
                ProcessPlayer(player.Entity, currentTotalHours);
            }
        }

        private void ProcessPlayer(EntityPlayer player, double currentTotalHours)
        {
            var inv = player.Player.InventoryManager.GetOwnInventory("character");
            if (inv == null) return;
            
            // Check all equipment slots
            foreach (ItemSlot slot in inv)
            {
                if (slot?.Empty != false || slot.Itemstack?.Attributes == null) continue;
                
                ProcessItem(slot, player, currentTotalHours);
            }
            
            // Check all 4 backpack slots (equipped bags/quivers)
            var backpackInv = player.Player.InventoryManager.GetOwnInventory("backpack");
            if (backpackInv != null)
            {
                for (int i = 0; i < backpackInv.Count && i < 4; i++)
                {
                    var bagSlot = backpackInv[i];
                    if (!bagSlot.Empty && bagSlot.Itemstack?.Attributes != null)
                    {
                        ProcessItem(bagSlot, player, currentTotalHours);
                    }
                }
            }
        }

        private void ProcessItem(ItemSlot slot, EntityPlayer player, double currentTotalHours)
        {
            var stack = slot.Itemstack;
            
            // Ensure action item attributes are applied
            WearableStatsCache.EnsureItemAttributes(stack);
            
            var attrs = stack.Attributes as TreeAttribute;
            if (attrs == null) return;
            
            // Find all attributes ending with "chargehours"
            var chargeKeys = new List<string>();
            foreach (var val in attrs)
            {
                if (val.Key.EndsWith("chargehours"))
                {
                    chargeKeys.Add(val.Key);
                }
            }
            
            bool anyChanged = false;
            
            foreach (var key in chargeKeys)
            {
                float currentCharge = attrs.GetFloat(key, 0f);
                if (currentCharge <= 0f) continue;
                
                // Get last check time for this specific attribute
                string lastCheckKey = LastCheckKeyPrefix + key;
                double lastCheckHours = player.WatchedAttributes.GetDouble(lastCheckKey, currentTotalHours);
                
                // Calculate elapsed game hours
                double elapsedHours = currentTotalHours - lastCheckHours;
                
                // Store current time for next check
                player.WatchedAttributes.SetDouble(lastCheckKey, currentTotalHours);
                
                if (elapsedHours <= 0) continue;
                
                // Drain charge
                float newCharge = Math.Max(0f, currentCharge - (float)elapsedHours);
                attrs.SetFloat(key, newCharge);
                anyChanged = true;
            }
            
            // Only mark dirty once per slot, and only if something changed
            if (anyChanged)
            {
                slot.MarkDirty();
            }
        }
    }
}
