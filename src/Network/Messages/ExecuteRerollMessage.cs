using ProtoBuf;

namespace VsQuest
{
    /// <summary>
    /// Message from client to server requesting a reroll for a specific group.
    /// </summary>
    [ProtoContract]
    public class ExecuteRerollMessage
    {
        /// <summary>
        /// The reroll group ID to reroll
        /// </summary>
        [ProtoMember(1)]
        public string GroupId { get; set; }
    }
}
