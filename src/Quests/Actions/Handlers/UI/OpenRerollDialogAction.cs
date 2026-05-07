using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Quest action that opens the reroll dialog for a player.
    /// Usage: "openrerolldialog" with no arguments.
    /// </summary>
    public class OpenRerollDialogAction : IQuestAction
    {
        public void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            var itemSystem = sapi.ModLoader.GetModSystem<ItemSystem>();
            var rerollService = itemSystem?.RerollService;
            if (rerollService == null) return;

            var availableRerolls = rerollService.GetAvailableRerolls(byPlayer);
            var groupStrings = new List<string>();

            foreach (var (group, itemCount) in availableRerolls)
            {
                groupStrings.Add($"{group.id}|{group.name}|{itemCount}|{group.itemsRequired}");
            }

            sapi.Network.GetChannel("alegacyvsquest").SendPacket(new ShowRerollDialogMessage
            {
                AvailableGroups = groupStrings.ToArray()
            }, byPlayer);
        }
    }
}
