using Vintagestory.API.Server;

namespace VsQuest
{
    public static class CommandActions
    {
        public static void ServerCommand(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (args.Length > 0)
            {
                string command = string.Join(" ", args);
                if (!command.StartsWith("/"))
                {
                    command = "/" + command;
                }
                sapi.InjectConsole(command);
            }
        }

        public static void PlayerCommand(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (args.Length > 0)
            {
                string command = string.Join(" ", args);
                sapi.Network.GetChannel("vsquest").SendPacket(new ExecutePlayerCommandMessage() { Command = command }, byPlayer);
            }
        }
    }
}
