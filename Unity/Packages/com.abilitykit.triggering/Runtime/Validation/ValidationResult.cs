using System;
using System.Collections.Generic;

namespace AbilityKit.Triggering.Validation
{
    /// <summary>
    /// 验证错误级别
    /// </summary>
    public enum ValidationSeverity
    {
        Error = 0,
        Warning = 1,
        Info = 2
    }

    /// <summary>
    /// 单个验证错误或警告
    /// </summary>
    public readonly struct ValidationIssue
    {
        public string Code { get; }
        public string Message { get; }
        public string Path { get; }
        public ValidationSeverity Severity { get; }
        public object[] Args { get; }

        public ValidationIssue(string code, string message, string path, ValidationSeverity severity, params object[] args)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Message = message ?? throw new ArgumentNullException(nameof(message));
            Path = path ?? "";
            Severity = severity;
            Args = args;
        }

        public static ValidationIssue Error(string code, string message, string path, params object[] args)
            => new ValidationIssue(code, message, path, ValidationSeverity.Error, args);

        public static ValidationIssue Warning(string code, string message, string path, params object[] args)
            => new ValidationIssue(code, message, path, ValidationSeverity.Warning, args);

        public static ValidationIssue Info(string code, string message, string path, params object[] args)
            => new ValidationIssue(code, message, path, ValidationSeverity.Info, args);

        public override string ToString()
        {
            var prefix = Severity switch
            {
                ValidationSeverity.Error => "[Error]",
                ValidationSeverity.Warning => "[Warning]",
                ValidationSeverity.Info => "[Info]",
                _ => "[Unknown]"
            };
            return $"{prefix} [{Code}] {Path}: {string.Format(Message, Args)}";
        }
    }

    /// <summary>
    /// 验证结果
    /// </summary>
    public struct ValidationResult
    {
        private List<ValidationIssue> _errors;
        private List<ValidationIssue> _warnings;
        private List<ValidationIssue> _infos;

        public bool IsValid => _errors == null || _errors.Count == 0;

        public IReadOnlyList<ValidationIssue> Errors => _errors ?? EmptyList;
        public IReadOnlyList<ValidationIssue> Warnings => _warnings ?? EmptyList;
        public IReadOnlyList<ValidationIssue> Infos => _infos ?? EmptyList;

        private static readonly List<ValidationIssue> EmptyList = new List<ValidationIssue>();

        public static ValidationResult Success => default;

        public void AddError(string code, string message, string path, params object[] args)
        {
            _errors ??= new List<ValidationIssue>();
            _errors.Add(ValidationIssue.Error(code, message, path, args));
        }

        public void AddWarning(string code, string message, string path, params object[] args)
        {
            _warnings ??= new List<ValidationIssue>();
            _warnings.Add(ValidationIssue.Warning(code, message, path, args));
        }

        public void AddInfo(string code, string message, string path, params object[] args)
        {
            _infos ??= new List<ValidationIssue>();
            _infos.Add(ValidationIssue.Info(code, message, path, args));
        }

        public void AddIssue(ValidationIssue issue)
        {
            switch (issue.Severity)
            {
                case ValidationSeverity.Error:
                    _errors ??= new List<ValidationIssue>();
                    _errors.Add(issue);
                    break;
                case ValidationSeverity.Warning:
                    _warnings ??= new List<ValidationIssue>();
                    _warnings.Add(issue);
                    break;
                case ValidationSeverity.Info:
                    _infos ??= new List<ValidationIssue>();
                    _infos.Add(issue);
                    break;
            }
        }

        /// <summary>
        /// 合并另一个验证结果
        /// </summary>
        public void Merge(in ValidationResult other)
        {
            if (other._errors != null)
            {
                _errors ??= new List<ValidationIssue>();
                _errors.AddRange(other._errors);
            }
            if (other._warnings != null)
            {
                _warnings ??= new List<ValidationIssue>();
                _warnings.AddRange(other._warnings);
            }
            if (other._infos != null)
            {
                _infos ??= new List<ValidationIssue>();
                _infos.AddRange(other._infos);
            }
        }

        /// <summary>
        /// 创建合并后的新结果
        /// </summary>
        public static ValidationResult Combine(in ValidationResult a, in ValidationResult b)
        {
            var result = a;
            result.Merge(in b);
            return result;
        }

        public override string ToString()
        {
            if (IsValid && (Warnings == null || Warnings.Count == 0))
                return "ValidationResult: Valid";

            var lines = new List<string>();
            foreach (var e in Errors)
                lines.Add(e.ToString());
            foreach (var w in Warnings)
                lines.Add(w.ToString());

            return $"ValidationResult: {Errors.Count} errors, {Warnings.Count} warnings\n" + string.Join("\n", lines);
        }
    }

    /// <summary>
    /// 验证错误码常量
    /// </summary>
    public static class ValidationErrorCodes
    {
        // 引用类错误
        public const string FUNCTION_NOT_FOUND = "FUNCTION_NOT_FOUND";
        public const string ACTION_NOT_FOUND = "ACTION_NOT_FOUND";
        public const string EVENT_KEY_NOT_FOUND = "EVENT_KEY_NOT_FOUND";
        public const string BLACKBOARD_DOMAIN_NOT_FOUND = "BLACKBOARD_DOMAIN_NOT_FOUND";
        public const string PAYLOAD_FIELD_NOT_FOUND = "PAYLOAD_FIELD_NOT_FOUND";
        public const string NUMERIC_VAR_NOT_FOUND = "NUMERIC_VAR_NOT_FOUND";
        public const string NUMERIC_FUNCTION_NOT_FOUND = "NUMERIC_FUNCTION_NOT_FOUND";

        // 循环与依赖类错误
        public const string CYCLE_DETECTED = "CYCLE_DETECTED";
        public const string SELF_TRIGGER = "SELF_TRIGGER";
        public const string CROSS_SCOPE_CYCLE = "CROSS_SCOPE_CYCLE";

        // UGC 限制类错误
        public const string EXCEEDS_NESTING_DEPTH = "EXCEEDS_NESTING_DEPTH";
        public const string EXCEEDS_NODE_COUNT = "EXCEEDS_NODE_COUNT";
        public const string EXCEEDS_COMPLEXITY = "EXCEEDS_COMPLEXITY";
        public const string EXCEEDS_ACTION_COUNT = "EXCEEDS_ACTION_COUNT";
        public const string EXCEEDS_RECURSION_DEPTH = "EXCEEDS_RECURSION_DEPTH";

        // 语义类错误
        public const string DETERMINISM_MISMATCH = "DETERMINISM_MISMATCH";
        public const string VOID_PREDICATE_RESULT = "VOID_PREDICATE_RESULT";
        public const string EMPTY_ACTION_LIST = "EMPTY_ACTION_LIST";
        public const string INVALID_PHASE_ORDER = "INVALID_PHASE_ORDER";
        public const string INVALID_EXPRESSION = "INVALID_EXPRESSION";

        // 警告
        public const string HIGH_COMPLEXITY = "HIGH_COMPLEXITY";
        public const string DEAD_CODE_BRANCH = "DEAD_CODE_BRANCH";
        public const string UNUSED_VARIABLE = "UNUSED_VARIABLE";
        public const string COMPLEX_EXPRESSION = "COMPLEX_EXPRESSION";
    }
}
