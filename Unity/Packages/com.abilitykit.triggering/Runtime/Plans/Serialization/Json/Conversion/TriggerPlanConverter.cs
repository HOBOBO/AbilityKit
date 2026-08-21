using System;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Variables.Numeric;

namespace AbilityKit.Triggering.Runtime.Plan.Json
{
    /// <summary>
    /// 触发器计划转换器
    /// 统一 TriggerPlanJsonDatabase 的转换逻辑
    /// </summary>
    internal sealed class TriggerPlanConverter
    {
        private const string TemplateParamKind = "TemplateParam";
        private System.Collections.Generic.Dictionary<string, TriggerPlanJsonDatabase.NumericValueRefDto> _templateBindings;

        internal TriggerPlan<object> Convert(TriggerPlanJsonDatabase.TriggerPlanDto dto, ITriggerCue cue = null)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            var previousBindings = _templateBindings;
            _templateBindings = BuildTemplateBindings(dto.Template);
            try
            {
                return ConvertCore(dto, cue);
            }
            finally
            {
                _templateBindings = previousBindings;
            }
        }

        private TriggerPlan<object> ConvertCore(TriggerPlanJsonDatabase.TriggerPlanDto dto, ITriggerCue cue)
        {
            var actions = ConvertActions(dto.Actions);
            var pred = dto.Predicate;

            if (pred == null || string.Equals(pred.Kind, "none", StringComparison.OrdinalIgnoreCase))
            {
                return new TriggerPlan<object>(
                    phase: dto.Phase,
                    priority: dto.Priority,
                    triggerId: dto.TriggerId,
                    actions: actions,
                    interruptPriority: 0,
                    cue: cue,
                    schedule: default,
                    executionControl: ConvertExecutionControl(dto.ExecutionControl));
            }

            if (string.Equals(pred.Kind, "expr", StringComparison.OrdinalIgnoreCase))
            {
                var expr = new PredicateExprPlan(BuildExprNodes(pred.Nodes));
                return new TriggerPlan<object>(
                    phase: dto.Phase,
                    priority: dto.Priority,
                    triggerId: dto.TriggerId,
                    predicateExpr: expr,
                    actions: actions,
                    interruptPriority: 0,
                    cue: cue,
                    schedule: default,
                    executionControl: ConvertExecutionControl(dto.ExecutionControl));
            }

            throw new NotSupportedException($"Predicate kind not supported: {pred.Kind}");
        }

        private static TriggerExecutionControlPlan ConvertExecutionControl(TriggerPlanJsonDatabase.ExecutionControlPlanDto dto)
        {
            if (dto == null || string.IsNullOrEmpty(dto.Mode))
            {
                return TriggerExecutionControlPlan.Always;
            }

            var modeText = dto.Mode.Trim().ToLowerInvariant();
            switch (modeText)
            {
                case "once":
                    return new TriggerExecutionControlPlan(ETriggerExecutionMode.Once, maxExecutions: 1);
                case "cooldown":
                    return new TriggerExecutionControlPlan(ETriggerExecutionMode.Cooldown, cooldownMs: Math.Max(0f, dto.CooldownMs));
                case "repeat":
                    return new TriggerExecutionControlPlan(ETriggerExecutionMode.Repeat, maxExecutions: Math.Max(0, dto.MaxExecutions));
                case "always":
                default:
                    return TriggerExecutionControlPlan.Always;
            }
        }

        internal ActionCallPlan[] ConvertActions(System.Collections.Generic.List<TriggerPlanJsonDatabase.ActionCallPlanDto> dtos)
        {
            if (dtos == null || dtos.Count == 0) return Array.Empty<ActionCallPlan>();

            var arr = new ActionCallPlan[dtos.Count];
            for (int i = 0; i < dtos.Count; i++)
            {
                arr[i] = ConvertAction(dtos[i]);
            }
            return arr;
        }

        internal ITriggerPlanExecutable ConvertExecutionRoot(TriggerPlanJsonDatabase.TriggerPlanDto dto)
        {
            return ConvertExecutionRoot(dto, null);
        }

        internal ITriggerPlanExecutable ConvertExecutionRoot(
            TriggerPlanJsonDatabase.TriggerPlanDto dto,
            TriggerPlanJsonDatabase.TriggerPlanDatabaseDto databaseDto)
        {
            if (dto == null) return null;

            var previousBindings = _templateBindings;
            _templateBindings = BuildTemplateBindings(dto.Template);
            try
            {
                return new TriggerPlanExecutionNodeConverter(this).ConvertExecutionRoot(dto, databaseDto);
            }
            finally
            {
                _templateBindings = previousBindings;
            }
        }

        internal ActionCallPlan ConvertAction(TriggerPlanJsonDatabase.ActionCallPlanDto dto)
        {
            if (dto == null) return default;

            var id = new ActionId(dto.ActionId);
            var scheduleMode = ParseActionScheduleMode(dto.ScheduleMode);
            var executionPolicy = ParseActionExecutionPolicy(dto.ExecutionPolicy);
            var retryMaxRetries = dto.RetryMaxRetries;

            if (dto.RetryMaxRetries < 0)
            {
                throw new InvalidOperationException($"RetryMaxRetries cannot be negative: {dto.RetryMaxRetries} actionId={dto.ActionId}");
            }

            if (dto.RetryDelayMs < 0f)
            {
                throw new InvalidOperationException($"RetryDelayMs cannot be negative: {dto.RetryDelayMs} actionId={dto.ActionId}");
            }

            var cueDescriptor = BuildActionCueDescriptor(dto);

            if (dto.Args != null && dto.Args.Count > 0)
            {
                if (dto.Arity > 2)
                {
                    throw new InvalidOperationException($"Unsupported named action arity: {dto.Arity} actionId={dto.ActionId}. PlannedTrigger currently supports arity 0/1/2; use Action schema named args for additional business parameters.");
                }

                var namedArgs = new System.Collections.Generic.Dictionary<string, ActionArgValue>(dto.Args.Count, StringComparer.OrdinalIgnoreCase);
                foreach (var kv in dto.Args)
                {
                    namedArgs[kv.Key] = ConvertActionArg(kv.Value, kv.Key);
                }

                var arity = dto.Arity;
                var arg0 = arity > 0 ? ConvertNumericValueRef(dto.Arg0) : default;
                var arg1 = arity > 1 ? ConvertNumericValueRef(dto.Arg1) : default;
                return new ActionCallPlan(
                    id,
                    (byte)arity,
                    arg0,
                    arg1,
                    namedArgs,
                    scheduleMode,
                    dto.ScheduleParam,
                    dto.MaxExecutions,
                    dto.CanBeInterrupted,
                    executionPolicy,
                    retryMaxRetries,
                    dto.RetryDelayMs,
                    in cueDescriptor);
            }

            ActionCallPlan plan;
            switch (dto.Arity)
            {
                case 0:
                    plan = new ActionCallPlan(id);
                    break;
                case 1:
                    plan = new ActionCallPlan(id, ConvertNumericValueRef(dto.Arg0));
                    break;
                case 2:
                    plan = new ActionCallPlan(id, ConvertNumericValueRef(dto.Arg0), ConvertNumericValueRef(dto.Arg1));
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported action arity: {dto.Arity} actionId={dto.ActionId}");
            }

            return new ActionCallPlan(
                plan.Id,
                plan.Arity,
                plan.Arg0,
                plan.Arg1,
                plan.Args,
                scheduleMode,
                dto.ScheduleParam,
                dto.MaxExecutions,
                dto.CanBeInterrupted,
                executionPolicy,
                retryMaxRetries,
                dto.RetryDelayMs,
                in cueDescriptor);
        }

        private ActionArgValue ConvertActionArg(TriggerPlanJsonDatabase.NumericValueRefDto dto, string name)
        {
            if (dto != null && string.Equals(dto.Kind, "BlackboardTarget", StringComparison.OrdinalIgnoreCase))
            {
                if (dto.BoardId == 0 || dto.KeyId == 0)
                    throw new InvalidOperationException($"Blackboard target requires non-zero BoardId and KeyId. argument={name}");
                if (dto.KeyType == BlackboardKeyType.Unknown)
                    throw new InvalidOperationException($"Blackboard target requires KeyType. argument={name}");

                var target = new BlackboardWriteTarget(dto.BoardId, dto.KeyId, dto.KeyType, dto.Scope);
                return ActionArgValue.OfBlackboardTarget(in target, name);
            }

            if (dto != null && string.Equals(dto.Kind, "Bool", StringComparison.OrdinalIgnoreCase))
                return ActionArgValue.OfBool(dto.BoolValue, name);
            if (dto != null && string.Equals(dto.Kind, "String", StringComparison.OrdinalIgnoreCase))
                return ActionArgValue.OfString(dto.StringValue, name);

            return new ActionArgValue(ConvertNumericValueRef(dto), name);
        }

        private static TriggerCueDescriptor BuildActionCueDescriptor(TriggerPlanJsonDatabase.ActionCallPlanDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.CueId)) return TriggerCueDescriptor.Empty;

            return new TriggerCueDescriptor(
                kind: dto.CueId,
                cueId: dto.CueId,
                primaryAssetId: dto.CuePrimaryAssetId,
                secondaryAssetId: dto.CueSecondaryAssetId,
                payload: dto.CuePayload,
                level: ECueLevel.Behavior);
        }

        private static EActionScheduleMode ParseActionScheduleMode(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return EActionScheduleMode.Immediate;
            }

            if (Enum.TryParse<EActionScheduleMode>(value, true, out var mode))
            {
                return mode;
            }

            throw new InvalidOperationException($"Unknown action schedule mode: {value}");
        }

        private static EActionExecutionPolicy ParseActionExecutionPolicy(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return EActionExecutionPolicy.Immediate;
            }

            if (Enum.TryParse<EActionExecutionPolicy>(value, true, out var policy))
            {
                return policy;
            }

            throw new InvalidOperationException($"Unknown action execution policy: {value}");
        }

        private NumericValueRef ConvertNumericValueRef(TriggerPlanJsonDatabase.NumericValueRefDto dto)
        {
            if (dto == null) return default;

            dto = ResolveTemplateParam(dto, new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase));

            if (!Enum.TryParse<ENumericValueRefKind>(dto.Kind, out var kind))
            {
                throw new InvalidOperationException($"Unknown NumericValueRef kind: {dto.Kind}");
            }

            var valueRef = kind switch
            {
                ENumericValueRefKind.Const => NumericValueRef.Const(dto.ConstValue),
                ENumericValueRefKind.Blackboard => NumericValueRef.Blackboard(dto.BoardId, dto.KeyId),
                ENumericValueRefKind.PayloadField => NumericValueRef.PayloadField(dto.FieldId),
                ENumericValueRefKind.Var => NumericValueRef.Var(dto.DomainId, dto.Key),
                ENumericValueRefKind.Expr => NumericValueRef.Expr(dto.ExprText),
                _ => throw new InvalidOperationException($"Unsupported NumericValueRef kind: {kind}")
            };

            if (dto.Required) valueRef = valueRef.AsRequired();
            if (dto.HasFallback) valueRef = valueRef.WithFallback(dto.FallbackValue);
            if (dto.HasMin) valueRef = valueRef.WithMin(dto.MinValue);
            if (dto.HasMax) valueRef = valueRef.WithMax(dto.MaxValue);
            if (dto.HasScale) valueRef = valueRef.WithScale(dto.Scale);
            if (dto.Offset != 0d) valueRef = valueRef.WithOffset(dto.Offset);
            if (!string.IsNullOrEmpty(dto.Label)) valueRef = valueRef.WithLabel(dto.Label);
            if (!string.IsNullOrEmpty(dto.Scope)) valueRef = valueRef.WithScope(dto.Scope);

            return valueRef;
        }

        private static System.Collections.Generic.Dictionary<string, TriggerPlanJsonDatabase.NumericValueRefDto> BuildTemplateBindings(TriggerPlanJsonDatabase.TriggerTemplateBindingDto dto)
        {
            if (dto == null || dto.Bindings == null || dto.Bindings.Count == 0)
            {
                return null;
            }

            return new System.Collections.Generic.Dictionary<string, TriggerPlanJsonDatabase.NumericValueRefDto>(dto.Bindings, StringComparer.OrdinalIgnoreCase);
        }

        private TriggerPlanJsonDatabase.NumericValueRefDto ResolveTemplateParam(
            TriggerPlanJsonDatabase.NumericValueRefDto dto,
            System.Collections.Generic.HashSet<string> resolving)
        {
            while (dto != null && string.Equals(dto.Kind, TemplateParamKind, StringComparison.OrdinalIgnoreCase))
            {
                var key = dto.Key;
                if (string.IsNullOrEmpty(key))
                {
                    throw new InvalidOperationException("TemplateParam NumericValueRef requires Key.");
                }

                if (_templateBindings == null || !_templateBindings.TryGetValue(key, out var bound) || bound == null)
                {
                    throw new InvalidOperationException($"Template parameter is not bound: {key}");
                }

                if (!resolving.Add(key))
                {
                    throw new InvalidOperationException($"Cyclic template parameter binding detected: {key}");
                }

                dto = bound;
            }

            return dto;
        }

        internal ITriggerPlanCondition ConvertCondition(TriggerPlanJsonDatabase.PredicatePlanDto dto)
        {
            var expr = ConvertPredicateExpr(dto);
            return expr.Nodes == null || expr.Nodes.Length == 0 ? null : new PredicateExprTriggerPlanCondition(expr);
        }

        private PredicateExprPlan ConvertPredicateExpr(TriggerPlanJsonDatabase.PredicatePlanDto dto)
        {
            if (dto == null || string.Equals(dto.Kind, "none", StringComparison.OrdinalIgnoreCase))
                return default;

            if (!string.Equals(dto.Kind, "expr", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException($"Predicate kind not supported: {dto.Kind}");
            }

            return new PredicateExprPlan(BuildExprNodes(dto.Nodes));
        }

        private BoolExprNode[] BuildExprNodes(System.Collections.Generic.List<TriggerPlanJsonDatabase.BoolExprNodeDto> dtos)
        {
            if (dtos == null || dtos.Count == 0) return Array.Empty<BoolExprNode>();

            var arr = new BoolExprNode[dtos.Count];
            for (int i = 0; i < dtos.Count; i++)
            {
                var d = dtos[i];
                if (d == null)
                {
                    arr[i] = BoolExprNode.Const(true);
                    continue;
                }

                if (!Enum.TryParse<EBoolExprNodeKind>(d.Kind, out var kind))
                {
                    throw new InvalidOperationException($"Unknown expr node kind: {d.Kind}");
                }

                switch (kind)
                {
                    case EBoolExprNodeKind.Const:
                        arr[i] = BoolExprNode.Const(d.ConstValue);
                        break;
                    case EBoolExprNodeKind.Not:
                        arr[i] = BoolExprNode.Not();
                        break;
                    case EBoolExprNodeKind.And:
                        arr[i] = BoolExprNode.And();
                        break;
                    case EBoolExprNodeKind.Or:
                        arr[i] = BoolExprNode.Or();
                        break;
                    case EBoolExprNodeKind.CompareNumeric:
                        if (!Enum.TryParse<ECompareOp>(d.CompareOp, out var op))
                        {
                            throw new InvalidOperationException($"Unknown compare op: {d.CompareOp}");
                        }
                        arr[i] = BoolExprNode.Compare(op, ConvertNumericValueRef(d.Left), ConvertNumericValueRef(d.Right));
                        break;
                    case EBoolExprNodeKind.Function:
                        arr[i] = BoolExprNode.Function(new FunctionId(d.FunctionId), (byte)d.FunctionArity, ConvertNumericValueRef(d.Left), ConvertNumericValueRef(d.Right));
                        break;
                    default:
                        throw new InvalidOperationException($"Unsupported expr node kind: {kind}");
                }
            }

            return arr;
        }

    }
}
