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

        private const string IntoxUntilMsKey = "alegacyvsquest:bossintoxaura:until";
        private const string RepulseStunUntilKey = "alegacyvsquest:bossrepulsestun:until";
        private const string RepulseStunMultKey = "alegacyvsquest:bossrepulsestun:mult";
        private const string BossGrabNoSneakUntilKey = "alegacyvsquest:bossgrab:nosneakuntil";

        private const string AshFloorNoJumpUntilKey = "alegacyvsquest:ashfloor:nojumpuntil";
        private const string AshFloorNoShiftUntilKey = "alegacyvsquest:ashfloor:noshiftuntil";
        private const string AshFloorUntilKey = "alegacyvsquest:ashfloor:until";
        private const string AshFloorWalkSpeedMultKey = "alegacyvsquest:ashfloor:walkspeedmult";

        private const string SecondChanceDebuffUntilKey = "alegacyvsquest:secondchance:debuffuntil";

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

        public TextCommandResult SetFloat(TextCommandCallingArgs args)
        {
            string playerName = (string)args[0];
            string key = (string)args[1];
            string valueRaw = (string)args[2];

            if (!TryResolveTarget(playerName, args, out var target, out var err)) return err;
            if (string.IsNullOrWhiteSpace(key)) return TextCommandResult.Error("Key must not be empty.");

            if (!float.TryParse(valueRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            {
                return TextCommandResult.Error("Value must be a number (use '.' for decimals).");
            }

            var wa = target.Entity?.WatchedAttributes;
            if (wa == null) return TextCommandResult.Error("Player entity not available.");

            wa.SetFloat(key, value);
            wa.MarkPathDirty(key);
            wa.MarkAllDirty();

            return TextCommandResult.Success($"Set wattr float '{key}' = {value.ToString(CultureInfo.InvariantCulture)} for '{target.PlayerName}'.");
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

        public TextCommandResult AddFloat(TextCommandCallingArgs args)
        {
            string playerName = (string)args[0];
            string key = (string)args[1];
            string deltaRaw = (string)args[2];

            if (!TryResolveTarget(playerName, args, out var target, out var err)) return err;
            if (string.IsNullOrWhiteSpace(key)) return TextCommandResult.Error("Key must not be empty.");

            if (!float.TryParse(deltaRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out float delta))
            {
                return TextCommandResult.Error("Delta must be a number (use '.' for decimals).");
            }

            var wa = target.Entity?.WatchedAttributes;
            if (wa == null) return TextCommandResult.Error("Player entity not available.");

            float cur = wa.GetFloat(key, 0f);
            float next = cur + delta;
            wa.SetFloat(key, next);
            wa.MarkPathDirty(key);
            wa.MarkAllDirty();

            return TextCommandResult.Success($"Added {delta.ToString(CultureInfo.InvariantCulture)} to wattr float '{key}' (now {next.ToString(CultureInfo.InvariantCulture)}) for '{target.PlayerName}'.");
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

        public TextCommandResult FixPlayer(TextCommandCallingArgs args)
        {
            string playerName = (string)args[0];

            if (!TryResolveTarget(playerName, args, out var target, out var err)) return err;

            var ent = target.Entity;
            if (ent == null) return TextCommandResult.Error("Player entity not available.");

            var wa = ent.WatchedAttributes;
            if (wa == null) return TextCommandResult.Error("Player watched attributes not available.");

            try
            {
                // Visual / post-effects
                wa.RemoveAttribute("intoxication");
                wa.RemoveAttribute(IntoxUntilMsKey);

                // Boss repulse stun
                wa.RemoveAttribute(RepulseStunUntilKey);
                wa.RemoveAttribute(RepulseStunMultKey);

                // Boss grab
                wa.RemoveAttribute(BossGrabNoSneakUntilKey);

                // Ash floor
                wa.RemoveAttribute(AshFloorNoJumpUntilKey);
                wa.RemoveAttribute(AshFloorNoShiftUntilKey);
                wa.RemoveAttribute(AshFloorUntilKey);
                wa.RemoveAttribute(AshFloorWalkSpeedMultKey);

                // Second chance
                wa.RemoveAttribute(SecondChanceDebuffUntilKey);
            }
            catch
            {
            }

            try
            {
                wa.MarkAllDirty();
            }
            catch
            {
            }

            try
            {
                if (ent.Stats != null)
                {
                    // Clear known walkspeed sources.
                    ent.Stats.Set("walkspeed", "alegacyvsquest:bossrepulsestun:stat", 0f, true);
                    ent.Stats.Remove("walkspeed", "alegacyvsquest:bossgrab");
                    ent.Stats.Remove("walkspeed", "alegacyvsquest");
                    ent.Stats.Set("walkspeed", "alegacyvsquest:ashfloor", 0f, true);
                    ent.Stats.Set("walkspeed", "alegacyvsquest:secondchance:debuff", 0f, true);

                    // Clear other second chance stats.
                    ent.Stats.Set("hungerrate", "alegacyvsquest:secondchance:debuff", 0f, true);
                    ent.Stats.Set("healingeffectivness", "alegacyvsquest:secondchance:debuff", 0f, true);

                    ent.walkSpeed = ent.Stats.GetBlended("walkspeed");
                }
            }
            catch
            {
            }

            return TextCommandResult.Success($"Cleared boss debuffs for '{target.PlayerName}'.");
        }
    }
}
