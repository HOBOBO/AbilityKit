using System;
using System.Collections.Generic;
using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Triggering.Validation
{
    /// <summary>
    /// TriggerPlan 正式注册边界的统一校验入口。
    /// 默认使用空引用集合，只执行可在计划结构内确定的语义检查；需要强引用校验时可传入完整 ValidationContext。
    /// </summary>
    public static class TriggerPlanValidation
    {
        private static readonly CompositeTriggerValidator<object> ObjectRuntimeBoundaryValidator = CreateRuntimeBoundaryValidator<object>();

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
            var validationContext = NormalizeContext(in context);
            return CreateRuntimeBoundaryValidator<TArgs>().Validate(in database, in validationContext);
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
        /// 校验一组计划及其批内约束并返回诊断，不注册或执行计划。
        /// </summary>
        public static ValidationResult ValidateRuntimeBoundary(
            IReadOnlyList<TriggerPlanEntry<object>> entries,
            in ValidationContext<object> context = default)
        {
            var database = new TriggerPlanDatabase<object>(entries ?? Array.Empty<TriggerPlanEntry<object>>());
            var validationContext = NormalizeContext(in context);
            return ObjectRuntimeBoundaryValidator.Validate(in database, in validationContext);
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

        private static CompositeTriggerValidator<TArgs> CreateRuntimeBoundaryValidator<TArgs>()
        {
            return new CompositeTriggerValidator<TArgs>(new ITriggerValidator<TArgs>[]
            {
                new ReferenceValidator<TArgs>(),
                new ActionCallPlanValidator<TArgs>(),
                new ExecutionRootValidator<TArgs>()
            }, stopOnFirstCriticalError: true);
        }

        private static ValidationContext<TArgs> NormalizeContext<TArgs>(in ValidationContext<TArgs> context)
        {
            return new ValidationContext<TArgs>(
                context.DefinedFunctionIds,
                context.DefinedActionIds,
                context.DefinedEventKeys,
                context.MaxNestingDepth > 0 ? context.MaxNestingDepth : 50,
                context.MaxNodeCount > 0 ? context.MaxNodeCount : 500,
                context.MaxComplexity > 0 ? context.MaxComplexity : 200,
                context.MaxRecursionDepth > 0 ? context.MaxRecursionDepth : 10,
                context.MaxActionCount > 0 ? context.MaxActionCount : 100,
                context.StrictMode);
        }
    }
}
