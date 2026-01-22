using ProtoBuf;

namespace VsQuest
{
    [ProtoContract]
    public class ShowQuestQuestionMessage
    {
        [ProtoMember(1)]
        public string Token { get; set; }

        [ProtoMember(2)]
        public string TitleLangKey { get; set; }

        [ProtoMember(3)]
        public string TextLangKey { get; set; }

        [ProtoMember(4)]
        public string[] OptionLangKeys { get; set; }
    }
}
