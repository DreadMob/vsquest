namespace VsQuest.Gui.Journal
{
    public interface IJournalPage
    {
        bool Visible { get; }
        string PageCode { get; }
        string CategoryCode { get; }
        void RenderListEntryTo(Vintagestory.API.Client.ICoreClientAPI capi, float dt, double x, double y, double cellWidth, double cellHeight);
        void Dispose();
        float GetTextMatchWeight(string searchText);
    }
}
