using System;
using System.Collections.Generic;
using AbilityKit.Core.Common.Log;
using AbilityKit.Triggering.Validation;

namespace AbilityKit.Triggering.Runtime.Plan.Json
{
    /// <summary>
    /// 触发器校验器选项
    /// </summary>
    public class TriggerValidationOptions
    {
        /// <summary>
        /// 是否在加载时自动校验
        /// </summary>
        public bool ValidateOnLoad { get; set; } = false;

        /// <summary>
        /// 是否在校验失败时抛出异常
        /// </summary>
        public bool ThrowOnValidationFailure { get; set; } = false;

        /// <summary>
        /// 是否输出警告到控制台
        /// </summary>
        public bool LogWarnings { get; set; } = true;

        /// <summary>
        /// 是否输出错误到控制台
        /// </summary>
        public bool LogErrors { get; set; } = true;

        /// <summary>
        /// 最大嵌套深度（UGC 限制）
        /// </summary>
        public int MaxNestingDepth { get; set; } = 10;

        /// <summary>
        /// 最大节点数（UGC 限制）
        /// </summary>
        public int MaxNodeCount { get; set; } = 100;

        /// <summary>
        /// 最大复杂度（UGC 限制）
        /// </summary>
        public int MaxComplexity { get; set; } = 50;

        /// <summary>
        /// 最大 Action 数量（UGC 限制）
        /// </summary>
        public int MaxActionCount { get; set; } = 20;

        /// <summary>
        /// 最大递归深度
        /// </summary>
        public int MaxRecursionDepth { get; set; } = 5;

        /// <summary>
        /// 是否为 UGC 模式（更严格的限制）
        /// </summary>
        public bool IsUgcMode { get; set; } = false;

        /// <summary>
        /// UGC 模式的预设配置
        /// </summary>
        public static TriggerValidationOptions UgcPreset => new TriggerValidationOptions
        {
            ValidateOnLoad = true,
            ThrowOnValidationFailure = true,
            LogWarnings = true,
            LogErrors = true,
            MaxNestingDepth = 5,
            MaxNodeCount = 50,
            MaxComplexity = 30,
            MaxActionCount = 10,
            MaxRecursionDepth = 3,
            IsUgcMode = true
        };

        /// <summary>
        /// 开发模式的预设配置
        /// </summary>
        public static TriggerValidationOptions DevelopmentPreset => new TriggerValidationOptions
        {
            ValidateOnLoad = true,
            ThrowOnValidationFailure = false,
            LogWarnings = true,
            LogErrors = true,
            MaxNestingDepth = 50,
            MaxNodeCount = 500,
            MaxComplexity = 200,
            MaxActionCount = 100,
            MaxRecursionDepth = 10,
            IsUgcMode = false
        };

        /// <summary>
        /// 生产模式的预设配置
        /// </summary>
        public static TriggerValidationOptions ProductionPreset => new TriggerValidationOptions
        {
            ValidateOnLoad = false,
            ThrowOnValidationFailure = false,
            LogWarnings = false,
            LogErrors = true,
            MaxNestingDepth = 20,
            MaxNodeCount = 200,
            MaxComplexity = 100,
            MaxActionCount = 50,
            MaxRecursionDepth = 5,
            IsUgcMode = false
        };
    }

    /// <summary>
    /// 带校验功能的触发器计划 JSON 数据库
    /// 包装 TriggerPlanJsonDatabase 并提供校验功能
    /// </summary>
    public sealed class ValidatingTriggerPlanJsonDatabase
    {
        private readonly TriggerPlanJsonDatabase _inner;
        private readonly TriggerValidationOptions _options;
        private readonly CompositeTriggerValidator<object> _validator;

        /// <summary>
        /// 获取内部的数据库实例
        /// </summary>
        public TriggerPlanJsonDatabase InnerDatabase => _inner;

        public ValidatingTriggerPlanJsonDatabase(
            TriggerValidationOptions options = null,
            IActionEventMapper eventMapper = null)
        {
            _options = options ?? new TriggerValidationOptions();
            _inner = new TriggerPlanJsonDatabase();

            // 根据配置创建校验器
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
        /// 使用自定义校验器
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
        /// 获取校验选项
        /// </summary>
        public TriggerValidationOptions Options => _options;

        /// <summary>
        /// 获取校验器
        /// </summary>
        public CompositeTriggerValidator<object> Validator => _validator;

        /// <summary>
        /// 加载 JSON 数据
        /// </summary>
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
        /// 执行校验
        /// </summary>
        public ValidationResult Validate()
        {
            var database = ConvertToValidationDatabase();
            var context = ValidationContext<object>.CreateForUgc();
            return _validator.Validate(in database, in context);
        }

        /// <summary>
        /// 将当前数据库转换为校验用的格式
        /// </summary>
        public TriggerPlanDatabase<object> ConvertToValidationDatabase()
        {
            var entries = new List<TriggerPlanEntry<object>>();

            for (int i = 0; i < _inner.Records.Count; i++)
            {
                var record = _inner.Records[i];
                var eventKey = new AbilityKit.Core.Common.Event.EventKey<object>(record.EventId);
                var entry = new TriggerPlanEntry<object>(
                    eventKey,
                    record.Plan,
                    id: record.TriggerId.ToString(),
                    source: record.EventName,
                    lineNumber: i,
                    scopePath: "/");
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
        /// 静态方法：快速校验 JSON
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
        /// 静态方法：校验并返回报告字符串
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
