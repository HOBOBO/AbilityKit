using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Share.CoreDtos
{
    [Serializable]
    public sealed class TriggerHeaderDTO
    {
        public int TriggerId;
        public string EventId;
    }

    [Serializable]
    public sealed class AbilityTriggerDatabaseDTO
    {
        public List<TriggerDTO> Triggers = new List<TriggerDTO>();
    }

    [Serializable]
    public sealed class TriggerDTO
    {
        public int TriggerId;
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
        public List<ConditionDTO> Items;
        public ConditionDTO Item;
    }

    [Serializable]
    public sealed class ActionDTO
    {
        public string Type;
        public Dictionary<string, object> Args;
        public List<ActionDTO> Items;
    }
}
