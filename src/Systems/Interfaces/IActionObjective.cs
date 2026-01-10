using System.Collections.Generic;
using Vintagestory.API.Common;

namespace VsQuest
{
    public interface IActionObjective
    {
        bool isCompletable(IPlayer byPlayer, params string[] args);
        List<int> progress(IPlayer byPlayer, params string[] args);
    }
}
