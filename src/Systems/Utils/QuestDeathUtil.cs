using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace VsQuest
{
    public static class QuestDeathUtil
    {
        public static void HandleEntityDeath(ICoreServerAPI sapi, List<ActiveQuest> quests, EntityPlayer player, Entity killedEntity)
        {
            if (sapi == null || player == null || quests == null) return;

            string killedCode = killedEntity?.Code?.Path;
            var serverPlayer = player.Player as IServerPlayer;

            foreach (var quest in quests)
            {
                quest.OnEntityKilled(killedCode, player.Player);

                if (serverPlayer != null)
                {
                    RandomKillQuestUtils.TryHandleKill(sapi, serverPlayer, quest, killedCode);
                }
            }
        }
    }
}
