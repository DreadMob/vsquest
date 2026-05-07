using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Service for handling item reroll functionality.
    /// Allows players to exchange multiple items from a reroll group for a random item from the same group.
    /// </summary>
    public class RerollService
    {
        private readonly ICoreServerAPI sapi;
        private readonly Dictionary<string, RerollGroup> groupRegistry = new Dictionary<string, RerollGroup>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> questToGroupMapping = new Dictionary<string, string>(StringComparer.Ordinal);

        public RerollService(ICoreServerAPI api)
        {
            this.sapi = api;
        }

        /// <summary>
        /// Loads reroll configurations from all mods' rerollconfig.json files
        /// </summary>
        public void LoadConfigs()
        {
            if (sapi == null) return;

            groupRegistry.Clear();
            questToGroupMapping.Clear();

            foreach (var mod in sapi.ModLoader.Mods)
            {
                var assets = sapi.Assets.GetMany<RerollConfig>(sapi.Logger, "config/rerollconfig", mod.Info.ModID);
                foreach (var asset in assets)
                {
                    if (asset.Value?.rerollGroups == null) continue;

                    foreach (var group in asset.Value.rerollGroups)
                    {
                        if (string.IsNullOrWhiteSpace(group.id)) continue;
                        groupRegistry[group.id] = group;
                    }

                    // Load quest to group mapping
                    if (asset.Value.questToGroupMapping != null)
                    {
                        foreach (var kvp in asset.Value.questToGroupMapping)
                        {
                            questToGroupMapping[kvp.Key] = kvp.Value;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets the reroll group ID for a quest ID
        /// </summary>
        public string GetRerollGroupIdForQuest(string questId)
        {
            if (string.IsNullOrWhiteSpace(questId)) return null;
            return questToGroupMapping.TryGetValue(questId, out var groupId) ? groupId : null;
        }

        /// <summary>
        /// Gets a reroll group by ID
        /// </summary>
        public RerollGroup GetGroup(string groupId)
        {
            if (string.IsNullOrWhiteSpace(groupId)) return null;
            return groupRegistry.TryGetValue(groupId, out var group) ? group : null;
        }

        /// <summary>
        /// Gets all reroll groups
        /// </summary>
        public IEnumerable<RerollGroup> GetAllGroups()
        {
            return groupRegistry.Values;
        }

        /// <summary>
        /// Counts how many items the player has from each reroll group
        /// </summary>
        public Dictionary<string, int> CountItemsByGroup(IServerPlayer player)
        {
            var result = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var kvp in player.InventoryManager.Inventories)
            {
                var inv = kvp.Value;
                if (inv == null) continue;
                for (int i = 0; i < inv.Count; i++)
                {
                    var slot = inv[i];
                    if (slot?.Itemstack?.Attributes == null) continue;

                    string groupId = slot.Itemstack.Attributes.GetString(ItemAttributeUtils.ActionItemRerollGroupKey);
                    if (string.IsNullOrWhiteSpace(groupId)) continue;

                    if (!result.TryGetValue(groupId, out var count))
                    {
                        count = 0;
                    }
                    result[groupId] = count + slot.Itemstack.StackSize;
                }
            }

            return result;
        }

        /// <summary>
        /// Gets slots containing items from a specific reroll group
        /// </summary>
        public List<ItemSlot> GetSlotsForGroup(IServerPlayer player, string groupId)
        {
            var result = new List<ItemSlot>();

            foreach (var kvp in player.InventoryManager.Inventories)
            {
                var inv = kvp.Value;
                if (inv == null) continue;
                for (int i = 0; i < inv.Count; i++)
                {
                    var slot = inv[i];
                    if (slot?.Itemstack?.Attributes == null) continue;

                    string itemGroupId = slot.Itemstack.Attributes.GetString(ItemAttributeUtils.ActionItemRerollGroupKey);
                    if (itemGroupId == groupId)
                    {
                        result.Add(slot);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Checks if player can perform a reroll for a specific group
        /// </summary>
        public bool CanReroll(IServerPlayer player, string groupId)
        {
            var group = GetGroup(groupId);
            if (group == null) return false;

            var counts = CountItemsByGroup(player);
            if (!counts.TryGetValue(groupId, out var count)) return false;

            return count >= group.itemsRequired;
        }

        /// <summary>
        /// Executes a reroll for a specific group.
        /// Removes itemsRequired items from the group and gives a random reward item.
        /// </summary>
        public bool ExecuteReroll(IServerPlayer player, string groupId)
        {
            var group = GetGroup(groupId);
            if (group == null) return false;
            if (!CanReroll(player, groupId)) return false;
            if (group.rewardItems == null || group.rewardItems.Count == 0) return false;

            // Find and remove items
            int toRemove = group.itemsRequired;
            var slots = GetSlotsForGroup(player, groupId);

            foreach (var slot in slots)
            {
                if (toRemove <= 0) break;

                int stackSize = slot.Itemstack?.StackSize ?? 0;
                int removeCount = Math.Min(toRemove, stackSize);

                if (removeCount >= stackSize)
                {
                    slot.Itemstack = null;
                }
                else
                {
                    slot.Itemstack.StackSize -= removeCount;
                }
                slot.MarkDirty();

                toRemove -= removeCount;
            }

            if (toRemove > 0)
            {
                // Failed to remove enough items
                return false;
            }

            // Give random reward item
            string rewardItemId = group.rewardItems[sapi.World.Rand.Next(group.rewardItems.Count)];

            var itemSystem = sapi.ModLoader.GetModSystem<ItemSystem>();
            if (!itemSystem.ActionItemRegistry.TryGetValue(rewardItemId, out var actionItem))
            {
                sapi.Logger.Warning("[RerollService] Reward item '{0}' not found in registry", rewardItemId);
                return false;
            }

            if (!ItemAttributeUtils.TryResolveCollectible(sapi, actionItem.itemCode, out var collectible))
            {
                sapi.Logger.Warning("[RerollService] Base collectible '{0}' not found for reward item '{1}'", actionItem.itemCode, rewardItemId);
                return false;
            }

            var stack = new ItemStack(collectible);
            ItemAttributeUtils.ApplyActionItemAttributes(stack, actionItem);

            // Set reroll group on the new item
            stack.Attributes.SetString(ItemAttributeUtils.ActionItemRerollGroupKey, groupId);

            // Apply quality if configured
            if (group.applyQuality)
            {
                var qualityService = itemSystem.QualityService;
                qualityService?.TryApplyQuality(stack, actionItem, sapi.World.Rand);
            }

            if (!player.InventoryManager.TryGiveItemstack(stack))
            {
                sapi.World.SpawnItemEntity(stack, player.Entity.Pos.XYZ);
            }

            return true;
        }

        /// <summary>
        /// Gets groups that the player can reroll (has enough items)
        /// </summary>
        public List<(RerollGroup group, int itemCount)> GetAvailableRerolls(IServerPlayer player)
        {
            var result = new List<(RerollGroup, int)>();
            var counts = CountItemsByGroup(player);

            foreach (var kvp in counts)
            {
                var group = GetGroup(kvp.Key);
                if (group != null && kvp.Value >= group.itemsRequired)
                {
                    result.Add((group, kvp.Value));
                }
            }

            return result;
        }
    }
}
