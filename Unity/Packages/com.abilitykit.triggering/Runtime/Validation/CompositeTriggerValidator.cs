using System;
using System.Collections.Generic;
using System.Linq;

namespace AbilityKit.Triggering.Validation
{
    /// <summary>
    /// 组合校验器
    /// 将多个校验器组合在一起，按优先级顺序执行
    /// </summary>
    public sealed class CompositeTriggerValidator<TCtx> : ITriggerValidator<TCtx>
    {
        private readonly List<ITriggerValidator<TCtx>> _validators;
        private readonly bool _stopOnFirstCriticalError;

        public string Name => "组合校验";
        public int Priority => int.MaxValue;
        public bool IsCritical => false;

        /// <summary>
        /// 所有子校验器（按优先级排序）
        /// </summary>
        public IReadOnlyList<ITriggerValidator<TCtx>> Validators => _validators;

        /// <summary>
        /// 是否在遇到第一个关键错误时停止
        /// </summary>
        public bool StopOnFirstCriticalError => _stopOnFirstCriticalError;

        public CompositeTriggerValidator(
            IEnumerable<ITriggerValidator<TCtx>> validators = null,
            bool stopOnFirstCriticalError = true)
        {
            _validators = validators?.OrderBy(v => v.Priority).ToList() ?? new List<ITriggerValidator<TCtx>>();
            _stopOnFirstCriticalError = stopOnFirstCriticalError;
        }

        /// <summary>
        /// 添加校验器
        /// </summary>
        public CompositeTriggerValidator<TCtx> Add(ITriggerValidator<TCtx> validator)
        {
            if (validator != null)
            {
                _validators.Add(validator);
                _validators.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            }
            return this;
        }

        /// <summary>
        /// 移除校验器
        /// </summary>
        public CompositeTriggerValidator<TCtx> Remove(string validatorName)
        {
            _validators.RemoveAll(v => v.Name == validatorName);
            return this;
        }

        /// <summary>
        /// 清空所有校验器
        /// </summary>
        public CompositeTriggerValidator<TCtx> Clear()
        {
            _validators.Clear();
            return this;
        }

        public ValidationResult Validate(in TriggerPlanDatabase<TCtx> database, in ValidationContext<TCtx> context)
        {
            var combinedResult = new ValidationResult();

            foreach (var validator in _validators)
            {
                try
                {
                    var result = validator.Validate(in database, in context);
                    combinedResult.Merge(in result);

                    if (_stopOnFirstCriticalError && validator.IsCritical && !result.IsValid)
                    {
                        combinedResult.AddInfo(
                            "VALIDATION_STOPPED",
                            $"校验在 '{validator.Name}' 失败后停止",
                            "$");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    combinedResult.AddError(
                        "VALIDATOR_EXCEPTION",
                        $"校验器 '{validator.Name}' 执行时发生异常: {ex.Message}",
                        "$");
                }
            }

            return combinedResult;
        }

        /// <summary>
        /// 创建一个包含所有核心校验器的组合校验器（开发环境）
        /// </summary>
        public static CompositeTriggerValidator<TCtx> CreateForDevelopment(
            IActionEventMapper eventMapper = null)
        {
            return new CompositeTriggerValidator<TCtx>(new ITriggerValidator<TCtx>[]
            {
                new CycleDetectorValidator<TCtx>(maxRecursionDepth: 10),
                new SelfTriggerValidator<TCtx>(eventMapper),
                new CrossScopeCycleValidator<TCtx>(),
                new ReferenceValidator<TCtx>(),
                new UgcLimitsValidator<TCtx>(
                    maxNestingDepth: 50,
                    maxNodeCount: 500,
                    maxComplexity: 200,
                    maxActionCount: 100)
            });
        }

        /// <summary>
        /// 创建一个用于 UGC 审核的组合校验器（严格模式）
        /// </summary>
        public static CompositeTriggerValidator<TCtx> CreateForUgcReview(
            IActionEventMapper eventMapper = null,
            int maxNestingDepth = 5,
            int maxNodeCount = 50,
            int maxComplexity = 30,
            int maxActionCount = 10)
        {
            return new CompositeTriggerValidator<TCtx>(new ITriggerValidator<TCtx>[]
            {
                new CycleDetectorValidator<TCtx>(maxRecursionDepth: 5),
                new SelfTriggerValidator<TCtx>(eventMapper),
                new ReferenceValidator<TCtx>(),
                new UgcLimitsValidator<TCtx>(
                    maxNestingDepth: maxNestingDepth,
                    maxNodeCount: maxNodeCount,
                    maxComplexity: maxComplexity,
                    maxActionCount: maxActionCount),
                new SemanticValidator<TCtx>(requireDeterministic: false),
                new DeadCodeValidator<TCtx>()
            });
        }

        /// <summary>
        /// 创建一个最小校验器（仅关键检查）
        /// </summary>
        public static CompositeTriggerValidator<TCtx> CreateMinimal()
        {
            return new CompositeTriggerValidator<TCtx>(new ITriggerValidator<TCtx>[]
            {
                new CycleDetectorValidator<TCtx>(),
                new ReferenceValidator<TCtx>()
            }, stopOnFirstCriticalError: true);
        }

        /// <summary>
        /// 创建一个仅包含循环检测的校验器
        /// </summary>
        public static CompositeTriggerValidator<TCtx> CreateCycleDetectionOnly(
            IActionEventMapper eventMapper = null)
        {
            return new CompositeTriggerValidator<TCtx>(new ITriggerValidator<TCtx>[]
            {
                new CycleDetectorValidator<TCtx>(),
                new SelfTriggerValidator<TCtx>(eventMapper)
            });
        }
    }

    /// <summary>
    /// 校验结果扩展方法
    /// </summary>
    public static class ValidationResultExtensions
    {
        /// <summary>
        /// 生成友好的错误报告
        /// </summary>
        public static string FormatAsReport(this in ValidationResult result, string title = "触发器校验报告")
        {
            if (result.IsValid && result.Warnings.Count == 0)
            {
                return $"{title}: 通过";
            }

            var lines = new List<string>
            {
                $"=== {title} ===",
                result.IsValid ? "状态: 通过" : "状态: 失败",
                ""
            };

            if (!result.IsValid)
            {
                lines.Add($"错误 ({result.Errors.Count}):");
                foreach (var error in result.Errors)
                {
                    lines.Add($"  [{error.Code}] {error.Path}");
                    lines.Add($"    {string.Format(error.Message, error.Args)}");
                }
                lines.Add("");
            }

            if (result.Warnings.Count > 0)
            {
                lines.Add($"警告 ({result.Warnings.Count}):");
                foreach (var warning in result.Warnings)
                {
                    lines.Add($"  [{warning.Code}] {warning.Path}");
                    lines.Add($"    {string.Format(warning.Message, warning.Args)}");
                }
                lines.Add("");
            }

            if (result.Infos.Count > 0)
            {
                lines.Add($"提示 ({result.Infos.Count}):");
                foreach (var info in result.Infos)
                {
                    lines.Add($"  [{info.Code}] {info.Path}");
                    lines.Add($"    {string.Format(info.Message, info.Args)}");
                }
            }

            return string.Join("\n", lines);
        }

        /// <summary>
        /// 抛出校验异常（如果校验失败）
        /// </summary>
        public static void ThrowIfInvalid(this in ValidationResult result, string message = null)
        {
            if (!result.IsValid)
            {
                var report = result.FormatAsReport(message ?? "校验失败");
                throw new TriggerValidationException(report, result);
            }
        }
    }

    /// <summary>
    /// 触发器校验异常
    /// </summary>
    public class TriggerValidationException : Exception
    {
        public ValidationResult ValidationResult { get; }

        public TriggerValidationException(string message, ValidationResult result)
            : base(message)
        {
            ValidationResult = result;
        }

        public TriggerValidationException(string message, ValidationResult result, Exception innerException)
            : base(message, innerException)
        {
            ValidationResult = result;
        }
    }
}
