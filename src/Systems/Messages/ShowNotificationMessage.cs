using ProtoBuf;

namespace VsQuest
{
    [ProtoContract]
    public class ShowNotificationMessage
    {
        [ProtoMember(1)]
        public string Notification { get; set; }
    }
}
