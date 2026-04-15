using System;
using System.Buffers;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Payload;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Variables.Numeric;
using AbilityKit.Triggering.Variables.Numeric.Expression;

namespace AbilityKit.Triggering.Runtime.Plan
{
    public sealed class PlannedTrigger<TArgs, TCtx> : ITrigger<TArgs, TCtx>, ITriggerWithId
    {
        public delegate bool Predicate0(TArgs args, ExecCtx<TCtx> ctx);
        public delegate bool Predicate1(TArgs args, double arg0, ExecCtx<TCtx> ctx);
        public delegate bool Predicate2(TArgs args, double arg0, double arg1, ExecCtx<TCtx> ctx);

        public delegate void Action0(TArgs args, ExecCtx<TCtx> ctx);
        public delegate void Action1(TArgs args, double arg0, ExecCtx<TCtx> ctx);
        public delegate void Action2(TArgs args, double arg0, double arg1, ExecCtx<TCtx> ctx);

        // NamedAction0/1/2 delegates are defined in NamedArgsPlanActionModuleBase.cs
        // to avoid circular dependency

        private readonly TriggerPlan<TArgs> _plan;
        private bool _resolved;

        private Predicate0 _predicate0;
        private Predicate1 _predicate1;
        private Predicate2 _predicate2;

        private Action0[] _actions0;
        private Action1[] _actions1;
        private Action2[] _actions2;

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
        }

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

            if (hasActions)
            {
                for (int i = 0; i < actions.Length; i++)
                {
                    var call = actions[i];

                    if (call.HasNamedArgs && _useNamedArgs[i])
                    {
                        // 具名参数模式：通过 Schema 解析后传给 NamedAction 委托
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
                        // 向后兼容位置参数模式
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

            // 执行成功后：如果声明了 InterruptPriority，自动设置优先级打断
            if (ctx.Control != null && _plan.InterruptPriority > 0)
            {
                ctx.Control.StopBelowPriority(
                    _plan.InterruptPriority,
                    conditionPassed: true,
                    _plan.TriggerId,
                    $"Trigger[{_plan.TriggerId}]"
                );
            }
        }

        private void Resolve(in ExecCtx<TCtx> ctx)
        {
            if (_resolved) return;

            if (_plan.HasPredicate && _plan.PredicateKind == EPredicateKind.Function)
            {
                switch (_plan.PredicateArity)
                {
                    case 0:
                        if (!ctx.Functions.TryGet<Predicate0>(_plan.PredicateId, out var p0, out var p0Det))
                            throw new InvalidOperationException($"Predicate function not found or signature mismatch. id={FormatFunctionId(in ctx, _plan.PredicateId)} arity=0");
                        if (ctx.Policy.RequireDeterministic && !p0Det)
                            throw new InvalidOperationException($"Non-deterministic predicate is not allowed by policy. id={FormatFunctionId(in ctx, _plan.PredicateId)}");
                        _predicate0 = p0;
                        break;
                    case 1:
                        if (!ctx.Functions.TryGet<Predicate1>(_plan.PredicateId, out var p1, out var p1Det))
                            throw new InvalidOperationException($"Predicate function not found or signature mismatch. id={FormatFunctionId(in ctx, _plan.PredicateId)} arity=1");
                        if (ctx.Policy.RequireDeterministic && !p1Det)
                            throw new InvalidOperationException($"Non-deterministic predicate is not allowed by policy. id={FormatFunctionId(in ctx, _plan.PredicateId)}");
                        _predicate1 = p1;
                        break;
                    case 2:
                        if (!ctx.Functions.TryGet<Predicate2>(_plan.PredicateId, out var p2, out var p2Det))
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
                _actions0 = new Action0[len];
                _actions1 = new Action1[len];
                _actions2 = new Action2[len];
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
                    if (!ctx.Actions.TryGet<Action0>(call.Id, out var a0, out var a0Det))
                        throw new InvalidOperationException($"Action not found or signature mismatch. id={FormatActionId(in ctx, call.Id)} arity=0");
                    if (ctx.Policy.RequireDeterministic && !a0Det)
                        throw new InvalidOperationException($"Non-deterministic action is not allowed by policy. id={FormatActionId(in ctx, call.Id)}");
                    _actions0[i] = a0;
                    break;
                case 1:
                    if (!ctx.Actions.TryGet<Action1>(call.Id, out var a1, out var a1Det))
                        throw new InvalidOperationException($"Action not found or signature mismatch. id={FormatActionId(in ctx, call.Id)} arity=1");
                    if (ctx.Policy.RequireDeterministic && !a1Det)
                        throw new InvalidOperationException($"Non-deterministic action is not allowed by policy. id={FormatActionId(in ctx, call.Id)}");
                    _actions1[i] = a1;
                    break;
                case 2:
                    if (!ctx.Actions.TryGet<Action2>(call.Id, out var a2, out var a2Det))
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
