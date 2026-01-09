using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class QuestEntityCommandHandler
    {
        private readonly ICoreServerAPI sapi;
        private readonly QuestSystem questSystem;

        public QuestEntityCommandHandler(ICoreServerAPI sapi, QuestSystem questSystem)
        {
            this.sapi = sapi;
            this.questSystem = questSystem;
        }

        public TextCommandResult Handle(TextCommandCallingArgs args)
        {
            try
            {
                string domain = null;
                try
                {
                    domain = (string)args[0];
                }
                catch
                {
                }

                HashSet<string> allowedDomains;
                if (!string.IsNullOrWhiteSpace(domain))
                {
                    allowedDomains = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { domain };
                }
                else
                {
                    allowedDomains = GetQuestDomains();
                    if (allowedDomains.Count == 0)
                    {
                        return TextCommandResult.Error("No quest domains found. Usage: quest entity <domain>");
                    }
                }

                var world = sapi.World;
                if (world == null) return TextCommandResult.Error("World is not available.");

                var entityTypesProp = world.GetType().GetProperty("EntityTypes");
                if (entityTypesProp == null) return TextCommandResult.Error("World.EntityTypes is not available in this API version.");

                object entityTypesObj;
                try
                {
                    entityTypesObj = entityTypesProp.GetValue(world);
                }
                catch (Exception e)
                {
                    sapi.Logger.Error(e.ToString());
                    return TextCommandResult.Error("Failed to read World.EntityTypes: " + e.Message);
                }

                if (entityTypesObj == null) return TextCommandResult.Error("World.EntityTypes is null.");

                IEnumerable entityTypesEnumerable;
                if (entityTypesObj is IDictionary entityTypesDict)
                {
                    entityTypesEnumerable = entityTypesDict.Values;
                }
                else if (entityTypesObj is IEnumerable entityTypesAsEnumerable)
                {
                    entityTypesEnumerable = entityTypesAsEnumerable;
                }
                else
                {
                    return TextCommandResult.Error("World.EntityTypes is not enumerable.");
                }

                var lines = new List<string>();
                var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var entry in entityTypesEnumerable)
                {
                    try
                    {
                        var entityType = TryExtractEntityProperties(entry);
                        if (entityType == null) continue;

                        var epCode = entityType.Code;
                        if (epCode == null) continue;
                        if (!allowedDomains.Contains(epCode.Domain)) continue;

                        var code = epCode.ToShortString();
                        if (string.IsNullOrWhiteSpace(code)) continue;
                        if (seenCodes.Contains(code)) continue;

                        seenCodes.Add(code);

                        string name;
                        try
                        {
                            name = GetEntityTypeDisplayName(entityType) ?? epCode.Path ?? code;
                        }
                        catch
                        {
                            name = code;
                        }

                        lines.Add($"{code} - {name}");
                    }
                    catch (Exception e)
                    {
                        sapi.Logger.Error(e.ToString());
                    }
                }

                if (lines.Count == 0)
                {
                    if (!string.IsNullOrWhiteSpace(domain)) return TextCommandResult.Success($"No entity types found for domain '{domain}'.");
                    return TextCommandResult.Success("No entity types found for quest domains.");
                }

                lines.Sort(StringComparer.OrdinalIgnoreCase);
                return TextCommandResult.Success(string.Join("\n", lines));
            }
            catch (Exception e)
            {
                sapi.Logger.Error(e.ToString());
                return TextCommandResult.Error("Quest entity failed: " + e.Message);
            }
        }

        private HashSet<string> GetQuestDomains()
        {
            var domains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var registry = questSystem?.QuestRegistry;
            if (registry == null) return domains;

            foreach (var quest in registry.Values)
            {
                var id = quest?.id;
                if (string.IsNullOrWhiteSpace(id)) continue;

                int idx = id.IndexOf(':');
                if (idx <= 0) continue;

                domains.Add(id.Substring(0, idx));
            }

            return domains;
        }

        private static EntityProperties TryExtractEntityProperties(object entry)
        {
            try
            {
                if (entry == null) return null;

                if (entry is EntityProperties ep) return ep;

                if (entry is DictionaryEntry de)
                {
                    return de.Value as EntityProperties;
                }

                var valueProp = entry.GetType().GetProperty("Value");
                if (valueProp != null)
                {
                    return valueProp.GetValue(entry) as EntityProperties;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static string GetEntityTypeDisplayName(EntityProperties entityType)
        {
            var code = entityType?.Code;
            if (code == null) return null;

            string key = "entity-" + code.ToShortString();
            string name = Lang.Get(key);
            if (!string.IsNullOrWhiteSpace(name) && name != key) return name;

            key = "entity-" + code.Path;
            name = Lang.Get(key);
            if (!string.IsNullOrWhiteSpace(name) && name != key) return name;

            return null;
        }
    }
}
