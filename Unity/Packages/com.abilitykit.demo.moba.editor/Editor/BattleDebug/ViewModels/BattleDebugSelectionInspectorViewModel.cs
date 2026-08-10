using System;
using AbilityKit.Demo.Moba.Diagnostics;

namespace AbilityKit.Game.Editor
{
    internal sealed class BattleDebugSelectionInspectorViewModel
    {
        internal const int MaximumEventPages = 4;

        private const int EventPageSize = BattleDiagnosticPageRequest.MaximumPageSize;

        private BattleDiagnosticSelection _selection;
        private BattleDiagnosticSessionScope _sessionScope;
        private long _storeRevision = -1;
        private long _requestId;
        private bool _hasCachedResult;

        private BattleDebugConfigReference _configReference;
        private BattleDiagnosticSelection _configSourceSelection;
        private bool _hasConfigSelection;
        private bool _hasCachedConfig;

        public BattleDiagnosticSelection Selection => _selection;
        public BattleDiagnosticActorSummary? Actor { get; private set; }
        public BattleDiagnosticEvent? Event { get; private set; }
        public BattleDiagnosticTraceNodeSummary? TraceNode { get; private set; }
        public BattleDebugConfigReference ConfigReference => _configReference;
        public BattleDebugConfigSourceLocation? ConfigLocation { get; private set; }
        public bool HasConfigSelection => _hasConfigSelection;
        public BattleDiagnosticQueryStatus QueryStatus { get; private set; }
        public string StatusMessage { get; private set; } = string.Empty;
        public string ConfigStatusMessage { get; private set; } = string.Empty;
        public int EventPagesScanned { get; private set; }

        public void InvalidateCache()
        {
            _selection = default;
            _sessionScope = default;
            _storeRevision = -1;
            _hasCachedResult = false;
            _hasCachedConfig = false;
            ClearProjection();
            ConfigLocation = null;
            ConfigStatusMessage = string.Empty;
        }

        public void SelectConfig(
            in BattleDebugConfigReference reference,
            in BattleDiagnosticSelection sourceSelection)
        {
            if (!reference.IsValid)
            {
                throw new ArgumentException("A valid configuration reference is required.", nameof(reference));
            }

            if (_hasConfigSelection &&
                _configReference.Equals(reference) &&
                _configSourceSelection.Equals(sourceSelection))
            {
                return;
            }

            _configReference = reference;
            _configSourceSelection = sourceSelection;
            _hasConfigSelection = true;
            _hasCachedConfig = false;
            ConfigLocation = null;
            ConfigStatusMessage = string.Empty;
        }

        public bool RefreshConfigIfActive(in BattleDiagnosticSelection currentSelection)
        {
            if (!_hasConfigSelection)
            {
                return false;
            }

            if (_configSourceSelection.IsValid && !_configSourceSelection.Equals(currentSelection))
            {
                ClearConfigSelection();
                return false;
            }

            if (_hasCachedConfig)
            {
                return true;
            }

            _hasCachedConfig = true;
            if (BattleDebugConfigSourceIndex.TryLocate(
                    in _configReference,
                    out var location,
                    out var error))
            {
                ConfigLocation = location;
                ConfigStatusMessage = string.Empty;
            }
            else
            {
                ConfigLocation = null;
                ConfigStatusMessage = error;
            }

            return true;
        }

        public void InvalidateConfigCache()
        {
            _hasCachedConfig = false;
            ConfigLocation = null;
            ConfigStatusMessage = string.Empty;
        }

        private void ClearConfigSelection()
        {
            _configReference = default;
            _configSourceSelection = default;
            _hasConfigSelection = false;
            _hasCachedConfig = false;
            ConfigLocation = null;
            ConfigStatusMessage = string.Empty;
        }

        public void RefreshIfNeeded(
            IBattleDiagnosticReadOnlySession session,
            in BattleDiagnosticSelection selection)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));

            var sessionScope = session.SessionInfo.Scope;
            var revision = ResolveStoreRevision(session, selection.Kind);
            if (_hasCachedResult &&
                _selection.Equals(selection) &&
                _sessionScope.Equals(sessionScope) &&
                _storeRevision == revision)
            {
                return;
            }

            _selection = selection;
            _sessionScope = sessionScope;
            _storeRevision = revision;
            _hasCachedResult = true;
            ClearProjection();

            if (!selection.IsValid)
            {
                StatusMessage = "尚未选择可检查的对象。";
                return;
            }

            if (!selection.BelongsTo(sessionScope))
            {
                StatusMessage = "当前选择不属于已连接的诊断会话。";
                return;
            }

            switch (selection.Kind)
            {
                case BattleDiagnosticSelectionKind.Actor:
                    RefreshActor(session, selection);
                    break;
                case BattleDiagnosticSelectionKind.Event:
                    RefreshEvent(session, selection);
                    break;
                case BattleDiagnosticSelectionKind.TraceRoot:
                case BattleDiagnosticSelectionKind.TraceNode:
                    RefreshTrace(session, selection);
                    break;
                default:
                    StatusMessage = $"当前 Inspector 尚不支持 {selection.Kind} 选择。";
                    break;
            }
        }

        private void RefreshActor(
            IBattleDiagnosticReadOnlySession session,
            in BattleDiagnosticSelection selection)
        {
            var frame = BattleDiagnosticFrames.IsValid(selection.Frame) ? selection.Frame : 0;
            var result = session.QueryActors(NextRequestId(), frame);
            QueryStatus = result.Status;

            for (var i = 0; i < result.Items.Count; i++)
            {
                if (result.Items[i].ActorId != selection.Id) continue;
                Actor = result.Items[i];
                return;
            }

            StatusMessage = BuildMissingMessage("Actor", selection.Id, result.Status);
        }

        private void RefreshEvent(
            IBattleDiagnosticReadOnlySession session,
            in BattleDiagnosticSelection selection)
        {
            var frames = BattleDiagnosticFrames.IsValid(selection.Frame)
                ? new BattleDiagnosticFrameFilter(selection.Frame, selection.Frame)
                : default;
            var filter = new BattleDiagnosticFilter(frames, BattleDiagnosticEventChannel.All);
            var page = new BattleDiagnosticPageRequest(
                session.EventStoreRevision,
                0,
                EventPageSize);

            for (var pageIndex = 0; pageIndex < MaximumEventPages; pageIndex++)
            {
                var result = session.QueryEvents(new BattleDiagnosticEventQuery(
                    NextRequestId(),
                    filter,
                    page,
                    newestFirst: true));
                EventPagesScanned++;
                QueryStatus = result.Status;

                for (var i = 0; i < result.Items.Count; i++)
                {
                    if (result.Items[i].Sequence != selection.Id) continue;
                    Event = result.Items[i];
                    return;
                }

                if (!result.Status.HasMore)
                {
                    StatusMessage = BuildMissingMessage("Event", selection.Id, result.Status);
                    return;
                }

                page = page.NextPage();
            }

            QueryStatus = BattleDiagnosticQueryStatus.Partial(
                QueryStatus.RequestId,
                session.EventStoreRevision,
                0,
                BattleDiagnosticDataAvailability.Truncated,
                $"Inspector lookup is limited to {MaximumEventPages * EventPageSize} events.");
            StatusMessage = $"未在最近 {MaximumEventPages * EventPageSize} 条同帧事件中找到 Event {selection.Id}。";
        }

        private void RefreshTrace(
            IBattleDiagnosticReadOnlySession session,
            in BattleDiagnosticSelection selection)
        {
            var rootContextId = selection.RelatedId != 0 ? selection.RelatedId : selection.Id;
            var result = session.QueryTrace(NextRequestId(), rootContextId);
            QueryStatus = result.Status;

            for (var i = 0; i < result.Items.Count; i++)
            {
                if (result.Items[i].ContextId != selection.Id) continue;
                TraceNode = result.Items[i];
                return;
            }

            StatusMessage = BuildMissingMessage("Trace Context", selection.Id, result.Status);
        }

        private void ClearProjection()
        {
            Actor = null;
            Event = null;
            TraceNode = null;
            QueryStatus = default;
            StatusMessage = string.Empty;
            EventPagesScanned = 0;
        }

        private long NextRequestId()
        {
            _requestId++;
            if (_requestId <= 0) _requestId = 1;
            return _requestId;
        }

        private static long ResolveStoreRevision(
            IBattleDiagnosticReadOnlySession session,
            BattleDiagnosticSelectionKind kind)
        {
            switch (kind)
            {
                case BattleDiagnosticSelectionKind.Actor:
                    return session.StateStoreRevision;
                case BattleDiagnosticSelectionKind.Event:
                    return session.EventStoreRevision;
                case BattleDiagnosticSelectionKind.TraceRoot:
                case BattleDiagnosticSelectionKind.TraceNode:
                    return session.TraceStoreRevision;
                default:
                    return -1;
            }
        }

        private static string BuildMissingMessage(
            string label,
            long id,
            BattleDiagnosticQueryStatus status)
        {
            if (status.Phase == BattleDiagnosticQueryPhase.Unavailable ||
                status.Phase == BattleDiagnosticQueryPhase.Error ||
                status.Phase == BattleDiagnosticQueryPhase.Partial)
            {
                var detail = string.IsNullOrEmpty(status.Message)
                    ? status.Availability.ToString()
                    : $"{status.Availability} {status.Message}";
                return $"{label} 数据不可用：{detail}";
            }

            return $"当前数据集中未找到 {label} {id}。";
        }
    }
}
