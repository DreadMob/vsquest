
using System;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public static class ResetWalkDistanceAction
    {
        public static void Execute(ICoreServerAPI api, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (byPlayer?.Entity?.WatchedAttributes == null) return;

            string questId = null;
            if (args != null && args.Length >= 1 && !string.IsNullOrWhiteSpace(args[0])) questId = args[0];
            if (string.IsNullOrWhiteSpace(questId)) questId = message?.questId;
            if (string.IsNullOrWhiteSpace(questId)) return;

            int slots = 1;
            if (args != null && args.Length >= 2 && int.TryParse(args[1], out int parsedSlots)) slots = parsedSlots;
            if (slots < 1) slots = 1;
            if (slots > 32) slots = 32;

            var wa = byPlayer.Entity.WatchedAttributes;

            for (int slot = 0; slot < slots; slot++)
            {
                string haveKey = WalkDistanceObjective.HaveKey(questId, slot);
                string lastXKey = $"vsquest:walkdist:{questId}:slot{slot}:lastx";
                string lastZKey = $"vsquest:walkdist:{questId}:slot{slot}:lastz";
                string hasLastKey = $"vsquest:walkdist:{questId}:slot{slot}:haslast";

                wa.RemoveAttribute(haveKey);
                wa.RemoveAttribute(lastXKey);
                wa.RemoveAttribute(lastZKey);
                wa.RemoveAttribute(hasLastKey);

                wa.MarkPathDirty(haveKey);
                wa.MarkPathDirty(lastXKey);
                wa.MarkPathDirty(lastZKey);
                wa.MarkPathDirty(hasLastKey);
            }
        }
    }
}
