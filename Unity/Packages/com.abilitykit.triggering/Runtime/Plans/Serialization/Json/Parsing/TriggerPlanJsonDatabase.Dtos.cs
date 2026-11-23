using System;
using System.Collections.Generic;
using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Config.Plans;
using Newtonsoft.Json;

namespace AbilityKit.Triggering.Runtime.Plan.Json
{
    public sealed partial class TriggerPlanJsonDatabase
    {
#pragma warning disable 0649 // DTO fields are populated by JSON deserialization.
        [Serializable]
        internal sealed class TriggerPlanDatabaseDto
        {
            public int FormatVersion;

            public List<TriggerPlanDto> Triggers;

            public Dictionary<string, TriggerPlanModuleTemplateDto> Templates;

            public Dictionary<string, TriggerPlanModuleTemplateDto> Modules;

            public List<TriggerPlanModuleInstanceDto> ModuleInstances;

            public List<TriggerPlanModuleInstanceDto> TemplateInstances;

            public Dictionary<string, ExecutionNodeDto> Behaviors;

            public Dictionary<string, ExecutionNodeDto> Nodes;

            public Dictionary<int, string> Strings;

            public List<BlackboardInitializationPlan> Blackboards;
        }

        [Serializable]
        internal sealed class TriggerPlanDto
        {
            public int TriggerId;
            public string EventName;
            public int EventId;
            public bool AllowExternal;
            public int Phase;
            public int Priority;
            public TriggerPlanScope Scope;
            public TriggerTemplateBindingDto Template;
            public PredicatePlanDto Predicate;
            public List<ActionCallPlanDto> Actions;
            public ExecutionControlPlanDto ExecutionControl;
            public ExecutionNodeDto ExecutionRoot;
 
            /// <summary>
            /// Cue 模板、通道或资源键。
            /// </summary>
            public string CueId;

            /// <summary>
            /// 主资源标识。
            /// </summary>
            public string CuePrimaryAssetId;

            /// <summary>
            /// 次资源标识。
            /// </summary>
            public string CueSecondaryAssetId;

            /// <summary>
            /// 项目自定义扩展载荷。
            /// </summary>
            public string CuePayload;
        }

        [Serializable]
        internal sealed class TriggerPlanModuleTemplateDto
        {
            public string Id;
            public string TemplateId;
            public string ModuleId;
            public string DisplayName;
            public string Description;
            public List<TemplateParameterDto> Parameters;
            public Dictionary<string, NumericValueRefDto> Defaults;
            public List<TriggerPlanDto> Triggers;
            public Dictionary<string, ExecutionNodeDto> Behaviors;
            public Dictionary<string, ExecutionNodeDto> Nodes;
        }

        [Serializable]
        internal sealed class TriggerPlanModuleInstanceDto
        {
            public string Id;
            public string InstanceId;
            public string TemplateId;
            public string ModuleId;
            public int TriggerIdOffset;
            public int TriggerIdBase;
            public string EventNamePrefix;
            public string EventNameSuffix;
            public Dictionary<string, NumericValueRefDto> Bindings;
        }

        [Serializable]
        internal sealed class TemplateParameterDto
        {
            public string Name;
            public string Kind;
            public bool Required;
            public string Description;
            public NumericValueRefDto Default;
        }

        [Serializable]
        internal sealed class TriggerTemplateBindingDto
        {
            public string TemplateId;
            public Dictionary<string, NumericValueRefDto> Bindings;
        }

        [Serializable]
        internal sealed class PredicatePlanDto
        {
            public string Kind;
            public List<BoolExprNodeDto> Nodes;
        }

        [Serializable]
        internal sealed class BoolExprNodeDto
        {
            public string Kind;
            public bool ConstValue;
            public string CompareOp;
            public NumericValueRefDto Left;
            public NumericValueRefDto Right;
            public int FunctionId;
            public int FunctionArity;
        }

        [Serializable]
        internal sealed class ActionCallPlanDto
        {
            public int ActionId;
            public int Arity;
            public NumericValueRefDto Arg0;
            public NumericValueRefDto Arg1;

            /// <summary>
            /// 具名参数字典（key=参数名）
            /// 优先级高于 Arg0/Arg1
            /// </summary>
            public Dictionary<string, NumericValueRefDto> Args;

            public string ScheduleMode;
            public float ScheduleParam;
            public int MaxExecutions = -1;
            public bool CanBeInterrupted = true;

            public string ExecutionPolicy;
            public int RetryMaxRetries = 3;
            public float RetryDelayMs;

            public string CueId;
            public string CuePrimaryAssetId;
            public string CueSecondaryAssetId;
            public string CuePayload;
        }

        [Serializable]
        internal sealed class ExecutionControlPlanDto
        {
            public string Mode;
            public int MaxExecutions;
            public float CooldownMs;
        }

        [Serializable]
        internal sealed class ExecutionNodeDto
        {
            public string Kind;
            public string BehaviorRef;
            public string BehaviorId;
            public string NodeRef;
            public string NodeId;
            public string Ref;
            public ActionCallPlanDto Action;
            public PredicatePlanDto Condition;
            public PredicatePlanDto UntilCondition;
            public NumericValueRefDto Collection;
            public NumericValueRefDto ItemTarget;
            public List<ExecutionNodeDto> Children;
            public List<ExecutionNodeDto> ElseChildren;
            public int Count = 1;
            public int MaxIterations = 1;
            public float Weight = 1f;
            public string Reason;
            public string ScheduleMode;
            public float IntervalMs;
            public int MaxExecutions = -1;
            public bool CanBeInterrupted = true;
            public string MetadataKind;
            public Dictionary<string, string> Values;
            public string SourceKind;
            public string SourceId;
            public string SourcePath;
        }

        [Serializable]
        internal sealed class NumericValueRefDto
        {
            public string Kind;
            public double ConstValue;
            public int BoardId;
            public int KeyId;
            public int FieldId;
            public string DomainId;
            public string Key;
            public string ExprText;
            public bool Required;
            public bool HasFallback;
            public double FallbackValue;
            public bool HasMin;
            public double MinValue;
            public bool HasMax;
            public double MaxValue;
            public bool HasScale;
            public double Scale = 1d;
            public double Offset;
            public string Label;
            public string Scope;
            public BlackboardKeyType KeyType;
            public bool BoolValue;
            public string StringValue;
        }
#pragma warning restore 0649
    }
}
