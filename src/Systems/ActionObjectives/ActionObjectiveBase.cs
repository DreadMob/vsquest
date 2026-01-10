using System.Collections.Generic;
using Vintagestory.API.Common;

namespace VsQuest
{
    public abstract class ActionObjectiveBase : ActiveActionObjective, IActionObjectiveV2
    {
        public abstract bool IsCompletable(IPlayer byPlayer, params string[] args);
        public abstract List<int> GetProgress(IPlayer byPlayer, params string[] args);

        public bool isCompletable(IPlayer byPlayer, params string[] args)
        {
            return IsCompletable(byPlayer, args);
        }

        public List<int> progress(IPlayer byPlayer, params string[] args)
        {
            return GetProgress(byPlayer, args);
        }
    }
}
