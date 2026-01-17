using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class QuestFxCommandHandler
    {
        private readonly ICoreServerAPI sapi;

        public QuestFxCommandHandler(ICoreServerAPI sapi)
        {
            this.sapi = sapi;
        }

        public TextCommandResult List(TextCommandCallingArgs args)
        {
            return TextCommandResult.Error("Particle FX preset system is disabled.");
        }

        public TextCommandResult Select(TextCommandCallingArgs args)
        {
            var caller = args.Caller?.Player as IServerPlayer;
            if (caller == null) return TextCommandResult.Error("This command can only be run by a player.");
            return TextCommandResult.Error("Particle FX preset system is disabled.");
        }

        public TextCommandResult Spawn(TextCommandCallingArgs args)
        {
            var caller = args.Caller?.Player as IServerPlayer;
            if (caller == null) return TextCommandResult.Error("This command can only be run by a player.");
            return TextCommandResult.Error("Particle FX preset system is disabled.");
        }

        public TextCommandResult SpawnSelected(TextCommandCallingArgs args)
        {
            var caller = args.Caller?.Player as IServerPlayer;
            if (caller == null) return TextCommandResult.Error("This command can only be run by a player.");
            return TextCommandResult.Error("Particle FX preset system is disabled.");
        }
    }
}
