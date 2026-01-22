using ProtoBuf;

namespace VsQuest
{
    [ProtoContract]
    public class QuestQuestionAnswerMessage
    {
        [ProtoMember(1)]
        public string Token { get; set; }

        [ProtoMember(2)]
        public int SelectedIndex { get; set; }
    }
}
