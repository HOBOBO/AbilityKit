using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Triggering.Validation
{
    /// <summary>
    /// 触发器计划数据库
    /// 包装一组触发器计划，用于校验
    /// </summary>
    public readonly struct TriggerPlanDatabase<TCtx>
    {
        /// <summary>
        /// 所有触发器计划的列表
        /// </summary>
        public readonly TriggerPlanEntry<TCtx>[] Plans;

        /// <summary>
        /// 计划总数
        /// </summary>
        public int Count => Plans?.Length ?? 0;

        public TriggerPlanDatabase(TriggerPlanEntry<TCtx>[] plans)
        {
            Plans = plans;
        }

        public TriggerPlanDatabase(IEnumerable<TriggerPlanEntry<TCtx>> plans)
        {
            Plans = plans?.ToArray();
        }
    }

    /// <summary>
    /// 单个触发器计划条目
    /// 包含事件键、触发器计划和额外元数据
    /// </summary>
    public readonly struct TriggerPlanEntry<TCtx>
    {
        /// <summary>
        /// 计划唯一标识
        /// </summary>
        public readonly string Id;

        /// <summary>
        /// 关联的事件键
        /// </summary>
        public readonly EventKey<TCtx> EventKey;

        /// <summary>
        /// 触发器计划
        /// </summary>
        public readonly TriggerPlan<TCtx> Plan;

        /// <summary>
        /// 计划来源（如文件路径，用于错误报告）
        /// </summary>
        public readonly string Source;

        /// <summary>
        /// 计划在源中的行号
        /// </summary>
        public readonly int LineNumber;

        /// <summary>
        /// 作用域路径（用于层级校验）
        /// </summary>
        public readonly string ScopePath;

        /// <summary>
        /// 额外元数据
        /// </summary>
        public readonly object Metadata;

        public TriggerPlanEntry(
            EventKey<TCtx> eventKey,
            TriggerPlan<TCtx> plan,
            string id = null,
            string source = null,
            int lineNumber = 0,
            string scopePath = null,
            object metadata = null)
        {
            Id = id ?? eventKey.StringId ?? eventKey.IntId.ToString();
            EventKey = eventKey;
            Plan = plan;
            Source = source;
            LineNumber = lineNumber;
            ScopePath = scopePath ?? "/";
            Metadata = metadata;
        }

        /// <summary>
        /// 获取用于错误报告的路径
        /// </summary>
        public string GetPath()
        {
            if (!string.IsNullOrEmpty(Source))
                return $"{Source}:{LineNumber}";
            return $"$.plans[\"{Id}\"]";
        }
    }

    /// <summary>
    /// 触发器计划数据库构建器
    /// </summary>
    public class TriggerPlanDatabaseBuilder<TCtx>
    {
        private readonly List<TriggerPlanEntry<TCtx>> _entries = new List<TriggerPlanEntry<TCtx>>();

        public TriggerPlanDatabaseBuilder<TCtx> Add(
            EventKey<TCtx> eventKey,
            TriggerPlan<TCtx> plan,
            string id = null,
            string source = null,
            int lineNumber = 0,
            string scopePath = null)
        {
            _entries.Add(new TriggerPlanEntry<TCtx>(eventKey, plan, id, source, lineNumber, scopePath));
            return this;
        }

        public TriggerPlanDatabaseBuilder<TCtx> AddRange(IEnumerable<TriggerPlanEntry<TCtx>> entries)
        {
            _entries.AddRange(entries);
            return this;
        }

        public TriggerPlanDatabase<TCtx> Build()
        {
            return new TriggerPlanDatabase<TCtx>(_entries.ToArray());
        }
    }

    /// <summary>
    /// 触发器计划分析工具
    /// 提供通用的计划分析方法
    /// </summary>
    public static class TriggerPlanAnalyzer
    {
        /// <summary>
        /// 计算谓词表达式的嵌套深度
        /// </summary>
        public static int CalculatePredicateDepth(PredicateExprPlan expr)
        {
            if (expr.Nodes == null || expr.Nodes.Length == 0)
                return 0;

            int maxDepth = 0;
            int currentDepth = 0;

            foreach (var node in expr.Nodes)
            {
                switch (node.Kind)
                {
                    case EBoolExprNodeKind.And:
                    case EBoolExprNodeKind.Or:
                        currentDepth++;
                        maxDepth = Math.Max(maxDepth, currentDepth);
                        break;
                    case EBoolExprNodeKind.Not:
                        currentDepth++;
                        maxDepth = Math.Max(maxDepth, currentDepth);
                        currentDepth--;
                        break;
                }
            }

            return maxDepth;
        }

        /// <summary>
        /// 计算表达式中的节点数量
        /// </summary>
        public static int CountPredicateNodes(PredicateExprPlan expr)
        {
            return expr.Nodes?.Length ?? 0;
        }

        /// <summary>
        /// 计算 NumericValueRef 引用的复杂度
        /// </summary>
        public static int CalculateValueRefComplexity(NumericValueRef ref_)
        {
            switch (ref_.Kind)
            {
                case ENumericValueRefKind.Const:
                    return 0;
                case ENumericValueRefKind.Blackboard:
                case ENumericValueRefKind.PayloadField:
                    return 1;
                case ENumericValueRefKind.Var:
                    return 2;
                case ENumericValueRefKind.Expr:
                    return Math.Max(3, (ref_.ExprText?.Length ?? 0) / 10);
                default:
                    return 0;
            }
        }

        /// <summary>
        /// 计算整个触发器计划的复杂度
        /// </summary>
        public static int CalculatePlanComplexity<TCtx>(TriggerPlan<TCtx> plan)
        {
            int complexity = 0;

            if (plan.HasPredicate)
            {
                if (plan.PredicateKind == EPredicateKind.Expr)
                {
                    complexity += CountPredicateNodes(plan.PredicateExpr) * 2;
                    complexity += CalculatePredicateDepth(plan.PredicateExpr) * 3;
                }

                complexity += CalculateValueRefComplexity(plan.PredicateArg0);
                complexity += CalculateValueRefComplexity(plan.PredicateArg1);
            }

            if (plan.Actions != null)
            {
                foreach (var action in plan.Actions)
                {
                    complexity += 1;
                    complexity += CalculateValueRefComplexity(action.Arg0);
                    complexity += CalculateValueRefComplexity(action.Arg1);
                }
            }

            return complexity;
        }

        /// <summary>
        /// 计算计划中的总节点数
        /// </summary>
        public static int CountTotalNodes<TCtx>(TriggerPlan<TCtx> plan)
        {
            int count = 0;

            if (plan.HasPredicate && plan.PredicateKind == EPredicateKind.Expr)
            {
                count += plan.PredicateExpr.Nodes?.Length ?? 0;
            }

            if (plan.Actions != null)
            {
                count += plan.Actions.Length;
            }

            return count;
        }

        /// <summary>
        /// 检查是否为空计划（无谓词、无动作）
        /// </summary>
        public static bool IsEmptyPlan<TCtx>(TriggerPlan<TCtx> plan)
        {
            return !plan.HasPredicate && (plan.Actions == null || plan.Actions.Length == 0);
        }

        /// <summary>
        /// 获取所有引用的 FunctionId
        /// </summary>
        public static IEnumerable<FunctionId> GetReferencedFunctions<TCtx>(TriggerPlan<TCtx> plan)
        {
            if (plan.HasPredicate && plan.PredicateKind == EPredicateKind.Function)
            {
                yield return plan.PredicateId;
            }
        }

        /// <summary>
        /// 获取所有引用的 ActionId
        /// </summary>
        public static IEnumerable<ActionId> GetReferencedActions<TCtx>(TriggerPlan<TCtx> plan)
        {
            if (plan.Actions != null)
            {
                foreach (var action in plan.Actions)
                {
                    yield return action.Id;
                }
            }
        }
    }
}
