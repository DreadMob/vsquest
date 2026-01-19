using System;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class NotifyQuestAction : IQuestAction
    {
        public void Execute(ICoreServerAPI api, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (args.Length < 1) throw new QuestException("The 'notify' action requires 1 argument: message.");
            var notification = new ShowNotificationMessage() { Notification = args[0] };

            if (string.Equals(args[0], "albase:bosshunt-rotation-info", StringComparison.OrdinalIgnoreCase))
            {
                var bossSystem = api?.ModLoader?.GetModSystem<BossHuntSystem>();
                if (bossSystem != null && bossSystem.TryGetBossHuntStatus(out _, out _, out double hoursUntilRotation))
                {
                    if (hoursUntilRotation > 0)
                    {
                        int daysLeft = (int)Math.Ceiling(hoursUntilRotation / 24.0);
                        if (daysLeft < 0) daysLeft = 0;
                        notification.Need = daysLeft;
                    }
                }
            }

            api.Network.GetChannel("alegacyvsquest").SendPacket(notification, byPlayer);
        }
    }
}
