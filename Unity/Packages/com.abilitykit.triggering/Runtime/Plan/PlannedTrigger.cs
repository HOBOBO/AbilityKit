using System;
using System.Buffers;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Payload;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.ActionScheduler;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Dispatcher;
using AbilityKit.Triggering.Variables.Numeric;
using AbilityKit.Triggering.Variables.Numeric.Expression;

namespace AbilityKit.Triggering.Runtime.Plan
{
    public sealed class PlannedTrigger<TArgs, TCtx> : ITrigger<TArgs, TCtx>, ITriggerWithId
        where TArgs : class
    {
        // 使用公共委托类型（在 TriggerDelegates.cs 中定义）
        private Predicate0<TArgs, TCtx> _predicate0;
        private Predicate1<TArgs, TCtx> _predicate1;
        private Predicate2<TArgs, TCtx> _predicate2;

        private Action0<TArgs, TCtx>[] _actions0;
        private Action1<TArgs, TCtx>[] _actions1;
        private Action2<TArgs, TCtx>[] _actions2;

        /// <summary>
        /// 具名参数模式的 Action 委托数组
        /// 与 _actions0/1/2 并行存储，但优先级更高（有 NamedArgs 时用这些）
        /// 注意：TActionArgs 固定为 object，因为 Schema 会在运行时将其解析为强类型
        /// </summary>
        private NamedAction0<TArgs, object, TCtx>[] _namedActions0;
        private NamedAction1<TArgs, object, TCtx>[] _namedActions1;
        private NamedAction2<TArgs, object, TCtx>[] _namedActions2;

        /// <summary>
        /// 标记哪些 Action 使用了具名参数模式（与 actions 数组索引对应）
        /// </summary>
        private bool[] _useNamedArgs;

        /// <inheritdoc />
        public ITriggerCue Cue => _plan.Cue;

        /// <inheritdoc />
        public int TriggerId => _plan.TriggerId;

        public PlannedTrigger(in TriggerPlan<TArgs> plan)
        {
            _plan = plan;
            _resolved = false;
            _predicate0 = null;
            _predicate1 = null;
            _predicate2 = null;
            _actions0 = null;
            _actions1 = null;
            _actions2 = null;
            _namedActions0 = null;
            _namedActions1 = null;
            _namedActions2 = null;
            _useNamedArgs = null;
            _execCtx = default;
        }

        private readonly TriggerPlan<TArgs> _plan;
        private bool _resolved;
        private ExecCtx<TCtx> _execCtx;

        private ExecCtx<TCtx> ExecCtx => _execCtx;

        public bool Evaluate(in TArgs args, in ExecCtx<TCtx> ctx)
        {
            Resolve(ctx);
            if (_plan.PredicateKind == EPredicateKind.None || !_plan.HasPredicate) return true;

            if (_plan.PredicateKind == EPredicateKind.Expr)
            {
                return EvaluateExpr(in args, in ctx);
            }

            if (_plan.PredicateKind != EPredicateKind.Function)
            {
                throw new InvalidOperationException($"Unsupported predicate kind: {_plan.PredicateKind}");
            }

            switch (_plan.PredicateArity)
            {
                case 0:
                    return _predicate0(args, ctx);
                case 1:
                    return _predicate1(args, ResolveNumeric(in args, in _plan.PredicateArg0, in ctx), ctx);
                case 2:
                    return _predicate2(args, ResolveNumeric(in args, in _plan.PredicateArg0, in ctx), ResolveNumeric(in args, in _plan.PredicateArg1, in ctx), ctx);
                default:
                    throw new InvalidOperationException($"Unsupported predicate arity: {_plan.PredicateArity}");
            }
        }

        public void Execute(in TArgs args, in ExecCtx<TCtx> ctx)
        {
            Resolve(ctx);
            var actions = _plan.Actions;
            var hasActions = actions != null && actions.Length > 0;

            if (!hasActions) return;

            // 检查是否启用新 Action 调度模式
            bool useActionScheduler = ctx.ActionSchedulerManager != null && 
                                       (_plan.Actions != null && _plan.Actions.Length > 0) &&
                                       (_plan.Actions[0].ScheduleMode != Config.EActionScheduleMode.Immediate);

            if (!useActionScheduler)
            {
                // 旧模式：直接执行（Immediate 模式）
                ExecuteImmediate(in args, in ctx);
                return;
            }

            // 新模式：创建 ActionScheduler 并注册 Actions
            var actionScheduler = ctx.ActionSchedulerManager.GetOrCreateScheduler(_plan.TriggerId);
            var control = ctx.Control ?? new ExecutionControl();

            for (int i = 0; i < actions.Length; i++)
            {
                var call = actions[i];

                // 创建 Action 委托适配器
                var actionDelegate = CreateActionDelegate(i);
                var conditionDelegate = CreateConditionDelegate();

                // 创建执行器（根据 ExecutionPolicy）
                IActionExecutor executor = CreateExecutor(call, actionDelegate, control);

                // 注册到 ActionScheduler
                actionScheduler.Register(
                    plan: call,
                    actionDelegate: actionDelegate,
                    conditionDelegate: conditionDelegate,
                    boundArgs: args,
                    executor: executor
                );
            }

            // 执行成功后：如果声明了 InterruptPriority，自动设置优先级打断
            if (control != null && _plan.InterruptPriority > 0)
            {
                control.StopBelowPriority(
                    _plan.InterruptPriority,
                    conditionPassed: true,
                    _plan.TriggerId,
                    $"Trigger[{_plan.TriggerId}]"
                );
            }
        }

        /// <summary>
        /// 立即执行模式（向后兼容）
        /// </summary>
        private void ExecuteImmediate(in TArgs args, in ExecCtx<TCtx> ctx)
        {
            var actions = _plan.Actions;
            for (int i = 0; i < actions.Length; i++)
            {
                var call = actions[i];

                if (call.HasNamedArgs && _useNamedArgs[i])
                {
                    var rawArgs = ResolveNamedArgs(in args, in call, in ctx);
                    switch (call.Arity)
                    {
                        case 0:
                            _namedActions0[i](args, default, ctx);
                            break;
                        case 1:
                            _namedActions1[i](args, rawArgs, ctx);
                            break;
                        case 2:
                            _namedActions2[i](args, rawArgs, ctx);
                            break;
                        default:
                            throw new InvalidOperationException($"Unsupported action arity (named): {call.Arity}");
                    }
                }
                else
                {
                    switch (call.Arity)
                    {
                        case 0:
                            _actions0[i](args, ctx);
                            break;
                        case 1:
                            _actions1[i](args, ResolveNumeric(in args, in call.Arg0, in ctx), ctx);
                            break;
                        case 2:
                            _actions2[i](args, ResolveNumeric(in args, in call.Arg0, in ctx), ResolveNumeric(in args, in call.Arg1, in ctx), ctx);
                            break;
                        default:
                            throw new InvalidOperationException($"Unsupported action arity: {call.Arity}");
                    }
                }

                if (ctx.Control != null && (ctx.Control.StopPropagation || ctx.Control.Cancel)) return;
            }
        }

        /// <summary>
        /// 创建 Action 委托适配器
        /// </summary>
        private Action<object, ITriggerDispatcherContext> CreateActionDelegate(int index)
        {
            var call = _plan.Actions[index];
            var actions0 = _actions0;
            var actions1 = _actions1;
            var actions2 = _actions2;
            var namedActions0 = _namedActions0;
            var namedActions1 = _namedActions1;
            var namedActions2 = _namedActions2;
            var useNamedArgs = _useNamedArgs;

            if (call.HasNamedArgs && useNamedArgs[index])
            {
                return (argsObj, _) =>
                {
                    var args = (TArgs)argsObj;
                    var rawArgs = ResolveNamedArgs(in args, in call, ExecCtx);
                    switch (call.Arity)
                    {
                        case 0:
                            namedActions0[index]?.Invoke(args, default, ExecCtx);
                            break;
                        case 1:
                            namedActions1[index]?.Invoke(args, rawArgs, ExecCtx);
                            break;
                        case 2:
                            namedActions2[index]?.Invoke(args, rawArgs, ExecCtx);
                            break;
                    }
                };
            }
            else
            {
                return (argsObj, _) =>
                {
                    var args = (TArgs)argsObj;
                    switch (call.Arity)
                    {
                        case 0:
                            actions0[index]?.Invoke(args, ExecCtx);
                            break;
                        case 1:
                            var v0 = call.Arg0.Resolve(args);
                            actions1[index]?.Invoke(args, v0, ExecCtx);
                            break;
                        case 2:
                            var v1 = call.Arg0.Resolve(args);
                            var v2 = call.Arg1.Resolve(args);
                            actions2[index]?.Invoke(args, v1, v2, ExecCtx);
                            break;
                    }
                };
            }
        }

        /// <summary>
        /// 创建条件委托（如果 TriggerPlan 有 Predicate）
        /// </summary>
        private TriggerPredicate<object> CreateConditionDelegate()
        {
            if (!_plan.HasPredicate || _plan.PredicateKind != EPredicateKind.Function)
                return null;

            var predicate0 = _predicate0;
            var predicate1 = _predicate1;
            var predicate2 = _predicate2;
            var plan = _plan;

            return (argsObj, _) =>
            {
                var args = (TArgs)argsObj;
                return plan.PredicateArity switch
                {
                    0 => predicate0?.Invoke(args, ExecCtx) ?? true,
                    1 => predicate1?.Invoke(args, plan.PredicateArg0.Resolve(args), ExecCtx) ?? true,
                    2 => predicate2?.Invoke(args, plan.PredicateArg0.Resolve(args), plan.PredicateArg1.Resolve(args), ExecCtx) ?? true,
                    _ => true
                };
            };
        }

        /// <summary>
        /// 根据 ExecutionPolicy 创建执行器
        /// </summary>
        private IActionExecutor CreateExecutor(ActionCallPlan plan, Action<object, ITriggerDispatcherContext> action, ExecutionControl control)
        {
            var baseExecutor = new ActionScheduler.DefaultActionExecutor(action);

            return plan.ExecutionPolicy switch
            {
                Config.EActionExecutionPolicy.Queued => new ActionScheduler.QueuedActionExecutor(baseExecutor),
                Config.EActionExecutionPolicy.Parallel => baseExecutor, // 并行不排队
                Config.EActionExecutionPolicy.WithRetry => new ActionScheduler.RetryActionExecutor(baseExecutor),
                Config.EActionExecutionPolicy.Conditional => baseExecutor, // 条件已在 ActionInstance 中检查
                _ => baseExecutor
            };
        }

        private void Resolve(in ExecCtx<TCtx> ctx)
        {
            if (_resolved) return;

            if (_plan.HasPredicate && _plan.PredicateKind == EPredicateKind.Function)
            {
                switch (_plan.PredicateArity)
                {
                    case 0:
                        if (!ctx.Functions.TryGet<Predicate0<TArgs, TCtx>>(_plan.PredicateId, out var p0, out var p0Det))
                            throw new InvalidOperationException($"Predicate function not found or signature mismatch. id={FormatFunctionId(in ctx, _plan.PredicateId)} arity=0");
                        if (ctx.Policy.RequireDeterministic && !p0Det)
                            throw new InvalidOperationException($"Non-deterministic predicate is not allowed by policy. id={FormatFunctionId(in ctx, _plan.PredicateId)}");
                        _predicate0 = p0;
                        break;
                    case 1:
                        if (!ctx.Functions.TryGet<Predicate1<TArgs, TCtx>>(_plan.PredicateId, out var p1, out var p1Det))
                            throw new InvalidOperationException($"Predicate function not found or signature mismatch. id={FormatFunctionId(in ctx, _plan.PredicateId)} arity=1");
                        if (ctx.Policy.RequireDeterministic && !p1Det)
                            throw new InvalidOperationException($"Non-deterministic predicate is not allowed by policy. id={FormatFunctionId(in ctx, _plan.PredicateId)}");
                        _predicate1 = p1;
                        break;
                    case 2:
                        if (!ctx.Functions.TryGet<Predicate2<TArgs, TCtx>>(_plan.PredicateId, out var p2, out var p2Det))
                            throw new InvalidOperationException($"Predicate function not found or signature mismatch. id={FormatFunctionId(in ctx, _plan.PredicateId)} arity=2");
                        if (ctx.Policy.RequireDeterministic && !p2Det)
                            throw new InvalidOperationException($"Non-deterministic predicate is not allowed by policy. id={FormatFunctionId(in ctx, _plan.PredicateId)}");
                        _predicate2 = p2;
                        break;
                    default:
                        throw new InvalidOperationException($"Unsupported predicate arity: {_plan.PredicateArity}");
                }
            }

            if (_plan.Actions != null && _plan.Actions.Length > 0)
            {
                var len = _plan.Actions.Length;
                _actions0 = new Action0<TArgs, TCtx>[len];
                _actions1 = new Action1<TArgs, TCtx>[len];
                _actions2 = new Action2<TArgs, TCtx>[len];
                _namedActions0 = new NamedAction0<TArgs, object, TCtx>[len];
                _namedActions1 = new NamedAction1<TArgs, object, TCtx>[len];
                _namedActions2 = new NamedAction2<TArgs, object, TCtx>[len];
                _useNamedArgs = new bool[len];

                for (int i = 0; i < len; i++)
                {
                    var call = _plan.Actions[i];

                    if (call.HasNamedArgs)
                    {
                        // 具名参数模式：尝试注册 NamedAction 委托
                        var namedResolved = TryResolveNamedAction(call, i, ctx);
                        if (!namedResolved)
                        {
                            // 如果 NamedAction 注册失败，fallback 到位置参数模式
                            TryResolveLegacyAction(call, i, ctx);
                        }
                    }
                    else
                    {
                        // 传统位置参数模式
                        TryResolveLegacyAction(call, i, ctx);
                    }
                }
            }

            _execCtx = ctx;
            _resolved = true;
        }

        /// <summary>
        /// 尝试解析具名参数模式的 Action 委托
        /// </summary>
        private bool TryResolveNamedAction(ActionCallPlan call, int i, in ExecCtx<TCtx> ctx)
        {
            switch (call.Arity)
            {
                case 0:
                    if (ctx.Actions.TryGet<NamedAction0<TArgs, object, TCtx>>(call.Id, out var na0, out var na0Det))
                    {
                        if (ctx.Policy.RequireDeterministic && !na0Det)
                            throw new InvalidOperationException($"Non-deterministic named action is not allowed. id={FormatActionId(in ctx, call.Id)}");
                        _namedActions0[i] = na0;
                        _useNamedArgs[i] = true;
                        return true;
                    }
                    return false;

                case 1:
                    if (ctx.Actions.TryGet<NamedAction1<TArgs, object, TCtx>>(call.Id, out var na1, out var na1Det))
                    {
                        if (ctx.Policy.RequireDeterministic && !na1Det)
                            throw new InvalidOperationException($"Non-deterministic named action is not allowed. id={FormatActionId(in ctx, call.Id)}");
                        _namedActions1[i] = na1;
                        _useNamedArgs[i] = true;
                        return true;
                    }
                    return false;

                case 2:
                    if (ctx.Actions.TryGet<NamedAction2<TArgs, object, TCtx>>(call.Id, out var na2, out var na2Det))
                    {
                        if (ctx.Policy.RequireDeterministic && !na2Det)
                            throw new InvalidOperationException($"Non-deterministic named action is not allowed. id={FormatActionId(in ctx, call.Id)}");
                        _namedActions2[i] = na2;
                        _useNamedArgs[i] = true;
                        return true;
                    }
                    return false;

                default:
                    return false;
            }
        }

        /// <summary>
        /// 解析传统位置参数模式的 Action 委托
        /// </summary>
        private void TryResolveLegacyAction(ActionCallPlan call, int i, in ExecCtx<TCtx> ctx)
        {
            switch (call.Arity)
            {
                case 0:
                    if (!ctx.Actions.TryGet<Action0<TArgs, TCtx>>(call.Id, out var a0, out var a0Det))
                        throw new InvalidOperationException($"Action not found or signature mismatch. id={FormatActionId(in ctx, call.Id)} arity=0");
                    if (ctx.Policy.RequireDeterministic && !a0Det)
                        throw new InvalidOperationException($"Non-deterministic action is not allowed by policy. id={FormatActionId(in ctx, call.Id)}");
                    _actions0[i] = a0;
                    break;
                case 1:
                    if (!ctx.Actions.TryGet<Action1<TArgs, TCtx>>(call.Id, out var a1, out var a1Det))
                        throw new InvalidOperationException($"Action not found or signature mismatch. id={FormatActionId(in ctx, call.Id)} arity=1");
                    if (ctx.Policy.RequireDeterministic && !a1Det)
                        throw new InvalidOperationException($"Non-deterministic action is not allowed by policy. id={FormatActionId(in ctx, call.Id)}");
                    _actions1[i] = a1;
                    break;
                case 2:
                    if (!ctx.Actions.TryGet<Action2<TArgs, TCtx>>(call.Id, out var a2, out var a2Det))
                        throw new InvalidOperationException($"Action not found or signature mismatch. id={FormatActionId(in ctx, call.Id)} arity=2");
                    if (ctx.Policy.RequireDeterministic && !a2Det)
                        throw new InvalidOperationException($"Non-deterministic action is not allowed by policy. id={FormatActionId(in ctx, call.Id)}");
                    _actions2[i] = a2;
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported action arity: {call.Arity}");
            }
        }

        /// <summary>
        /// 将具名参数字典解析为可传递给 NamedAction 委托的 rawArgs 对象
        /// </summary>
        private static object ResolveNamedArgs<TArgs, TCtx>(in TArgs args, in ActionCallPlan call, in ExecCtx<TCtx> ctx)
        {
            if (call.Args == null || call.Args.Count == 0)
                return null;

            // 通过 ActionSchemaRegistry 泛型方法解析
            var parsed = ActionSchemaRegistry.GetParsedArgs<TArgs, TCtx>(call.Id, call.Args, ctx);
            if (parsed != null)
                return parsed;

            // 没有 Schema：返回原始字典（供 fallback 处理）
            return new NamedArgsDict(call.Args);
        }

        private bool EvaluateExpr(in TArgs args, in ExecCtx<TCtx> ctx)
        {
            var nodes = _plan.PredicateExpr.Nodes;
            if (nodes == null || nodes.Length == 0) return true;

            // RPN evaluation stack.
            // Use stackalloc for small expressions; ArrayPool fallback for larger ones.
            if (nodes.Length <= 64)
            {
                Span<bool> stack = stackalloc bool[64];
                var sp = 0;
                EvalNodes(nodes, in args, in ctx, ref stack, ref sp);
                if (sp != 1) throw new InvalidOperationException($"Invalid expr stack depth: {sp}");
                return stack[0];
            }

            var rented = ArrayPool<bool>.Shared.Rent(nodes.Length);
            try
            {
                Span<bool> stack = rented;
                var sp = 0;
                EvalNodes(nodes, in args, in ctx, ref stack, ref sp);
                if (sp != 1) throw new InvalidOperationException($"Invalid expr stack depth: {sp}");
                return stack[0];
            }
            finally
            {
                ArrayPool<bool>.Shared.Return(rented);
            }
        }

        private void EvalNodes(BoolExprNode[] nodes, in TArgs args, in ExecCtx<TCtx> ctx, ref Span<bool> stack, ref int sp)
        {
            for (int i = 0; i < nodes.Length; i++)
            {
                var n = nodes[i];
                switch (n.Kind)
                {
                    case EBoolExprNodeKind.Const:
                        stack[sp++] = n.ConstValue;
                        break;
                    case EBoolExprNodeKind.Not:
                        if (sp < 1) throw new InvalidOperationException("Invalid expr: NOT stack underflow");
                        stack[sp - 1] = !stack[sp - 1];
                        break;
                    case EBoolExprNodeKind.And:
                    {
                        if (sp < 2) throw new InvalidOperationException("Invalid expr: AND stack underflow");
                        var b = stack[--sp];
                        var a = stack[--sp];
                        stack[sp++] = a && b;
                        break;
                    }
                    case EBoolExprNodeKind.Or:
                    {
                        if (sp < 2) throw new InvalidOperationException("Invalid expr: OR stack underflow");
                        var b = stack[--sp];
                        var a = stack[--sp];
                        stack[sp++] = a || b;
                        break;
                    }
                    case EBoolExprNodeKind.CompareNumeric:
                    {
                        var left = ResolveNumeric(in args, in n.Left, in ctx);
                        var right = ResolveNumeric(in args, in n.Right, in ctx);
                        stack[sp++] = CompareNumeric(n.CompareOp, left, right);
                        break;
                    }
                    default:
                        throw new InvalidOperationException($"Unsupported expr node kind: {n.Kind}");
                }
            }
        }

        private static bool CompareNumeric(ECompareOp op, double left, double right)
        {
            switch (op)
            {
                case ECompareOp.Equal: return left == right;
                case ECompareOp.NotEqual: return left != right;
                case ECompareOp.GreaterThan: return left > right;
                case ECompareOp.GreaterThanOrEqual: return left >= right;
                case ECompareOp.LessThan: return left < right;
                case ECompareOp.LessThanOrEqual: return left <= right;
                default:
                    throw new InvalidOperationException($"Unsupported compare op: {op}");
            }
        }

        private static double ResolveNumeric(in TArgs args, in NumericValueRef valueRef, in ExecCtx<TCtx> ctx)
        {
            if (valueRef.Kind == ENumericValueRefKind.Const) return valueRef.ConstValue;

            if (valueRef.Kind == ENumericValueRefKind.Blackboard)
            {
                var resolver = ctx.Blackboards;
                if (resolver == null)
                {
                    throw new InvalidOperationException($"Blackboard resolver is null. boardId={FormatBoardId(in ctx, valueRef.BoardId)} keyId={FormatKeyId(in ctx, valueRef.KeyId)}");
                }

                if (!resolver.TryResolve(valueRef.BoardId, out var bb) || bb == null)
                {
                    throw new InvalidOperationException($"Blackboard not found. boardId={FormatBoardId(in ctx, valueRef.BoardId)}");
                }

                if (!bb.TryGetDouble(valueRef.KeyId, out var v))
                {
                    throw new InvalidOperationException($"Blackboard numeric key not found. boardId={FormatBoardId(in ctx, valueRef.BoardId)} keyId={FormatKeyId(in ctx, valueRef.KeyId)}");
                }

                return v;
            }

            if (valueRef.Kind == ENumericValueRefKind.PayloadField)
            {
                // 使用 legacy accessor（强类型访问在 ExecCtx 层面提供）
                var payloads = ctx.Payloads;
                if (payloads == null)
                {
                    throw new InvalidOperationException($"Payload accessor registry is null. fieldId={FormatFieldId(in ctx, valueRef.FieldId)}");
                }

                if (!payloads.TryGetDouble(in args, valueRef.FieldId, out var v))
                {
                    throw new InvalidOperationException($"Payload numeric field not found. fieldId={FormatFieldId(in ctx, valueRef.FieldId)}");
                }

                return v;
            }

            if (valueRef.Kind == ENumericValueRefKind.Var)
            {
                if (string.IsNullOrEmpty(valueRef.DomainId) || string.IsNullOrEmpty(valueRef.Key))
                {
                    throw new InvalidOperationException("Numeric var ref is empty");
                }

                if (!ctx.TryGetNumericVar(valueRef.DomainId, valueRef.Key, out var v))
                {
                    throw new InvalidOperationException($"Numeric var not found. domainId='{valueRef.DomainId}' key='{valueRef.Key}'");
                }

                return v;
            }

            if (valueRef.Kind == ENumericValueRefKind.Expr)
            {
                if (string.IsNullOrEmpty(valueRef.ExprText))
                {
                    throw new InvalidOperationException("Numeric expr text is empty");
                }

                if (!NumericExpressionCompiler.TryCompileCached(valueRef.ExprText, out var program) || program == null)
                {
                    throw new InvalidOperationException("Numeric expr compile failed: " + valueRef.ExprText);
                }

                if (!NumericExpressionEvaluator.TryEvaluate(in ctx, program, out var v))
                {
                    throw new InvalidOperationException("Numeric expr evaluate failed: " + valueRef.ExprText);
                }

                return v;
            }

            throw new InvalidOperationException($"Unsupported NumericValueRef kind: {valueRef.Kind}");
        }

        private static string FormatFunctionId(in ExecCtx<TCtx> ctx, FunctionId id)
        {
            var names = ctx.IdNames;
            if (names != null && names.TryGetFunctionName(id, out var name) && !string.IsNullOrEmpty(name))
                return $"{id.Value}('{name}')";
            return id.Value.ToString();
        }

        private static string FormatActionId(in ExecCtx<TCtx> ctx, ActionId id)
        {
            var names = ctx.IdNames;
            if (names != null && names.TryGetActionName(id, out var name) && !string.IsNullOrEmpty(name))
                return $"{id.Value}('{name}')";
            return id.Value.ToString();
        }

        private static string FormatBoardId(in ExecCtx<TCtx> ctx, int id)
        {
            var names = ctx.IdNames;
            if (names != null && names.TryGetBoardName(id, out var name) && !string.IsNullOrEmpty(name))
                return $"{id}('{name}')";
            return id.ToString();
        }

        private static string FormatKeyId(in ExecCtx<TCtx> ctx, int id)
        {
            var names = ctx.IdNames;
            if (names != null && names.TryGetKeyName(id, out var name) && !string.IsNullOrEmpty(name))
                return $"{id}('{name}')";
            return id.ToString();
        }

        private static string FormatFieldId(in ExecCtx<TCtx> ctx, int id)
        {
            var names = ctx.IdNames;
            if (names != null && names.TryGetFieldName(id, out var name) && !string.IsNullOrEmpty(name))
                return $"{id}('{name}')";
            return id.ToString();
        }
    }
}
