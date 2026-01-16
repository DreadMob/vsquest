
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using System.Text.Json.Nodes;

namespace VsQuest.Harmony
{
    [HarmonyPatch(typeof(EntityBehaviorConversable), "Controller_DialogTriggers")]
    public class EntityBehaviorConversable_Controller_DialogTriggers_Patch
    {
        public static void Postfix(EntityBehaviorConversable __instance, EntityAgent triggeringEntity, string value, JsonObject data)
        {
            var sapi = __instance.entity.Api as ICoreServerAPI;
            if (sapi == null) return;

            var player = (triggeringEntity as EntityPlayer)?.Player as IServerPlayer;
            if (player == null) return;

            var message = new QuestAcceptedMessage { questGiverId = __instance.entity.EntityId, questId = "dialog-action" };
            ActionStringExecutor.Execute(sapi, message, player, value);
        }
    }
}
