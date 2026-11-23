using System.Collections.Generic;
using AbilityKit.Triggering.Runtime.Plan.Json;
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
        /// JSON 加载诊断。
        /// </summary>
        JsonLoad = 1,
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
        public readonly string Source;
        /// <summary>
        /// 关联的配置路径或兼容入口；未提供时为空。
        /// </summary>
        public readonly string Path;
        /// <summary>
        /// 诊断代码或严重程度文本；未提供时为空。
        /// </summary>
        public readonly string Code;
        /// <summary>
        /// 调用方提供的描述；未提供时为空。
        /// </summary>
        public readonly string Message;
        /// <summary>
        /// 该记录携带的错误数，不代表快照的 JSON 分级计数。
        /// </summary>
        public readonly int ErrorCount;
        /// <summary>
        /// 该记录携带的警告数，不代表快照的 JSON 分级计数。
        /// </summary>
        public readonly int WarningCount;
        /// <summary>
        /// 该记录携带的信息数，不代表快照的 JSON 分级计数。
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
        public readonly string ScheduleHandle;

        /// <summary>
        /// 用给定诊断信息构造记录，不更新收集器计数。
        /// </summary>
        public TriggeringDiagnosticRecord(
            TriggeringDiagnosticEventKind kind,
            string source = null,
            string path = null,
            string code = null,
            string message = null,
            int errorCount = 0,
            int warningCount = 0,
            int infoCount = 0,
            int triggerId = 0,
            int actionId = 0,
            string scheduleHandle = null)
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
        /// 累计的 JSON 错误数。
        /// </summary>
        public readonly int JsonErrors;
        /// <summary>
        /// 累计的 JSON 警告数。
        /// </summary>
        public readonly int JsonWarnings;
        /// <summary>
        /// 累计的其他 JSON 诊断数。
        /// </summary>
        public readonly int JsonInfos;
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
            int jsonErrors,
            int jsonWarnings,
            int jsonInfos,
            int executionFailures,
            int scheduleEvents,
            int legacyHits,
            int totalRecords)
        {
            ValidationErrors = validationErrors;
            ValidationWarnings = validationWarnings;
            ValidationInfos = validationInfos;
            JsonErrors = jsonErrors;
            JsonWarnings = jsonWarnings;
            JsonInfos = jsonInfos;
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
        private int _jsonErrors;
        private int _jsonWarnings;
        private int _jsonInfos;
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
            _jsonErrors,
            _jsonWarnings,
            _jsonInfos,
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
            _jsonErrors = 0;
            _jsonWarnings = 0;
            _jsonInfos = 0;
            _executionFailures = 0;
            _scheduleEvents = 0;
            _legacyHits = 0;
        }

        /// <summary>
        /// 累计校验问题数并追加一条汇总记录。
        /// </summary>
        public void RecordValidation(in ValidationResult result, string source = null)
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
        /// 逐条追加 JSON 诊断并按严重程度计数；输入为空时不记录。
        /// </summary>
        public void RecordJsonDiagnostics(IEnumerable<TriggerPlanJsonDiagnostic> diagnostics)
        {
            if (diagnostics == null)
                return;

            foreach (var diagnostic in diagnostics)
            {
                switch (diagnostic.Severity)
                {
                    case TriggerPlanJsonDiagnosticSeverity.Error:
                        _jsonErrors++;
                        break;
                    case TriggerPlanJsonDiagnosticSeverity.Warning:
                        _jsonWarnings++;
                        break;
                    default:
                        _jsonInfos++;
                        break;
                }

                _records.Add(new TriggeringDiagnosticRecord(
                    TriggeringDiagnosticEventKind.JsonLoad,
                    diagnostic.SourceName,
                    diagnostic.Path,
                    diagnostic.Severity.ToString(),
                    diagnostic.Message));
            }
        }

        /// <summary>
        /// 追加一次执行失败，保留调用方提供的动作和触发器编号。
        /// </summary>
        public void RecordExecutionFailure(int triggerId, int actionId, string message, string path = null)
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
        public void RecordScheduleEvent(string scheduleHandle, string message = null, int triggerId = 0, int actionId = 0)
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
        public void RecordLegacyHit(string entry, string replacement, string caller = null)
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
