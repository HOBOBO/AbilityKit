using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Config.Authoring
{
    public static class TriggerAuthoringSchema
    {
        public const string Id = "abilitykit-trigger-authoring";
        public const string Version = "2.0";
    }

    public enum TriggerModuleKind
    {
        Ability = 0,
        Buff = 1,
        Passive = 2,
        Projectile = 3,
        Summon = 4,
        Custom = 100
    }

    public enum TriggerNodeKind
    {
        Condition = 0,
        Action = 1
    }

    public enum TriggerValueType
    {
        None = 0,
        Integer = 1,
        Number = 2,
        Boolean = 3,
        String = 4,
        Entity = 5,
        ObjectId = 6,
        IntegerList = 7
    }

    public enum TriggerValueSource
    {
        Constant = 0,
        Payload = 1,
        Context = 2,
        LocalBlackboard = 3,
        GlobalBlackboard = 4,
        TemplateParameter = 5,
        Expression = 6
    }

    [Serializable]
    public sealed class TriggerAuthoringSourceDocument
    {
        public string Schema = TriggerAuthoringSchema.Id;
        public string Version = TriggerAuthoringSchema.Version;
        public TriggerAuthoringSourceMetadata Metadata = new TriggerAuthoringSourceMetadata();
        public TriggerAuthoringModuleData Module = new TriggerAuthoringModuleData();
    }

    [Serializable]
    public sealed class TriggerAuthoringSourceMetadata
    {
        public string Author = "team";
        public string Description;
    }

    [Serializable]
    public sealed class TriggerAuthoringModuleData
    {
        public string ModuleId;
        public string DisplayName;
        public TriggerModuleKind Kind;
        public List<TriggerBlackboardVariableData> Blackboard = new List<TriggerBlackboardVariableData>();
        public List<TriggerDefinitionData> Triggers = new List<TriggerDefinitionData>();
    }

    [Serializable]
    public sealed class TriggerDefinitionData
    {
        public int Id;
        public string Name;
        public bool Enabled = true;
        public string Event;
        public string Phase = "immediate";
        public int Priority;
        public int InterruptPriority;
        public string Scope = "owner";
        public bool AllowExternal;
        public TriggerScheduleData Schedule = new TriggerScheduleData();
        public TriggerCueData Cue = new TriggerCueData();
        public TriggerExecutionControlData ExecutionControl = new TriggerExecutionControlData();
        public TriggerTemplateReferenceData Template;
        public TriggerNodeData Condition;
        public TriggerNodeData Actions;
        public List<TriggerBlackboardVariableData> Blackboard = new List<TriggerBlackboardVariableData>();
        public string Note;
    }

    [Serializable]
    public sealed class TriggerNodeData
    {
        public TriggerNodeKind Kind;
        public string Type;
        public string Note;
        public List<TriggerArgumentData> Arguments = new List<TriggerArgumentData>();
        public List<TriggerNodeData> Children = new List<TriggerNodeData>();
    }

    [Serializable]
    public sealed class TriggerArgumentData
    {
        public string Name;
        public TriggerValueRefData Value = new TriggerValueRefData();
    }

    [Serializable]
    public sealed class TriggerValueRefData
    {
        public TriggerValueSource Source;
        public TriggerValueType Type;
        public long IntegerValue;
        public double NumberValue;
        public bool BooleanValue;
        public string StringValue;
        public List<long> IntegerListValue = new List<long>();
        public string Path;
        public string Expression;
    }

    [Serializable]
    public sealed class TriggerBlackboardVariableData
    {
        public string Key;
        public TriggerValueType Type;
        public bool ReadOnly;
        public string Description;
        public TriggerValueRefData DefaultValue = new TriggerValueRefData();
    }

    [Serializable]
    public sealed class TriggerTemplateReferenceData
    {
        public string TemplateId;
        public string Version;
        public List<TriggerArgumentData> Bindings = new List<TriggerArgumentData>();
    }

    [Serializable]
    public sealed class TriggerScheduleData
    {
        public string Mode = "transient";
        public int DelayMilliseconds;
        public int IntervalMilliseconds;
        public int RepeatCount;
    }

    [Serializable]
    public sealed class TriggerCueData
    {
        public string CueId;
    }

    [Serializable]
    public sealed class TriggerExecutionControlData
    {
        public string InterruptPolicy = "none";
        public bool StopPropagationOnSuccess;
        public bool StopPropagationOnFailure;
    }
}
