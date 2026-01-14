using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class QuestExecActionStringCommandHandler
    {
        private readonly ICoreServerAPI sapi;

        public QuestExecActionStringCommandHandler(ICoreServerAPI sapi)
        {
            this.sapi = sapi;
        }

        public TextCommandResult Handle(TextCommandCallingArgs args)
        {
            string playerName = (string)args[0];
            string actionString = (string)args[1];

            if (string.IsNullOrWhiteSpace(actionString))
            {
                return TextCommandResult.Error("No action string provided.");
            }

            IServerPlayer target;

            if (string.IsNullOrWhiteSpace(playerName))
            {
                target = args.Caller?.Player as IServerPlayer;
                if (target == null)
                {
                    return TextCommandResult.Error("No player specified and command caller is not a player.");
                }
            }
            else
            {
                target = sapi.World.AllOnlinePlayers
                    .FirstOrDefault(p => p.PlayerName.Equals(playerName, StringComparison.OrdinalIgnoreCase)) as IServerPlayer;

                if (target == null)
                {
                    return TextCommandResult.Error($"Player '{playerName}' not found online.");
                }
            }

            var message = new QuestAcceptedMessage
            {
                questGiverId = 0,
                questId = "vsquest:admin-action"
            };

            try
            {
                ActionStringExecutor.Execute(sapi, message, target, actionString);
            }
            catch (Exception e)
            {
                return TextCommandResult.Error($"Failed to execute action string: {e.Message}");
            }

            return TextCommandResult.Success($"Executed action string for '{target.PlayerName}'.");
        }
    }
}
