using System.Collections.Generic;

namespace VsQuest
{
    /// <summary>
    /// Configuration for item reroll system loaded from rerollconfig.json
    /// </summary>
    public class RerollConfig
    {
        public List<RerollGroup> rerollGroups { get; set; } = new List<RerollGroup>();
        
        /// <summary>
        /// Mapping from questId to rerollGroupId.
        /// When an item is given from a quest, its rerollGroup is looked up here.
        /// </summary>
        public Dictionary<string, string> questToGroupMapping { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Defines a reroll group - items that can be exchanged for a random item from the same group.
    /// </summary>
    public class RerollGroup
    {
        /// <summary>
        /// Unique identifier for this group (e.g., "ossuarywarden")
        /// </summary>
        public string id { get; set; }

        /// <summary>
        /// Display name for UI (e.g., "Оссуарий")
        /// </summary>
        public string name { get; set; }

        /// <summary>
        /// Number of items required to perform a reroll (default: 2)
        /// </summary>
        public int itemsRequired { get; set; } = 2;

        /// <summary>
        /// List of action item IDs that can be given as reroll result.
        /// </summary>
        public List<string> rewardItems { get; set; } = new List<string>();

        /// <summary>
        /// Whether to apply quality roll on rerolled item (default: false)
        /// </summary>
        public bool applyQuality { get; set; } = false;

        /// <summary>
        /// Optional: restrict reroll to specific NPC entity codes.
        /// If empty, any NPC with reroll capability can be used.
        /// </summary>
        public List<string> npcEntityCodes { get; set; } = new List<string>();
    }
}
