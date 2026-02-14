using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class QuestProfilerCommandHandler
    {
        private readonly ICoreServerAPI sapi;

        public QuestProfilerCommandHandler(ICoreServerAPI sapi)
        {
            this.sapi = sapi;
        }

        public TextCommandResult Enable(TextCommandCallingArgs args)
        {
            QuestProfiler.Enabled = true;
            QuestProfiler.Initialize(sapi);
            sapi.Logger.Notification("[QuestProfiler] Profiling enabled");
            return TextCommandResult.Success("Quest profiler enabled. Check server logs for [QuestProfiler] entries.");
        }

        public TextCommandResult Disable(TextCommandCallingArgs args)
        {
            QuestProfiler.Enabled = false;
            sapi.Logger.Notification("[QuestProfiler] Profiling disabled");
            return TextCommandResult.Success("Quest profiler disabled.");
        }

        public TextCommandResult Status(TextCommandCallingArgs args)
        {
            bool enabled = QuestProfiler.Enabled;
            return TextCommandResult.Success($"Quest profiler is {(enabled ? "enabled" : "disabled")}.");
        }

        public TextCommandResult Clear(TextCommandCallingArgs args)
        {
            QuestProfiler.Clear();
            return TextCommandResult.Success("Profiler statistics cleared.");
        }
    }
}
