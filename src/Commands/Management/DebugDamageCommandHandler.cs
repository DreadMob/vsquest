using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class DebugDamageCommandHandler
    {
        public static readonly HashSet<string> EnabledPlayers = new HashSet<string>();

        private readonly ICoreServerAPI sapi;

        public DebugDamageCommandHandler(ICoreServerAPI sapi)
        {
            this.sapi = sapi;
        }

        public TextCommandResult Enable(TextCommandCallingArgs args)
        {
            var player = args.Caller?.Player as IServerPlayer;
            if (player == null) return TextCommandResult.Error("Only players can use this command.");

            EnabledPlayers.Add(player.PlayerUID);
            return TextCommandResult.Success("Debug damage enabled. You will see damage you deal to entities.");
        }

        public TextCommandResult Disable(TextCommandCallingArgs args)
        {
            var player = args.Caller?.Player as IServerPlayer;
            if (player == null) return TextCommandResult.Error("Only players can use this command.");

            EnabledPlayers.Remove(player.PlayerUID);
            return TextCommandResult.Success("Debug damage disabled.");
        }

        public TextCommandResult Status(TextCommandCallingArgs args)
        {
            var player = args.Caller?.Player as IServerPlayer;
            if (player == null) return TextCommandResult.Error("Only players can use this command.");

            bool enabled = EnabledPlayers.Contains(player.PlayerUID);
            return TextCommandResult.Success(enabled ? "Debug damage is ENABLED." : "Debug damage is DISABLED.");
        }

        public static void SendDamageMessage(ICoreServerAPI sapi, IServerPlayer attacker, Entity target, float damage, DamageSource damageSource)
        {
            if (sapi == null || attacker == null || target == null) return;
            if (!EnabledPlayers.Contains(attacker.PlayerUID)) return;

            string targetName = target.GetName() ?? target.Code?.Path ?? "unknown";
            string damageType = damageSource?.Type.ToString() ?? "unknown";
            string source = damageSource?.Source.ToString() ?? "unknown";

            string msg = $"[Debug] Damage: {damage:0.#} | Target: {targetName} | Type: {damageType} | Source: {source}";
            sapi.SendMessage(attacker, GlobalConstants.GeneralChatGroup, msg, EnumChatType.Notification);
        }
    }
}
