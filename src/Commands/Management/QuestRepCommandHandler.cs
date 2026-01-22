using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class QuestRepCommandHandler
    {
        private readonly ICoreServerAPI sapi;

        public QuestRepCommandHandler(ICoreServerAPI sapi)
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
                ?.FirstOrDefault(p => p.PlayerName.Equals(playerName, StringComparison.OrdinalIgnoreCase)) as IServerPlayer;

            if (target == null)
            {
                error = TextCommandResult.Error($"Player '{playerName}' not found online.");
                return false;
            }

            return true;
        }

        public TextCommandResult Set(TextCommandCallingArgs args)
        {
            string scopeRaw = (string)args[0];
            string id = (string)args[1];
            int value = (int)args[2];
            string playerName = (string)args[3];

            if (!TryResolveTarget(playerName, args, out var target, out var err)) return err;

            var repSystem = sapi.ModLoader.GetModSystem<ReputationSystem>();
            if (repSystem == null) return TextCommandResult.Error("Reputation system not available.");

            if (!repSystem.TryParseScope(scopeRaw, out var scope))
            {
                return TextCommandResult.Error("Scope must be 'npc' or 'faction'.");
            }

            if (string.IsNullOrWhiteSpace(id)) return TextCommandResult.Error("Id must not be empty.");

            repSystem.ApplyReputationChange(sapi, target, scope, id, value, false);

            return TextCommandResult.Success($"Set reputation ({scope.ToString().ToLowerInvariant()}) '{id}' = {value} for '{target.PlayerName}'.");
        }

        public TextCommandResult Add(TextCommandCallingArgs args)
        {
            string scopeRaw = (string)args[0];
            string id = (string)args[1];
            int delta = (int)args[2];
            string playerName = (string)args[3];

            if (!TryResolveTarget(playerName, args, out var target, out var err)) return err;

            var repSystem = sapi.ModLoader.GetModSystem<ReputationSystem>();
            if (repSystem == null) return TextCommandResult.Error("Reputation system not available.");

            if (!repSystem.TryParseScope(scopeRaw, out var scope))
            {
                return TextCommandResult.Error("Scope must be 'npc' or 'faction'.");
            }

            if (string.IsNullOrWhiteSpace(id)) return TextCommandResult.Error("Id must not be empty.");

            int current = repSystem.GetReputationValue(target as IPlayer, scope, id);
            int next = current + delta;

            repSystem.ApplyReputationChange(sapi, target, scope, id, next, false);

            return TextCommandResult.Success($"Added {delta} reputation to ({scope.ToString().ToLowerInvariant()}) '{id}' (now {next}) for '{target.PlayerName}'.");
        }

        public TextCommandResult Get(TextCommandCallingArgs args)
        {
            string scopeRaw = (string)args[0];
            string id = (string)args[1];
            string playerName = (string)args[2];

            if (!TryResolveTarget(playerName, args, out var target, out var err)) return err;

            var repSystem = sapi.ModLoader.GetModSystem<ReputationSystem>();
            if (repSystem == null) return TextCommandResult.Error("Reputation system not available.");

            if (!repSystem.TryParseScope(scopeRaw, out var scope))
            {
                return TextCommandResult.Error("Scope must be 'npc' or 'faction'.");
            }

            if (string.IsNullOrWhiteSpace(id)) return TextCommandResult.Error("Id must not be empty.");

            int value = repSystem.GetReputationValue(target as IPlayer, scope, id);
            var definition = scope == ReputationScope.Npc ? repSystem.GetNpcDefinition(id) : repSystem.GetFactionDefinition(id);
            string rankKey = repSystem.GetRankLangKey(definition, value);
            string rankText = string.IsNullOrWhiteSpace(rankKey) ? "-" : Lang.Get(rankKey);

            return TextCommandResult.Success($"Reputation ({scope.ToString().ToLowerInvariant()}) '{id}': {value} (Rank: {rankText}) for '{target.PlayerName}'.");
        }
    }
}
