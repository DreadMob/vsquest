using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace VsQuest
{
    public class InteractAtCoordinateObjective : ActionObjectiveBase
    {
        public override bool IsCompletable(IPlayer byPlayer, params string[] args)
        {
            if (args.Length < 1) return false;
            
            string coordString = args[0];
            string[] coords = coordString.Split(',');
            if (coords.Length != 3) return false;
            
            int targetX, targetY, targetZ;
            if (!int.TryParse(coords[0], out targetX) || 
                !int.TryParse(coords[1], out targetY) || 
                !int.TryParse(coords[2], out targetZ)) 
            {
                return false;
            }
            

            string interactionKey = $"interactat_{targetX}_{targetY}_{targetZ}";
            string completedInteractions = byPlayer.Entity.WatchedAttributes.GetString("completedInteractions", "");
            string[] completed = completedInteractions.Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);

            return completed.Contains(interactionKey);
        }

        public override List<int> GetProgress(IPlayer byPlayer, params string[] args)
        {
            bool completed = IsCompletable(byPlayer, args);
            return new List<int>(new int[] { completed ? 1 : 0 });
        }
    }
}
