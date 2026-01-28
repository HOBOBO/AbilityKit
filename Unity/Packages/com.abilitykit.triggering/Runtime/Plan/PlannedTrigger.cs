using System;
using System.Buffers;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Payload;

namespace AbilityKit.Triggering.Runtime.Plan
{
    public sealed class PlannedTrigger<TArgs, TCtx> : ITrigger<TArgs, TCtx>
    {
        public delegate bool Predicate0(TArgs args, ExecCtx<TCtx> ctx);
        public delegate bool Predicate1(TArgs args, int arg0, ExecCtx<TCtx> ctx);
        public delegate bool Predicate2(TArgs args, int arg0, int arg1, ExecCtx<TCtx> ctx);

        public delegate void Action0(TArgs args, ExecCtx<TCtx> ctx);
        public delegate void Action1(TArgs args, int arg0, ExecCtx<TCtx> ctx);
        public delegate void Action2(TArgs args, int arg0, int arg1, ExecCtx<TCtx> ctx);

        private readonly TriggerPlan<TArgs> _plan;
        private bool _resolved;

        private Predicate0 _predicate0;
        private Predicate1 _predicate1;
        private Predicate2 _predicate2;

        private Action0[] _actions0;
        private Action1[] _actions1;
        private Action2[] _actions2;

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
                    return _predicate1(args, ResolveInt(in args, in _plan.PredicateArg0, in ctx), ctx);
                case 2:
                    return _predicate2(args, ResolveInt(in args, in _plan.PredicateArg0, in ctx), ResolveInt(in args, in _plan.PredicateArg1, in ctx), ctx);
                default:
                    throw new InvalidOperationException($"Unsupported predicate arity: {_plan.PredicateArity}");
            }
        }

        public void Execute(in TArgs args, in ExecCtx<TCtx> ctx)
        {
            Resolve(ctx);
            var actions = _plan.Actions;
            var hasStrong = actions != null && actions.Length > 0;

            if (hasStrong)
            {
                for (int i = 0; i < actions.Length; i++)
                {
                    var call = actions[i];
                    switch (call.Arity)
                    {
                        case 0:
                            _actions0[i](args, ctx);
                            break;
                        case 1:
                            _actions1[i](args, ResolveInt(in args, in call.Arg0, in ctx), ctx);
                            break;
                        case 2:
                            _actions2[i](args, ResolveInt(in args, in call.Arg0, in ctx), ResolveInt(in args, in call.Arg1, in ctx), ctx);
                            break;
                        default:
                            throw new InvalidOperationException($"Unsupported action arity: {call.Arity}");
                    }

                    if (ctx.Control != null && (ctx.Control.StopPropagation || ctx.Control.Cancel)) return;
                }
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

                for (int i = 0; i < len; i++)
                {
                    var call = _plan.Actions[i];
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
            }

            _resolved = true;
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
                    case EBoolExprNodeKind.CompareInt:
                    {
                        var left = ResolveInt(in args, in n.Left, in ctx);
                        var right = ResolveInt(in args, in n.Right, in ctx);
                        stack[sp++] = CompareInt(n.CompareOp, left, right);
                        break;
                    }
                    default:
                        throw new InvalidOperationException($"Unsupported expr node kind: {n.Kind}");
                }
            }
        }

        private static bool CompareInt(ECompareOp op, int left, int right)
        {
            switch (op)
            {
                case ECompareOp.Eq: return left == right;
                case ECompareOp.Ne: return left != right;
                case ECompareOp.Gt: return left > right;
                case ECompareOp.Ge: return left >= right;
                case ECompareOp.Lt: return left < right;
                case ECompareOp.Le: return left <= right;
                default:
                    throw new InvalidOperationException($"Unsupported compare op: {op}");
            }
        }

        private static int ResolveInt(in TArgs args, in IntValueRef valueRef, in ExecCtx<TCtx> ctx)
        {
            if (valueRef.Kind == EIntValueRefKind.Const) return valueRef.ConstValue;

            if (valueRef.Kind == EIntValueRefKind.Blackboard)
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

                if (!bb.TryGetInt(valueRef.KeyId, out var v))
                {
                    throw new InvalidOperationException($"Blackboard int key not found. boardId={FormatBoardId(in ctx, valueRef.BoardId)} keyId={FormatKeyId(in ctx, valueRef.KeyId)}");
                }

                return v;
            }

            if (valueRef.Kind == EIntValueRefKind.PayloadField)
            {
                var payloads = ctx.Payloads;
                if (payloads == null)
                {
                    throw new InvalidOperationException($"Payload accessor registry is null. fieldId={FormatFieldId(in ctx, valueRef.FieldId)}");
                }

                if (!payloads.TryGetInt(in args, valueRef.FieldId, out var v))
                {
                    throw new InvalidOperationException($"Payload int field not found. fieldId={FormatFieldId(in ctx, valueRef.FieldId)}");
                }

                return v;
            }

            throw new InvalidOperationException($"Unsupported IntValueRef kind: {valueRef.Kind}");
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
