using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Diagnostics;

namespace AbilityKit.Game.Editor
{
    internal sealed class BattleDebugRuntimeObjectsViewModel
    {
        internal const int PageSize = 200;
        internal const int RelatedEventScanPageSize = BattleDiagnosticPageRequest.MaximumPageSize;
        internal const int RelatedEventScanPageLimit = 10;

        private IBattleDiagnosticReadOnlySession _lastSession;
        private BattleDiagnosticSessionScope _lastScope;
        private long _lastStoreRevision = -1L;
        private BattleDiagnosticRuntimeObjectFilter _lastFilter;
        private long _worksetRevision = -1L;
        private int _nextPageOffset;
        private long _nextRequestId;
        private IReadOnlyList<BattleDiagnosticRuntimeObject> _items;

        public BattleDiagnosticRuntimeObjectKind Kind { get; set; }
        public BattleDiagnosticRuntimeObjectState State { get; set; }
        public BattleDiagnosticDataCompleteness Completeness { get; set; }

        public BattleDiagnosticQueryStatus QueryStatus { get; private set; }
        public BattleDiagnosticQueryStatus SummaryQueryStatus { get; private set; }
        public BattleDiagnosticRuntimeObjectCatalogSummary? Summary { get; private set; }
        public IReadOnlyList<BattleDiagnosticRuntimeObject> Items =>
            _items ?? Array.Empty<BattleDiagnosticRuntimeObject>();
        public BattleDiagnosticRuntimeObject? Selected { get; private set; }
        public int SelectedIndex => FindSelectedIndex();
        public bool HasMore { get; private set; }
        public int LoadedCount => Items.Count;
        public long StoreRevision => _lastStoreRevision;
        public long WorksetRevision => _worksetRevision;
        public string StatusMessage { get; private set; } = string.Empty;
        public string PagingStatusMessage { get; private set; } = string.Empty;
        public string RelatedEventStatusMessage { get; private set; } = string.Empty;
        public bool IsSupported =>
            QueryStatus.Availability != BattleDiagnosticDataAvailability.Unsupported;

        public void Invalidate()
        {
            _lastSession = null;
            _lastScope = default;
            _lastStoreRevision = -1L;
            _worksetRevision = -1L;
            _nextPageOffset = 0;
            _items = null;
            Summary = null;
            QueryStatus = default;
            SummaryQueryStatus = default;
            HasMore = false;
            StatusMessage = string.Empty;
            PagingStatusMessage = string.Empty;
            RelatedEventStatusMessage = string.Empty;
        }

        public IReadOnlyList<BattleDiagnosticRuntimeObject> RefreshIfNeeded(
            IBattleDiagnosticReadOnlySession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));

            if (!(session is IBattleDiagnosticRuntimeObjectCatalogSession catalog))
            {
                SetUnsupported(session);
                return Items;
            }

            var scope = session.SessionInfo.Scope;
            var revision = catalog.RuntimeObjectStoreRevision;
            var filter = CurrentFilter();
            if (_items != null &&
                ReferenceEquals(_lastSession, session) &&
                _lastScope.Equals(scope) &&
                _lastStoreRevision == revision &&
                _lastFilter.Equals(filter))
            {
                return _items;
            }

            var summaryResult = catalog.QueryRuntimeObjectSummary(NextRequestId());
            var query = new BattleDiagnosticRuntimeObjectQuery(
                NextRequestId(),
                filter,
                new BattleDiagnosticPageRequest(revision, 0, PageSize));
            var result = catalog.QueryRuntimeObjects(query);

            _lastSession = session;
            _lastScope = scope;
            _lastStoreRevision = revision;
            _lastFilter = filter;
            _worksetRevision = result.Status.StoreRevision;
            _nextPageOffset = PageSize;
            QueryStatus = result.Status;
            SummaryQueryStatus = summaryResult.Status;
            Summary = summaryResult.Status.CanDisplayResults && summaryResult.Items.Count > 0
                ? summaryResult.Items[0]
                : (BattleDiagnosticRuntimeObjectCatalogSummary?)null;
            _items = CanRetainItems(result.Status)
                ? result.Items
                : Array.Empty<BattleDiagnosticRuntimeObject>();
            HasMore = result.Status.HasMore;
            PagingStatusMessage = string.Empty;
            StatusMessage = BuildStatusMessage(result.Status);
            ReconcileSelection();
            return _items;
        }

        public bool LoadMore(IBattleDiagnosticReadOnlySession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (!HasMore || !(session is IBattleDiagnosticRuntimeObjectCatalogSession catalog))
                return false;

            if (!ReferenceEquals(_lastSession, session) ||
                !_lastScope.Equals(session.SessionInfo.Scope) ||
                catalog.RuntimeObjectStoreRevision != _worksetRevision)
            {
                Invalidate();
                RefreshIfNeeded(session);
                StatusMessage = "运行时对象目录已变化，列表已刷新。";
                return false;
            }

            var query = new BattleDiagnosticRuntimeObjectQuery(
                NextRequestId(),
                CurrentFilter(),
                new BattleDiagnosticPageRequest(_worksetRevision, _nextPageOffset, PageSize));
            var result = catalog.QueryRuntimeObjects(query);
            QueryStatus = result.Status;
            if (!CanRetainItems(result.Status))
            {
                HasMore = false;
                PagingStatusMessage = BuildStatusMessage(result.Status);
                return false;
            }

            var combined = new List<BattleDiagnosticRuntimeObject>(Items.Count + result.Items.Count);
            for (var i = 0; i < Items.Count; i++) combined.Add(Items[i]);
            for (var i = 0; i < result.Items.Count; i++) combined.Add(result.Items[i]);
            _items = combined;
            _nextPageOffset += PageSize;
            HasMore = result.Status.HasMore;
            PagingStatusMessage = HasMore
                ? $"已加载 {_items.Count} 个对象，还有更多。"
                : $"已加载全部 {_items.Count} 个匹配对象。";
            ReconcileSelection();
            return true;
        }

        public void Select(in BattleDiagnosticRuntimeObject runtimeObject)
        {
            Selected = runtimeObject;
            RelatedEventStatusMessage = string.Empty;
        }

        public void ClearSelection()
        {
            Selected = null;
            RelatedEventStatusMessage = string.Empty;
        }

        public bool SelectAdjacent(int offset)
        {
            if (offset == 0 || Items.Count == 0) return false;

            var index = FindSelectedIndex();
            if (index < 0)
            {
                index = offset > 0 ? 0 : Items.Count - 1;
            }
            else
            {
                index = Math.Max(0, Math.Min(Items.Count - 1, index + offset));
            }

            if (Selected.HasValue && index == FindSelectedIndex()) return false;
            var item = Items[index];
            Select(in item);
            return true;
        }

        public bool TryFindRelatedEvent(
            IBattleDiagnosticReadOnlySession session,
            in BattleDiagnosticRuntimeObject runtimeObject,
            out BattleDiagnosticEvent diagnosticEvent)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));

            diagnosticEvent = default;
            var revision = session.EventStoreRevision;
            var offset = 0;
            var scanned = 0;
            var found = false;
            var exhausted = false;

            for (var pageIndex = 0; pageIndex < RelatedEventScanPageLimit; pageIndex++)
            {
                var query = new BattleDiagnosticEventQuery(
                    NextRequestId(),
                    BattleDiagnosticFilter.Default,
                    new BattleDiagnosticPageRequest(revision, offset, RelatedEventScanPageSize),
                    newestFirst: true);
                var result = session.QueryEvents(query);
                if (!CanRetainItems(result.Status))
                {
                    RelatedEventStatusMessage =
                        $"关联事件查询不可用：{BattleDebugDisplayText.Availability(result.Status.Availability)} " +
                        result.Status.Message;
                    return false;
                }

                scanned += result.Items.Count;
                for (var i = 0; i < result.Items.Count; i++)
                {
                    var candidate = result.Items[i];
                    if (!Matches(in runtimeObject, candidate.SourceActor) &&
                        !Matches(in runtimeObject, candidate.TargetActor) &&
                        !Matches(in runtimeObject, candidate.SubjectObject))
                        continue;

                    if (!found || IsNewer(in candidate, in diagnosticEvent))
                    {
                        diagnosticEvent = candidate;
                        found = true;
                    }
                }

                if (!result.Status.HasMore)
                {
                    exhausted = true;
                    break;
                }

                offset += RelatedEventScanPageSize;
            }

            if (found)
            {
                RelatedEventStatusMessage =
                    $"已找到第 {diagnosticEvent.Frame} 帧、序列 {diagnosticEvent.Sequence} 的事件。";
                return true;
            }

            RelatedEventStatusMessage = exhausted
                ? $"在保留的 {scanned} 条事件中未找到关联事件。"
                : $"在最新 {scanned} 条事件中未找到关联事件；未扫描更早的数据。";
            return false;
        }

        public long GetPreferredActorId()
        {
            if (!Selected.HasValue) return 0L;
            var value = Selected.Value;
            if (value.RelatedActorId != 0L) return value.RelatedActorId;
            if (value.SourceActorId != 0L) return value.SourceActorId;
            if (value.OwnerActorId != 0L) return value.OwnerActorId;
            return value.TargetActorId;
        }

        internal BattleDiagnosticRuntimeObjectFilter CurrentFilter()
        {
            return new BattleDiagnosticRuntimeObjectFilter(Kind, State, Completeness);
        }

        internal static bool Matches(
            in BattleDiagnosticRuntimeObject runtimeObject,
            in BattleDiagnosticRuntimeObjectReference reference)
        {
            if (!reference.HasRuntimeId ||
                reference.Kind != runtimeObject.Kind ||
                reference.RuntimeId != runtimeObject.RuntimeId)
                return false;
            return reference.Generation == 0 || reference.Generation == runtimeObject.Generation;
        }

        private static bool IsNewer(
            in BattleDiagnosticEvent candidate,
            in BattleDiagnosticEvent current)
        {
            return candidate.Frame > current.Frame ||
                   (candidate.Frame == current.Frame && candidate.Sequence > current.Sequence);
        }

        private static bool CanRetainItems(in BattleDiagnosticQueryStatus status)
        {
            return status.CanDisplayResults || status.Phase == BattleDiagnosticQueryPhase.Empty;
        }

        private static string BuildStatusMessage(in BattleDiagnosticQueryStatus status)
        {
            switch (status.Phase)
            {
                case BattleDiagnosticQueryPhase.Empty:
                    return "没有符合当前过滤条件的运行时对象。";
                case BattleDiagnosticQueryPhase.Partial:
                    return string.IsNullOrEmpty(status.Message)
                        ? "当前仅有部分运行时对象数据可用。"
                        : status.Message;
                case BattleDiagnosticQueryPhase.Unavailable:
                case BattleDiagnosticQueryPhase.Error:
                    return string.IsNullOrEmpty(status.Message)
                        ? $"运行时对象目录不可用：{BattleDebugDisplayText.Availability(status.Availability)}。"
                        : status.Message;
                default:
                    return status.HasMore
                        ? $"已加载 {status.ResultCount} 个对象，还有更多。"
                        : string.Empty;
            }
        }

        private void SetUnsupported(IBattleDiagnosticReadOnlySession session)
        {
            if (_items != null && ReferenceEquals(_lastSession, session) &&
                QueryStatus.Availability == BattleDiagnosticDataAvailability.Unsupported)
                return;

            var revision = session is IBattleDiagnosticRuntimeObjectSession objectSession
                ? objectSession.RuntimeObjectStoreRevision
                : 0L;
            _lastSession = session;
            _lastScope = session.SessionInfo.Scope;
            _lastStoreRevision = revision;
            _worksetRevision = revision;
            _items = Array.Empty<BattleDiagnosticRuntimeObject>();
            Summary = null;
            HasMore = false;
            StatusMessage =
                "不支持：当前会话适配器未提供运行时对象目录查询。";
            QueryStatus = BattleDiagnosticQueryStatus.Unavailable(
                0L,
                revision,
                BattleDiagnosticDataAvailability.Unsupported,
                StatusMessage);
            SummaryQueryStatus = QueryStatus;
        }

        private void ReconcileSelection()
        {
            if (!Selected.HasValue) return;
            var selected = Selected.Value;
            for (var i = 0; i < Items.Count; i++)
            {
                var item = Items[i];
                if (item.Kind != selected.Kind ||
                    item.RuntimeId != selected.RuntimeId ||
                    item.Generation != selected.Generation)
                    continue;
                Selected = item;
                return;
            }

            Selected = null;
            RelatedEventStatusMessage = string.Empty;
        }

        private int FindSelectedIndex()
        {
            if (!Selected.HasValue) return -1;
            var selected = Selected.Value;
            for (var i = 0; i < Items.Count; i++)
            {
                var item = Items[i];
                if (item.Kind == selected.Kind &&
                    item.RuntimeId == selected.RuntimeId &&
                    item.Generation == selected.Generation)
                    return i;
            }

            return -1;
        }

        private long NextRequestId()
        {
            _nextRequestId++;
            if (_nextRequestId <= 0L) _nextRequestId = 1L;
            return _nextRequestId;
        }
    }
}
