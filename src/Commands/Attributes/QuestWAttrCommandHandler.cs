using System;
using System.Globalization;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class QuestWAttrCommandHandler
    {
        private readonly ICoreServerAPI sapi;

        public QuestWAttrCommandHandler(ICoreServerAPI sapi)
        {
            this.sapi = sapi;
        }

        private bool TryResolveTarget(string playerName, TextCommandCallingArgs args, out IServerPlayer target, out TextCommandResult error)
        {
            error = null;
            target = null;

            if (string.IsNullOrWhiteSpace(playerName))
            {
                target = args.Caller?.Player as IServerPlayer;
                if (target == null)
                {
                    error = TextCommandResult.Error("No player specified and command caller is not a player.");
                    return false;
                }
                return true;
            }

            target = sapi.World.AllOnlinePlayers
                .FirstOrDefault(p => p.PlayerName.Equals(playerName, StringComparison.OrdinalIgnoreCase)) as IServerPlayer;

            if (target == null)
            {
                error = TextCommandResult.Error($"Player '{playerName}' not found online.");
                return false;
            }

            return true;
        }

        public TextCommandResult SetInt(TextCommandCallingArgs args)
        {
            string playerName = (string)args[0];
            string key = (string)args[1];
            int value = (int)args[2];

            if (!TryResolveTarget(playerName, args, out var target, out var err)) return err;
            if (string.IsNullOrWhiteSpace(key)) return TextCommandResult.Error("Key must not be empty.");

            var wa = target.Entity?.WatchedAttributes;
            if (wa == null) return TextCommandResult.Error("Player entity not available.");

            wa.SetInt(key, value);
            wa.MarkPathDirty(key);
            wa.MarkAllDirty();

            return TextCommandResult.Success($"Set wattr int '{key}' = {value} for '{target.PlayerName}'.");
        }

        public TextCommandResult SetBool(TextCommandCallingArgs args)
        {
            string playerName = (string)args[0];
            string key = (string)args[1];
            bool value = (bool)args[2];

            if (!TryResolveTarget(playerName, args, out var target, out var err)) return err;
            if (string.IsNullOrWhiteSpace(key)) return TextCommandResult.Error("Key must not be empty.");

            var wa = target.Entity?.WatchedAttributes;
            if (wa == null) return TextCommandResult.Error("Player entity not available.");

            wa.SetBool(key, value);
            wa.MarkPathDirty(key);
            wa.MarkAllDirty();

            return TextCommandResult.Success($"Set wattr bool '{key}' = {(value ? "true" : "false")} for '{target.PlayerName}'.");
        }

        public TextCommandResult SetString(TextCommandCallingArgs args)
        {
            string playerName = (string)args[0];
            string key = (string)args[1];
            string value = (string)args[2];

            if (!TryResolveTarget(playerName, args, out var target, out var err)) return err;
            if (string.IsNullOrWhiteSpace(key)) return TextCommandResult.Error("Key must not be empty.");

            var wa = target.Entity?.WatchedAttributes;
            if (wa == null) return TextCommandResult.Error("Player entity not available.");

            wa.SetString(key, value ?? "");
            wa.MarkPathDirty(key);
            wa.MarkAllDirty();

            return TextCommandResult.Success($"Set wattr string '{key}' for '{target.PlayerName}'.");
        }

        public TextCommandResult AddInt(TextCommandCallingArgs args)
        {
            string playerName = (string)args[0];
            string key = (string)args[1];
            int delta = (int)args[2];

            if (!TryResolveTarget(playerName, args, out var target, out var err)) return err;
            if (string.IsNullOrWhiteSpace(key)) return TextCommandResult.Error("Key must not be empty.");

            var wa = target.Entity?.WatchedAttributes;
            if (wa == null) return TextCommandResult.Error("Player entity not available.");

            int cur = wa.GetInt(key, 0);
            int next = cur + delta;
            wa.SetInt(key, next);
            wa.MarkPathDirty(key);
            wa.MarkAllDirty();

            return TextCommandResult.Success($"Added {delta} to wattr int '{key}' (now {next}) for '{target.PlayerName}'.");
        }

        public TextCommandResult Remove(TextCommandCallingArgs args)
        {
            string playerName = (string)args[0];
            string key = (string)args[1];

            if (!TryResolveTarget(playerName, args, out var target, out var err)) return err;
            if (string.IsNullOrWhiteSpace(key)) return TextCommandResult.Error("Key must not be empty.");

            var wa = target.Entity?.WatchedAttributes;
            if (wa == null) return TextCommandResult.Error("Player entity not available.");

            wa.RemoveAttribute(key);
            wa.MarkPathDirty(key);
            wa.MarkAllDirty();

            return TextCommandResult.Success($"Removed wattr '{key}' for '{target.PlayerName}'.");
        }
    }
}
