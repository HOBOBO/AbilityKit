using System;
using System.Collections.Generic;
using AbilityKit.Core.Logging;
using AbilityKit.Triggering.Validation;

namespace AbilityKit.Triggering.Runtime.Plan.Json
{
    /// <summary>
    /// 将 JSON 数据库的记录投影为计划校验输入的兼容包装器。
    /// </summary>
    /// <remarks>
    /// 不参与目录、聚合与覆盖策略，也不替代宿主在合并后的领域校验。
    /// </remarks>
    public sealed class ValidatingTriggerPlanJsonDatabase
    {
        private readonly TriggerPlanJsonDatabase _inner;
        private readonly TriggerValidationOptions _options;
        private readonly CompositeTriggerValidator<object> _validator;

        /// <summary>
        /// 公开所包装的 JSON 数据库；直接操作它不触发包装器的自动校验。
        /// </summary>
        public TriggerPlanJsonDatabase InnerDatabase => _inner;

        /// <summary>
        /// 按创建时的选项选择开发或 UGC 校验器，不加载计划。
        /// </summary>
        public ValidatingTriggerPlanJsonDatabase(
            TriggerValidationOptions options = null,
            IActionEventMapper eventMapper = null)
        {
            _options = options ?? new TriggerValidationOptions();
            _inner = new TriggerPlanJsonDatabase();

            if (_options.IsUgcMode)
            {
                _validator = CompositeTriggerValidator<object>.CreateForUgcReview(
                    eventMapper,
                    _options.MaxNestingDepth,
                    _options.MaxNodeCount,
                    _options.MaxComplexity,
                    _options.MaxActionCount);
            }
            else
            {
                _validator = CompositeTriggerValidator<object>.CreateForDevelopment(eventMapper);
            }
        }

        /// <summary>
        /// 使用给定的校验器，新建空的内部 JSON 数据库。
        /// </summary>
        public ValidatingTriggerPlanJsonDatabase(
            CompositeTriggerValidator<object> validator,
            TriggerValidationOptions options = null)
        {
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _options = options ?? new TriggerValidationOptions();
            _inner = new TriggerPlanJsonDatabase();
        }

        /// <summary>
        /// 返回所用的选项；可调整自动校验和报告行为，但不会重新创建校验器。
        /// </summary>
        public TriggerValidationOptions Options => _options;

        /// <summary>
        /// 返回当前校验器。
        /// </summary>
        public CompositeTriggerValidator<object> Validator => _validator;

        /// <summary>
        /// 先将 JSON 加载到内部数据库，再按选项运行校验并处理报告。
        /// </summary>
        /// <remarks>
        /// 自动校验异常不回滚已加载的计划；需要在校验完成后才发布数据的调用方应自行安排提交时机。
        /// </remarks>
        public void LoadFromJson(string json, string sourceName = null)
        {
            _inner.LoadFromJson(json, sourceName);

            if (_options.ValidateOnLoad)
            {
                var result = Validate();
                ProcessValidationResult(result, sourceName ?? "<json>");
            }
        }

        /// <summary>
        /// 校验当前记录并返回诊断；不按自动校验选项输出日志或抛出校验失败异常。
        /// </summary>
        public ValidationResult Validate()
        {
            var database = ConvertToValidationDatabase();
            var context = ValidationContext<object>.CreateForUgc();
            return _validator.Validate(in database, in context);
        }

        /// <summary>
        /// 依记录顺序构造校验输入，保留计划和执行根；位置信息使用事件名与记录索引。
        /// </summary>
        public TriggerPlanDatabase<object> ConvertToValidationDatabase()
        {
            var entries = new List<TriggerPlanEntry<object>>();

            for (int i = 0; i < _inner.Records.Count; i++)
            {
                var record = _inner.Records[i];
                var eventKey = new AbilityKit.Core.Eventing.EventKey<object>(record.EventId);
                var entry = new TriggerPlanEntry<object>(
                    eventKey,
                    record.Plan,
                    id: record.TriggerId.ToString(),
                    source: record.EventName,
                    lineNumber: i,
                    scopePath: "/",
                    executionRoot: record.ExecutionRoot);
                entries.Add(entry);
            }

            return new TriggerPlanDatabase<object>(entries);
        }

        /// <summary>
        /// 处理校验结果
        /// </summary>
        private void ProcessValidationResult(ValidationResult result, string sourceName)
        {
            if (_options.LogErrors && !result.IsValid)
            {
                foreach (var error in result.Errors)
                {
                Log.Error($"[TriggerValidation] [{error.Code}] {error.Path}: {string.Format(error.Message, error.Args)}");
                }
            }

            if (_options.LogWarnings && result.Warnings.Count > 0)
            {
                foreach (var warning in result.Warnings)
                {
                Log.Warning($"[TriggerValidation] [{warning.Code}] {warning.Path}: {string.Format(warning.Message, warning.Args)}");
                }
            }

            if (_options.ThrowOnValidationFailure && !result.IsValid)
            {
                result.ThrowIfInvalid($"触发器计划校验失败: {sourceName}");
            }
        }

        /// <summary>
        /// 将 JSON 加载到临时包装器并校验，加载时仍遵循传入的自动校验选项。
        /// </summary>
        public static ValidationResult QuickValidate(
            string json,
            TriggerValidationOptions options = null,
            IActionEventMapper eventMapper = null)
        {
            var database = new ValidatingTriggerPlanJsonDatabase(options, eventMapper);
            database.LoadFromJson(json, "<quick_validate>");
            return database.Validate();
        }

        /// <summary>
        /// 校验 JSON 并将诊断格式化为文本报告，加载或自动校验失败时仍可抛出异常。
        /// </summary>
        public static string QuickValidateAndReport(
            string json,
            TriggerValidationOptions options = null,
            IActionEventMapper eventMapper = null,
            string title = "触发器校验报告")
        {
            var result = QuickValidate(json, options, eventMapper);
            return result.FormatAsReport(title);
        }
    }
}
