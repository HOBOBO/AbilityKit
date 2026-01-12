using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Triggering.Json
{
    [Serializable]
    public sealed class AbilityTriggerDatabaseDTO
    {
        public List<AbilityTriggerEntryDTO> Abilities = new List<AbilityTriggerEntryDTO>();
    }

    [Serializable]
    public sealed class AbilityTriggerEntryDTO
    {
        public string AbilityId;
        public List<TriggerDTO> Triggers = new List<TriggerDTO>();
    }

    [Serializable]
    public sealed class TriggerDTO
    {
        public string EventId;
        public Dictionary<string, object> InitialLocalVars;
        public List<ConditionDTO> Conditions = new List<ConditionDTO>();
        public List<ActionDTO> Actions = new List<ActionDTO>();
    }

    [Serializable]
    public sealed class ConditionDTO
    {
        public string Type;
        public Dictionary<string, object> Args;
    }

    [Serializable]
    public sealed class ActionDTO
    {
        public string Type;
        public Dictionary<string, object> Args;
    }
}
