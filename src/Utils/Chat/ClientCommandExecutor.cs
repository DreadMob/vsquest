using Vintagestory.API.Client;

namespace VsQuest
{
    public static class ClientCommandExecutor
    {
        public static void Execute(ExecutePlayerCommandMessage message, ICoreClientAPI capi)
        {
            string command = message.Command;

            if (command.StartsWith("."))
            {
                capi.TriggerChatMessage(command);
            }
            else
            {
                capi.SendChatMessage(command);
            }
        }
    }
}
