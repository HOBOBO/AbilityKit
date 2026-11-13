using System;
using System.Buffers;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Dispatcher;
using AbilityKit.Triggering.Variables.Numeric.Expression;
using ECompareOp = AbilityKit.Triggering.Runtime.Config.ECompareOp;

namespace AbilityKit.Triggering.Runtime.Plan
{
    /// <summary>
    /// PlannedTrigger 的谓词评估辅助，统一处理函数谓词和表达式谓词。
    /// </summary>
    internal static class PlannedTriggerPredicateEvaluator<TArgs, TCtx>
    {
        public static bool Evaluate(in TriggerPlan<TArgs> plan, in TArgs args, in ExecCtx<TCtx> ctx)
        {
            if (plan.PredicateKind == EPredicateKind.None || !plan.HasPredicate)
            {
                return true;
            }

            if (plan.PredicateKind == EPredicateKind.Expr)
            {
                return EvaluateExpr(in plan, in args, in ctx);
            }

            if (plan.PredicateKind != EPredicateKind.Function)
            {
                throw new InvalidOperationException($"Unsupported predicate kind: {plan.PredicateKind}");
            }

            return EvaluateFunction(in plan, in args, in ctx);
        }

        public static TriggerPredicate<object> CreateConditionDelegate(in TriggerPlan<TArgs> plan, in ExecCtx<TCtx> ctx)
        {
            if (plan.PredicateKind == EPredicateKind.None || !plan.HasPredicate)
            {
                return null;
            }

            var capturedPlan = plan;
            var capturedCtx = ctx;

            return (argsObj, _) =>
            {
                var args = (TArgs)argsObj;
                return Evaluate(in capturedPlan, in args, in capturedCtx);
            };
        }

        private static bool EvaluateFunction(in TriggerPlan<TArgs> plan, in TArgs args, in ExecCtx<TCtx> ctx)
        {
            switch (plan.PredicateArity)
            {
                case 0:
                    if (ctx.Functions.TryGet<Predicate0<TArgs, TCtx>>(plan.PredicateId, out var p0, out var p0Det))
                    {
                        if (ctx.Policy.RequireDeterministic && !p0Det)
                        {
                            throw new InvalidOperationException($"Non-deterministic predicate is not allowed by policy. id={FormatFunctionId(in ctx, plan.PredicateId)}");
                        }

                        return p0?.Invoke(args, ctx) ?? true;
                    }

                    throw new InvalidOperationException($"Predicate function not found. id={FormatFunctionId(in ctx, plan.PredicateId)} arity=0");

                case 1:
                {
                    var v0 = ResolveNumeric(in args, in plan.PredicateArg0, in ctx);
                    var argsDict = PlannedTriggerArgumentResolver<TArgs, TCtx>.CreatePositionalArgs(v0);
                    if (TryInvokePredicate1(plan.PredicateId, in args, argsDict, in ctx, out var result))
                    {
                        return result;
                    }

                    throw new InvalidOperationException($"Predicate function not found. id={FormatFunctionId(in ctx, plan.PredicateId)} arity=1");
                }

                case 2:
                {
                    var v0 = ResolveNumeric(in args, in plan.PredicateArg0, in ctx);
                    var v1 = ResolveNumeric(in args, in plan.PredicateArg1, in ctx);
                    var argsDict = PlannedTriggerArgumentResolver<TArgs, TCtx>.CreatePositionalArgs(v0, v1);
                    if (TryInvokePredicate2(plan.PredicateId, in args, argsDict, in ctx, out var result))
                    {
                        return result;
                    }

                    throw new InvalidOperationException($"Predicate function not found. id={FormatFunctionId(in ctx, plan.PredicateId)} arity=2");
                }

                default:
                    throw new InvalidOperationException($"Unsupported predicate arity: {plan.PredicateArity}");
            }
        }

        private static bool EvaluateExpr(in TriggerPlan<TArgs> plan, in TArgs args, in ExecCtx<TCtx> ctx)
        {
            var nodes = plan.PredicateExpr.Nodes;
            if (nodes == null || nodes.Length == 0)
            {
                return true;
            }

            if (nodes.Length <= 64)
            {
                Span<bool> stack = stackalloc bool[64];
                var sp = 0;
                EvalNodes(nodes, in args, in ctx, ref stack, ref sp);
                if (sp != 1)
                {
                    throw new InvalidOperationException($"Invalid expr stack depth: {sp}");
                }

                return stack[0];
            }

            var rented = ArrayPool<bool>.Shared.Rent(nodes.Length);
            try
            {
                Span<bool> stack = rented;
                var sp = 0;
                EvalNodes(nodes, in args, in ctx, ref stack, ref sp);
                if (sp != 1)
                {
                    throw new InvalidOperationException($"Invalid expr stack depth: {sp}");
                }

                return stack[0];
            }
            finally
            {
                ArrayPool<bool>.Shared.Return(rented);
            }
        }

        private static void EvalNodes(BoolExprNode[] nodes, in TArgs args, in ExecCtx<TCtx> ctx, ref Span<bool> stack, ref int sp)
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
                        var cmpResult = CompareNumeric(n.CompareOp, left, right);
                        stack[sp++] = cmpResult;
                        break;
                    }
                    case EBoolExprNodeKind.Function:
                    {
                        var funcResult = EvaluateExprFunction(in n, in args, in ctx);
                        stack[sp++] = funcResult;
                        break;
                    }
                    default:
                        throw new InvalidOperationException($"Unsupported expr node kind: {n.Kind}");
                }
            }
        }

        private static bool EvaluateExprFunction(in BoolExprNode node, in TArgs args, in ExecCtx<TCtx> ctx)
        {
            switch (node.FunctionArity)
            {
                case 0:
                    if (ctx.Functions.TryGet<Predicate0<TArgs, TCtx>>(node.FunctionId, out var p0, out var p0Det))
                    {
                        if (ctx.Policy.RequireDeterministic && !p0Det)
                        {
                            throw new InvalidOperationException($"Non-deterministic predicate is not allowed by policy. id={FormatFunctionId(in ctx, node.FunctionId)}");
                        }

                        return p0?.Invoke(args, ctx) ?? true;
                    }

                    throw new InvalidOperationException($"Predicate function not found. id={FormatFunctionId(in ctx, node.FunctionId)} arity=0");

                case 1:
                {
                    var v0 = ResolveNumeric(in args, in node.Left, in ctx);
                    var argsDict = PlannedTriggerArgumentResolver<TArgs, TCtx>.CreatePositionalArgs(v0);
                    if (TryInvokePredicate1(node.FunctionId, in args, argsDict, in ctx, out var result))
                    {
                        return result;
                    }

                    throw new InvalidOperationException($"Predicate function not found. id={FormatFunctionId(in ctx, node.FunctionId)} arity=1");
                }

                case 2:
                {
                    var v0 = ResolveNumeric(in args, in node.Left, in ctx);
                    var v1 = ResolveNumeric(in args, in node.Right, in ctx);
                    var argsDict = PlannedTriggerArgumentResolver<TArgs, TCtx>.CreatePositionalArgs(v0, v1);
                    if (TryInvokePredicate2(node.FunctionId, in args, argsDict, in ctx, out var result))
                    {
                        return result;
                    }

                    throw new InvalidOperationException($"Predicate function not found. id={FormatFunctionId(in ctx, node.FunctionId)} arity=2");
                }

                default:
                    throw new InvalidOperationException($"Unsupported predicate arity: {node.FunctionArity}");
            }
        }

        private static bool TryInvokePredicate1(FunctionId functionId, in TArgs args, NamedArgsDict argsDict, in ExecCtx<TCtx> ctx, out bool result)
        {
            if (ctx.Functions.TryGet<Predicate1<TArgs, TCtx>>(functionId, out var predicate, out var deterministic))
            {
                EnsureDeterministic(functionId, deterministic, in ctx);
                result = predicate?.Invoke(args, argsDict, ctx) ?? true;
                return true;
            }

            if (ctx.Functions.TryGet<Predicate1<object, TCtx>>(functionId, out var objectPredicate, out deterministic))
            {
                EnsureDeterministic(functionId, deterministic, in ctx);
                result = objectPredicate?.Invoke(args, argsDict, ctx) ?? true;
                return true;
            }

            result = false;
            return false;
        }

        private static bool TryInvokePredicate2(FunctionId functionId, in TArgs args, NamedArgsDict argsDict, in ExecCtx<TCtx> ctx, out bool result)
        {
            if (ctx.Functions.TryGet<Predicate2<TArgs, TCtx>>(functionId, out var predicate, out var deterministic))
            {
                EnsureDeterministic(functionId, deterministic, in ctx);
                result = predicate?.Invoke(args, argsDict, ctx) ?? true;
                return true;
            }

            if (ctx.Functions.TryGet<Predicate2<object, TCtx>>(functionId, out var objectPredicate, out deterministic))
            {
                EnsureDeterministic(functionId, deterministic, in ctx);
                result = objectPredicate?.Invoke(args, argsDict, ctx) ?? true;
                return true;
            }

            result = false;
            return false;
        }

        private static void EnsureDeterministic(FunctionId functionId, bool deterministic, in ExecCtx<TCtx> ctx)
        {
            if (ctx.Policy.RequireDeterministic && !deterministic)
            {
                throw new InvalidOperationException($"Non-deterministic predicate is not allowed by policy. id={FormatFunctionId(in ctx, functionId)}");
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
            return PlannedTriggerArgumentResolver<TArgs, TCtx>.ResolveNumeric(in args, in valueRef, in ctx);
        }

        private static string FormatFunctionId(in ExecCtx<TCtx> ctx, FunctionId id)
        {
            return PlannedTriggerArgumentResolver<TArgs, TCtx>.FormatFunctionId(in ctx, id);
        }
    }
}
