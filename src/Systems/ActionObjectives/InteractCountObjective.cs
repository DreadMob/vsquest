using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;

namespace VsQuest
{
    public class InteractCountObjective : ActionObjectiveBase
    {
        public override bool IsCompletable(IPlayer byPlayer, params string[] args)
        {
            if (args == null || args.Length == 0) return false;

            var coordArgs = GetCoordArgs(args);
            if (coordArgs.Length == 0) return false;

            return CountCompleted(byPlayer, coordArgs) >= coordArgs.Length;
        }

        public override List<int> GetProgress(IPlayer byPlayer, params string[] args)
        {
            var coordArgs = GetCoordArgs(args);
            int total = coordArgs.Length;
            if (total == 0) return new List<int>(new int[] { 0, 0 });

            int count = CountCompleted(byPlayer, coordArgs);
            if (count > total) count = total;

            return new List<int>(new int[] { count, total });
        }

        private static string[] GetCoordArgs(string[] args)
        {
            if (args == null) return Array.Empty<string>();
            if (args.Length >= 2) return args.Take(args.Length - 1).ToArray();
            return Array.Empty<string>();
        }

        private static int CountCompleted(IPlayer byPlayer, string[] coordArgs)
        {
            string completedInteractions = byPlayer.Entity.WatchedAttributes.GetString("completedInteractions", "");
            if (string.IsNullOrEmpty(completedInteractions)) return 0;

            var completed = new HashSet<string>(
                completedInteractions.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            );

            int count = 0;
            foreach (var coordString in coordArgs)
            {
                if (string.IsNullOrWhiteSpace(coordString)) continue;
                var coords = coordString.Split(',');
                if (coords.Length != 3) continue;

                if (int.TryParse(coords[0], out int x) && int.TryParse(coords[1], out int y) && int.TryParse(coords[2], out int z))
                {
                    string key = $"interactat_{x}_{y}_{z}";
                    if (completed.Contains(key)) count++;
                }
            }

            return count;
        }
    }
}
