using AbilityKit.Demo.Moba.Diagnostics;

namespace AbilityKit.Game.Editor
{
    internal enum BattleDebugEmptyStateReason
    {
        None = 0,
        SelectionRequired = 1,
        FilteredEmpty = 2,
        Empty = 3,
        NotProduced = 4,
        NotCaptured = 5,
        Evicted = 6,
        Truncated = 7,
        Unsupported = 8,
        Disconnected = 9,
        Error = 10
    }

    internal enum BattleDebugEmptyStateSeverity
    {
        None = 0,
        Info = 1,
        Warning = 2,
        Error = 3
    }

    internal readonly struct BattleDebugEmptyStateProjection
    {
        public BattleDebugEmptyStateProjection(
            BattleDebugEmptyStateReason reason,
            BattleDebugEmptyStateSeverity severity,
            string title,
            string message)
        {
            Reason = reason;
            Severity = severity;
            Title = title ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public BattleDebugEmptyStateReason Reason { get; }
        public BattleDebugEmptyStateSeverity Severity { get; }
        public string Title { get; }
        public string Message { get; }
        public bool HasValue => Reason != BattleDebugEmptyStateReason.None;
    }

    internal static class BattleDebugEmptyStateProjector
    {
        public static BattleDebugEmptyStateProjection Project(
            in BattleDiagnosticQueryStatus status,
            bool requiresSelection = false,
            bool hasSelection = true,
            bool hasActiveFilter = false,
            string subject = "数据",
            string selectionSubject = "Actor")
        {
            subject = string.IsNullOrEmpty(subject) ? "数据" : subject;
            selectionSubject = string.IsNullOrEmpty(selectionSubject)
                ? "对象"
                : selectionSubject;
            if (requiresSelection && !hasSelection)
            {
                return Create(
                    BattleDebugEmptyStateReason.SelectionRequired,
                    BattleDebugEmptyStateSeverity.Info,
                    $"需要选择 {selectionSubject}",
                    $"选择一个 {selectionSubject} 后可查看对应的{subject}。");
            }

            if (status.CanDisplayResults)
            {
                return default;
            }

            if (status.Phase == BattleDiagnosticQueryPhase.Empty)
            {
                return hasActiveFilter
                    ? Create(
                        BattleDebugEmptyStateReason.FilteredEmpty,
                        BattleDebugEmptyStateSeverity.Info,
                        "当前筛选无结果",
                        $"{subject}已产生，但当前过滤条件没有匹配项。")
                    : Create(
                        BattleDebugEmptyStateReason.Empty,
                        BattleDebugEmptyStateSeverity.Info,
                        $"暂无{subject}",
                        $"当前会话尚无可显示的{subject}。");
            }

            var detail = string.IsNullOrEmpty(status.Message)
                ? string.Empty
                : $" {status.Message}";
            switch (status.Availability)
            {
                case BattleDiagnosticDataAvailability.NotProduced:
                    return Create(
                        BattleDebugEmptyStateReason.NotProduced,
                        BattleDebugEmptyStateSeverity.Info,
                        $"{subject}尚未产生",
                        $"运行对应战斗流程后再检查。{detail}".Trim());
                case BattleDiagnosticDataAvailability.NotCaptured:
                    return Create(
                        BattleDebugEmptyStateReason.NotCaptured,
                        BattleDebugEmptyStateSeverity.Warning,
                        $"{subject}未被捕获",
                        $"检查诊断能力和捕获通道配置。{detail}".Trim());
                case BattleDiagnosticDataAvailability.Evicted:
                    return Create(
                        BattleDebugEmptyStateReason.Evicted,
                        BattleDebugEmptyStateSeverity.Warning,
                        $"{subject}已被淘汰",
                        $"缩小时间范围或提高 Store 容量。{detail}".Trim());
                case BattleDiagnosticDataAvailability.Truncated:
                    return Create(
                        BattleDebugEmptyStateReason.Truncated,
                        BattleDebugEmptyStateSeverity.Warning,
                        $"{subject}不完整",
                        $"当前仅保留部分结果。{detail}".Trim());
                case BattleDiagnosticDataAvailability.Unsupported:
                    return Create(
                        BattleDebugEmptyStateReason.Unsupported,
                        BattleDebugEmptyStateSeverity.Info,
                        $"不支持{subject}",
                        $"当前会话未声明对应诊断能力。{detail}".Trim());
                case BattleDiagnosticDataAvailability.Disconnected:
                    return Create(
                        BattleDebugEmptyStateReason.Disconnected,
                        BattleDebugEmptyStateSeverity.Warning,
                        "诊断会话已断开",
                        $"重新进入战斗或打开诊断 Artifact。{detail}".Trim());
                case BattleDiagnosticDataAvailability.Error:
                    return Create(
                        BattleDebugEmptyStateReason.Error,
                        BattleDebugEmptyStateSeverity.Error,
                        $"{subject}查询失败",
                        string.IsNullOrEmpty(status.ErrorCode)
                            ? detail.Trim()
                            : $"[{status.ErrorCode}]{detail}");
                default:
                    return Create(
                        BattleDebugEmptyStateReason.Empty,
                        BattleDebugEmptyStateSeverity.Info,
                        $"暂无{subject}",
                        detail.Trim());
            }
        }

        private static BattleDebugEmptyStateProjection Create(
            BattleDebugEmptyStateReason reason,
            BattleDebugEmptyStateSeverity severity,
            string title,
            string message)
        {
            return new BattleDebugEmptyStateProjection(reason, severity, title, message);
        }
    }
}
