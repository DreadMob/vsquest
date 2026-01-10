using System.Collections.Generic;
using Vintagestory.API.Common;

namespace VsQuest
{
    public interface IActionObjectiveV2
    {
        bool IsCompletable(IPlayer byPlayer, params string[] args);
        List<int> GetProgress(IPlayer byPlayer, params string[] args);
    }
}
