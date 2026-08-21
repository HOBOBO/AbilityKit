using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Config.Authoring
{
    public static class TriggerAuthoringSchema
    {
        public const string Id = "abilitykit-trigger-authoring";
        public const string Version = "2.2";
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
        IntegerList = 7,
        Vector3 = 8
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

    [Flags]
    public enum TriggerTemplateValueSourceMask
    {
        None = 0,
        Constant = 1 << (int)TriggerValueSource.Constant,
        Payload = 1 << (int)TriggerValueSource.Payload,
        Context = 1 << (int)TriggerValueSource.Context,
        LocalBlackboard = 1 << (int)TriggerValueSource.LocalBlackboard,
        GlobalBlackboard = 1 << (int)TriggerValueSource.GlobalBlackboard,
        TemplateParameter = 1 << (int)TriggerValueSource.TemplateParameter,
        Expression = 1 << (int)TriggerValueSource.Expression,
        InstanceBinding = Constant | Payload | Context | LocalBlackboard | GlobalBlackboard | Expression,
        All = InstanceBinding | TemplateParameter
    }

    public enum TriggerEventMatchMode
    {
        Exact = 0,
        Prefix = 1
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
    public sealed class TriggerAuthoringTemplateSourceDocument
    {
        public string Schema = TriggerAuthoringSchema.Id;
        public string Version = TriggerAuthoringSchema.Version;
        public TriggerAuthoringSourceMetadata Metadata = new TriggerAuthoringSourceMetadata();
        public TriggerAuthoringTemplateData Template = new TriggerAuthoringTemplateData();
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
        public List<TriggerNodeGroupData> ConditionGroups = new List<TriggerNodeGroupData>();
        public List<TriggerNodeGroupData> ActionGroups = new List<TriggerNodeGroupData>();
        public List<TriggerDefinitionData> Triggers = new List<TriggerDefinitionData>();
    }

    [Serializable]
    public sealed class TriggerNodeGroupData
    {
        public string Id;
        public string DisplayName;
        public string Description;
        public TriggerNodeData Root;
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
        public string GroupReference;
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
        public TriggerVector3Data Vector3Value = new TriggerVector3Data();
        public string Path;
        public string Expression;
    }

    [Serializable]
    public sealed class TriggerVector3Data
    {
        public double X;
        public double Y;
        public double Z;
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
    public sealed class TriggerPayloadFieldData
    {
        public string Path;
        public string DisplayName;
        public TriggerValueType Type;
        public string Description;
    }

    [Serializable]
    public sealed class TriggerEventDefinitionData
    {
        public string Id;
        public TriggerEventMatchMode MatchMode;
        public string DisplayName;
        public string Category;
        public string PayloadType;
        public List<TriggerPayloadFieldData> PayloadFields = new List<TriggerPayloadFieldData>();
        public bool AllowExternal;
        public bool Deterministic = true;
        public string Description;
    }

    [Serializable]
    public sealed class TriggerGlobalBlackboardKeyData
    {
        public string Key;
        public string DisplayName;
        public TriggerValueType Type;
        public TriggerValueRefData DefaultValue = new TriggerValueRefData();
        public bool CanRead = true;
        public bool CanWrite = true;
        public string Domain = "global";
        public string Description;
    }

    [Serializable]
    public sealed class TriggerTemplateReferenceData
    {
        public string TemplateId;
        public string Version;
        public List<TriggerArgumentData> Bindings = new List<TriggerArgumentData>();
    }

    [Serializable]
    public sealed class TriggerAuthoringTemplateData
    {
        public string TemplateId;
        public string TemplateVersion = "1.0.0";
        public string DisplayName;
        public string Description;
        public string Event;
        public List<TriggerAuthoringTemplateParameterData> Parameters =
            new List<TriggerAuthoringTemplateParameterData>();
        public TriggerNodeData Condition;
        public TriggerNodeData Actions;
    }

    [Serializable]
    public sealed class TriggerAuthoringTemplateParameterData
    {
        public string Name;
        public TriggerValueType Type;
        public bool Required = true;
        public TriggerTemplateValueSourceMask AllowedSources = TriggerTemplateValueSourceMask.InstanceBinding;
        public bool HasDefault;
        public TriggerValueRefData DefaultValue = new TriggerValueRefData();
        public string Description;
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
