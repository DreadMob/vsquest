using System;
using System.Collections.Generic;
using Vintagestory.API.Common;

namespace VsQuest
{
    public class TimeOfDayObjective : ActiveActionObjective
    {
        public bool isCompletable(IPlayer byPlayer, params string[] args)
        {
            return IsInTimeWindow(byPlayer, args);
        }

        public List<int> progress(IPlayer byPlayer, params string[] args)
        {
            return IsInTimeWindow(byPlayer, args)
                ? new List<int>(new int[] { 1, 1 })
                : new List<int>(new int[] { 0, 1 });
        }

        private static bool IsInTimeWindow(IPlayer byPlayer, string[] args)
        {
            if (byPlayer?.Entity?.World?.Calendar == null) return false;

            string mode = (args != null && args.Length >= 1) ? args[0] : null;
            mode = string.IsNullOrWhiteSpace(mode) ? "day" : mode.Trim().ToLowerInvariant();

            double hour = byPlayer.Entity.World.Calendar.HourOfDay;

            // Default windows
            if (mode == "night")
            {
                // 18:00 - 06:00
                return hour >= 18 || hour < 6;
            }

            if (mode == "day")
            {
                // 06:00 - 18:00
                return hour >= 6 && hour < 18;
            }

            // Custom hours: mode=startHour,endHour (e.g. "8,16")
            if (mode.Contains(","))
            {
                var parts = mode.Split(',');
                if (parts.Length == 2 && double.TryParse(parts[0], out double start) && double.TryParse(parts[1], out double end))
                {
                    start = NormalizeHour(start);
                    end = NormalizeHour(end);

                    if (Math.Abs(start - end) < 0.0001) return true;

                    if (start < end)
                    {
                        return hour >= start && hour < end;
                    }

                    // Wrap around midnight
                    return hour >= start || hour < end;
                }
            }

            // Unknown mode -> allow
            return true;
        }

        private static double NormalizeHour(double hour)
        {
            hour %= 24;
            if (hour < 0) hour += 24;
            return hour;
        }
    }
}
