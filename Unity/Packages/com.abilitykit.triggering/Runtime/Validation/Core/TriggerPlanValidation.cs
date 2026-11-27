using System;
using System.Collections.Generic;
using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Triggering.Validation
{
    /// <summary>
    /// TriggerPlan 正式注册边界的统一校验入口。
    /// 检查扁平动作结构，以及批量条目携带的执行根；非空的动作和函数目录另外启用相应的引用检查。
    /// 不执行 UGC 审查、权限校验或运行预算检查。
    /// </summary>
    public static class TriggerPlanValidation
    {
        /// <summary>
        /// 校验单个类型化计划并返回诊断，不注册或执行计划。
        /// </summary>
        public static ValidationResult ValidateRuntimeBoundary<TArgs>(
            EventKey<TArgs> eventKey,
            in TriggerPlan<TArgs> plan,
            string id = null,
            string source = null,
            int lineNumber = 0,
            in ValidationContext<TArgs> context = default)
        {
            var database = new TriggerPlanDatabase<TArgs>(new[]
            {
                new TriggerPlanEntry<TArgs>(eventKey, plan, id, source, lineNumber)
            });
            return RuntimeBoundary<TArgs>.Validate(in database, in context);
        }

        /// <summary>
        /// 校验单个类型化计划，有错误时抛出包含诊断的异常。
        /// </summary>
        public static void ThrowIfInvalidRuntimeBoundary<TArgs>(
            EventKey<TArgs> eventKey,
            in TriggerPlan<TArgs> plan,
            string id = null,
            string source = null,
            int lineNumber = 0,
            in ValidationContext<TArgs> context = default)
        {
            var result = ValidateRuntimeBoundary(eventKey, in plan, id, source, lineNumber, in context);
            result.ThrowIfInvalid($"TriggerPlan runtime boundary validation failed: {id ?? eventKey.IntId.ToString()}");
        }

        /// <summary>
        /// 校验一组计划及其执行根并返回诊断，不注册或执行计划。
        /// </summary>
        public static ValidationResult ValidateRuntimeBoundary(
            IReadOnlyList<TriggerPlanEntry<object>> entries,
            in ValidationContext<object> context = default)
        {
            var database = new TriggerPlanDatabase<object>(entries ?? Array.Empty<TriggerPlanEntry<object>>());
            return RuntimeBoundary<object>.Validate(in database, in context);
        }

        /// <summary>
        /// 校验一组计划，有错误时抛出包含诊断的异常。
        /// </summary>
        public static void ThrowIfInvalidRuntimeBoundary(
            IReadOnlyList<TriggerPlanEntry<object>> entries,
            string sourceName = null,
            in ValidationContext<object> context = default)
        {
            var result = ValidateRuntimeBoundary(entries, in context);
            result.ThrowIfInvalid($"TriggerPlan runtime boundary validation failed: {sourceName ?? "<runtime>"}");
        }

        private static class RuntimeBoundary<TArgs>
        {
            private static readonly CompositeTriggerValidator<TArgs> Validator =
                new CompositeTriggerValidator<TArgs>(new ITriggerValidator<TArgs>[]
                {
                    new ReferenceValidator<TArgs>(),
                    new ActionCallPlanValidator<TArgs>(),
                    new ExecutionRootValidator<TArgs>()
                }, stopOnFirstCriticalError: true);

            internal static ValidationResult Validate(
                in TriggerPlanDatabase<TArgs> database,
                in ValidationContext<TArgs> context) => Validator.Validate(in database, in context);
        }
    }
}
