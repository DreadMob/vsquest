using System.Collections.Generic;
using Vintagestory.API.Common;

namespace VsQuest
{
    public class QuestObjectiveRegistry : IQuestObjectiveRegistry
    {
        private readonly Dictionary<string, ActiveActionObjective> objectiveRegistry;
        private readonly ICoreAPI api;

        public QuestObjectiveRegistry(Dictionary<string, ActiveActionObjective> objectiveRegistry, ICoreAPI api)
        {
            this.objectiveRegistry = objectiveRegistry;
            this.api = api;
        }

        public void RegisterObjectives()
        {
            objectiveRegistry.Add("plantflowers", new NearbyFlowersActionObjective());
            objectiveRegistry.Add("hasAttribute", new PlayerHasAttributeActionObjective());
            objectiveRegistry.Add("interactat", new InteractAtCoordinateObjective());
            objectiveRegistry.Add("checkvariable", new CheckVariableObjective());
            objectiveRegistry.Add("interactcount", new InteractCountObjective());
            objectiveRegistry.Add("randomkill", new RandomKillObjective());
            objectiveRegistry.Add("walkdistance", new WalkDistanceObjective());
            objectiveRegistry.Add("timeofday", new TimeOfDayObjective());
        }
    }
}
