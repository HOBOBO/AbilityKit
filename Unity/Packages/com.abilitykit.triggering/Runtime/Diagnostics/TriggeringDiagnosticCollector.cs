#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Validation;

namespace AbilityKit.Triggering.Runtime.Diagnostics
{
    /// <summary>
    /// 区分收集器接收的诊断类别。
    /// </summary>
    public enum TriggeringDiagnosticEventKind
    {
        /// <summary>
        /// 计划语义校验。
        /// </summary>
        Validation = 0,
        /// <summary>
        /// 计划数据的加载诊断，不限于某种输入格式。
        /// </summary>
        PlanLoad = 1,
        /// <summary>
        /// 调用方报告的执行失败。
        /// </summary>
        Execution = 2,
        /// <summary>
        /// 调用方报告的调度事件。
        /// </summary>
        Schedule = 3,
        /// <summary>
        /// 调用方报告的兼容入口命中。
        /// </summary>
        LegacyHit = 4
    }

    /// <summary>
    /// 计划加载诊断的严重程度，由各输入格式映射到此分类。
    /// </summary>
    public enum TriggeringLoadDiagnosticSeverity
    {
        /// <summary>
        /// 提示信息。
        /// </summary>
        Info = 0,
        /// <summary>
        /// 警告。
        /// </summary>
        Warning = 1,
        /// <summary>
        /// 错误。
        /// </summary>
        Error = 2
    }

    /// <summary>
    /// 保存一条诊断事件的来源、定位信息和关联标识。
    /// </summary>
    public readonly struct TriggeringDiagnosticRecord
    {
        /// <summary>
        /// 诊断事件类别。
        /// </summary>
        public readonly TriggeringDiagnosticEventKind Kind;
        /// <summary>
        /// 诊断来源；未提供时为空。
        /// </summary>
        public readonly string? Source;
        /// <summary>
        /// 关联的配置路径或兼容入口；未提供时为空。
        /// </summary>
        public readonly string? Path;
        /// <summary>
        /// 诊断代码或严重程度文本；未提供时为空。
        /// </summary>
        public readonly string? Code;
        /// <summary>
        /// 调用方提供的描述；未提供时为空。
        /// </summary>
        public readonly string? Message;
        /// <summary>
        /// 该记录携带的错误数，不是累计计数。
        /// </summary>
        public readonly int ErrorCount;
        /// <summary>
        /// 该记录携带的警告数，不是累计计数。
        /// </summary>
        public readonly int WarningCount;
        /// <summary>
        /// 该记录携带的信息数，不是累计计数。
        /// </summary>
        public readonly int InfoCount;
        /// <summary>
        /// 关联的触发器编号；收集器缺省为零。
        /// </summary>
        public readonly int TriggerId;
        /// <summary>
        /// 关联的动作编号；收集器缺省为零。
        /// </summary>
        public readonly int ActionId;
        /// <summary>
        /// 调用方提供的调度句柄文本；未提供时为空。
        /// </summary>
        public readonly string? ScheduleHandle;

        /// <summary>
        /// 用给定诊断信息构造记录，不更新收集器计数。
        /// </summary>
        public TriggeringDiagnosticRecord(
            TriggeringDiagnosticEventKind kind,
            string? source = null,
            string? path = null,
            string? code = null,
            string? message = null,
            int errorCount = 0,
            int warningCount = 0,
            int infoCount = 0,
            int triggerId = 0,
            int actionId = 0,
            string? scheduleHandle = null)
        {
            Kind = kind;
            Source = source;
            Path = path;
            Code = code;
            Message = message;
            ErrorCount = errorCount;
            WarningCount = warningCount;
            InfoCount = infoCount;
            TriggerId = triggerId;
            ActionId = actionId;
            ScheduleHandle = scheduleHandle;
        }
    }

    /// <summary>
    /// 保存读取时的累计计数，后续记录或清空不会改变该值。
    /// </summary>
    public readonly struct TriggeringDiagnosticSnapshot
    {
        /// <summary>
        /// 累计的计划校验错误数。
        /// </summary>
        public readonly int ValidationErrors;
        /// <summary>
        /// 累计的计划校验警告数。
        /// </summary>
        public readonly int ValidationWarnings;
        /// <summary>
        /// 累计的计划校验信息数。
        /// </summary>
        public readonly int ValidationInfos;
        /// <summary>
        /// 累计的计划加载错误数。
        /// </summary>
        public readonly int LoadErrors;
        /// <summary>
        /// 累计的计划加载警告数。
        /// </summary>
        public readonly int LoadWarnings;
        /// <summary>
        /// 累计的计划加载提示数。
        /// </summary>
        public readonly int LoadInfos;
        /// <summary>
        /// 调用方报告的执行失败次数。
        /// </summary>
        public readonly int ExecutionFailures;
        /// <summary>
        /// 调用方报告的调度事件次数。
        /// </summary>
        public readonly int ScheduleEvents;
        /// <summary>
        /// 调用方报告的兼容入口命中次数。
        /// </summary>
        public readonly int LegacyHits;
        /// <summary>
        /// 生成快照时的诊断记录条数。
        /// </summary>
        public readonly int TotalRecords;

        /// <summary>
        /// 用指定计数构造快照。
        /// </summary>
        public TriggeringDiagnosticSnapshot(
            int validationErrors,
            int validationWarnings,
            int validationInfos,
            int loadErrors,
            int loadWarnings,
            int loadInfos,
            int executionFailures,
            int scheduleEvents,
            int legacyHits,
            int totalRecords)
        {
            ValidationErrors = validationErrors;
            ValidationWarnings = validationWarnings;
            ValidationInfos = validationInfos;
            LoadErrors = loadErrors;
            LoadWarnings = loadWarnings;
            LoadInfos = loadInfos;
            ExecutionFailures = executionFailures;
            ScheduleEvents = scheduleEvents;
            LegacyHits = legacyHits;
            TotalRecords = totalRecords;
        }
    }

    /// <summary>
    /// 按调用顺序收集显式报告的诊断，不自动订阅运行链。实例不提供并发访问保护。
    /// </summary>
    public sealed class TriggeringDiagnosticCollector
    {
        private readonly List<TriggeringDiagnosticRecord> _records = new List<TriggeringDiagnosticRecord>();
        private int _validationErrors;
        private int _validationWarnings;
        private int _validationInfos;
        private int _loadErrors;
        private int _loadWarnings;
        private int _loadInfos;
        private int _executionFailures;
        private int _scheduleEvents;
        private int _legacyHits;

        /// <summary>
        /// 返回活动列表的只读接口视图；后续记录或清空会反映在该视图中。
        /// </summary>
        public IReadOnlyList<TriggeringDiagnosticRecord> Records => _records;

        /// <summary>
        /// 按当前累计值创建计数快照。
        /// </summary>
        public TriggeringDiagnosticSnapshot Snapshot => new TriggeringDiagnosticSnapshot(
            _validationErrors,
            _validationWarnings,
            _validationInfos,
            _loadErrors,
            _loadWarnings,
            _loadInfos,
            _executionFailures,
            _scheduleEvents,
            _legacyHits,
            _records.Count);

        /// <summary>
        /// 清除当前记录并将累计计数归零。
        /// </summary>
        public void Clear()
        {
            _records.Clear();
            _validationErrors = 0;
            _validationWarnings = 0;
            _validationInfos = 0;
            _loadErrors = 0;
            _loadWarnings = 0;
            _loadInfos = 0;
            _executionFailures = 0;
            _scheduleEvents = 0;
            _legacyHits = 0;
        }

        /// <summary>
        /// 累计校验问题数并追加一条汇总记录。
        /// </summary>
        public void RecordValidation(in ValidationResult result, string? source = null)
        {
            _validationErrors += result.Errors.Count;
            _validationWarnings += result.Warnings.Count;
            _validationInfos += result.Infos.Count;
            _records.Add(new TriggeringDiagnosticRecord(
                TriggeringDiagnosticEventKind.Validation,
                source,
                errorCount: result.Errors.Count,
                warningCount: result.Warnings.Count,
                infoCount: result.Infos.Count));
        }

        /// <summary>
        /// 追加一条计划加载诊断并计数；不执行加载，也不修改加载策略。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// 严重程度不在支持的分类内；不追加记录或更新计数。
        /// </exception>
        public void RecordLoadDiagnostic(
            TriggeringLoadDiagnosticSeverity severity,
            string? message,
            string? source = null,
            string? path = null,
            string? code = null)
        {
            switch (severity)
            {
                case TriggeringLoadDiagnosticSeverity.Error:
                    _loadErrors++;
                    break;
                case TriggeringLoadDiagnosticSeverity.Warning:
                    _loadWarnings++;
                    break;
                case TriggeringLoadDiagnosticSeverity.Info:
                    _loadInfos++;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(severity), severity, null);
            }

            _records.Add(new TriggeringDiagnosticRecord(
                TriggeringDiagnosticEventKind.PlanLoad,
                source,
                path,
                code ?? severity.ToString(),
                message,
                errorCount: severity == TriggeringLoadDiagnosticSeverity.Error ? 1 : 0,
                warningCount: severity == TriggeringLoadDiagnosticSeverity.Warning ? 1 : 0,
                infoCount: severity == TriggeringLoadDiagnosticSeverity.Info ? 1 : 0));
        }

        /// <summary>
        /// 追加一次执行失败，保留调用方提供的动作和触发器编号。
        /// </summary>
        public void RecordExecutionFailure(int triggerId, int actionId, string? message, string? path = null)
        {
            _executionFailures++;
            _records.Add(new TriggeringDiagnosticRecord(
                TriggeringDiagnosticEventKind.Execution,
                path: path,
                message: message,
                triggerId: triggerId,
                actionId: actionId,
                errorCount: 1));
        }

        /// <summary>
        /// 记录调度事件，不创建或推进调度任务。
        /// </summary>
        public void RecordScheduleEvent(string? scheduleHandle, string? message = null, int triggerId = 0, int actionId = 0)
        {
            _scheduleEvents++;
            _records.Add(new TriggeringDiagnosticRecord(
                TriggeringDiagnosticEventKind.Schedule,
                message: message,
                triggerId: triggerId,
                actionId: actionId,
                scheduleHandle: scheduleHandle));
        }

        /// <summary>
        /// 记录调用方报告的兼容入口及替代路径，不执行替换。
        /// </summary>
        public void RecordLegacyHit(string? entry, string? replacement, string? caller = null)
        {
            _legacyHits++;
            _records.Add(new TriggeringDiagnosticRecord(
                TriggeringDiagnosticEventKind.LegacyHit,
                source: caller,
                path: entry,
                message: replacement,
                warningCount: 1));
        }
    }
}
