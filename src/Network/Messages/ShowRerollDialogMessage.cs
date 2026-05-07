using ProtoBuf;

namespace VsQuest
{
    /// <summary>
    /// Message to show the reroll dialog to a player.
    /// Contains available reroll groups and item counts.
    /// </summary>
    [ProtoContract]
    public class ShowRerollDialogMessage
    {
        /// <summary>
        /// List of reroll groups available to the player.
        /// Format: "groupId|groupName|itemCount|itemsRequired"
        /// </summary>
        [ProtoMember(1)]
        public string[] AvailableGroups { get; set; }
    }
}
