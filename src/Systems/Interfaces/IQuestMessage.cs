namespace VsQuest
{
    public interface IQuestMessage
    {
        string questId { get; set; }
        long questGiverId { get; set; }
    }
}
