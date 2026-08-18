using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Diagnostics;

namespace AbilityKit.Game.Editor
{
    internal sealed class BattleDebugDiagnosticBuffsViewModel
    {
        private const int TimelineLimit = 128;
        private const int MaximumTimelinePages = 4;

        private long _lastRequestId;
        private BattleDiagnosticSessionScope _lastScope;
        private long _lastStoreRevision = -1;
        private long _lastEventStoreRevision = -1;
        private long _lastActorId;
        private int _lastFrame;
        private bool _hasCachedResult;
        private bool _hasCachedTimeline;
        private IReadOnlyList<BattleDiagnosticActorBuff> _buffs =
            Array.Empty<BattleDiagnosticActorBuff>();
        private IReadOnlyList<BattleDiagnosticEvent> _timelineEvents =
            Array.Empty<BattleDiagnosticEvent>();

        public IReadOnlyList<BattleDiagnosticActorBuff> Buffs => _buffs;
        public IReadOnlyList<BattleDiagnosticEvent> TimelineEvents => _timelineEvents;
        public BattleDiagnosticQueryStatus QueryStatus { get; private set; }
        public BattleDiagnosticQueryStatus EventQueryStatus { get; private set; }
        public string StatusMessage { get; private set; } = string.Empty;
        public string EventStatusMessage { get; private set; } = string.Empty;
        public long StoreRevision => _lastStoreRevision;
        public long EventStoreRevision => _lastEventStoreRevision;

        public void InvalidateCache()
        {
            _buffs = Array.Empty<BattleDiagnosticActorBuff>();
            _timelineEvents = Array.Empty<BattleDiagnosticEvent>();
            QueryStatus = default;
            EventQueryStatus = default;
            StatusMessage = string.Empty;
            EventStatusMessage = string.Empty;
            _lastStoreRevision = -1;
            _lastEventStoreRevision = -1;
            _hasCachedResult = false;
            _hasCachedTimeline = false;
        }

        public void RefreshIfNeeded(
            IBattleDiagnosticReadOnlySession session,
            long actorId,
            int frame = 0)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (actorId == 0) throw new ArgumentOutOfRangeException(nameof(actorId));

            var scope = session.SessionInfo.Scope;
            var revision = session.ActorBuffStoreRevision;
            var eventRevision = session.EventStoreRevision;
            var queryFrame = frame < 0 ? 0 : frame;
            var identityChanged = _lastScope != scope ||
                                  _lastActorId != actorId ||
                                  _lastFrame != queryFrame;

            if (!_hasCachedResult || identityChanged || _lastStoreRevision != revision)
            {
                var result = session.QueryActorBuffs(NextRequestId(), queryFrame, actorId);
                _lastStoreRevision = revision;
                _hasCachedResult = true;
                QueryStatus = result.Status;
                _buffs = result.Items ?? Array.Empty<BattleDiagnosticActorBuff>();
                StatusMessage = BuildStatusMessage(result.Status);
            }

            if (!_hasCachedTimeline || identityChanged || _lastEventStoreRevision != eventRevision)
            {
                RefreshTimeline(session, actorId, eventRevision);
            }

            _lastScope = scope;
            _lastActorId = actorId;
            _lastFrame = queryFrame;
        }

        private void RefreshTimeline(
            IBattleDiagnosticReadOnlySession session,
            long actorId,
            long eventRevision)
        {
            var filter = new BattleDiagnosticFilter(
                BattleDiagnosticFilter.Default.Frames,
                BattleDiagnosticEventChannel.Buff,
                actorId,
                BattleDiagnosticActorRelation.Target);
            var page = new BattleDiagnosticPageRequest(eventRevision, 0, TimelineLimit);
            var filtered = new List<BattleDiagnosticEvent>(TimelineLimit);
            var status = default(BattleDiagnosticQueryStatus);

            for (var pageIndex = 0; pageIndex < MaximumTimelinePages; pageIndex++)
            {
                var result = session.QueryEvents(new BattleDiagnosticEventQuery(
                    NextRequestId(),
                    filter,
                    page,
                    newestFirst: true));
                status = result.Status;
                if (result.Items != null)
                {
                    for (var i = 0; i < result.Items.Count && filtered.Count < TimelineLimit; i++)
                    {
                        var item = result.Items[i];
                        if (item.Payload.TryGetBuffLifecycle(out _)) filtered.Add(item);
                    }
                }

                if ((!status.CanDisplayResults && status.Phase != BattleDiagnosticQueryPhase.Empty) ||
                    filtered.Count >= TimelineLimit ||
                    !status.HasMore)
                {
                    break;
                }

                if (pageIndex == MaximumTimelinePages - 1)
                {
                    status = BattleDiagnosticQueryStatus.Partial(
                        status.RequestId,
                        eventRevision,
                        filtered.Count,
                        BattleDiagnosticDataAvailability.Truncated,
                        $"Buff timeline scan is limited to {MaximumTimelinePages * TimelineLimit} events.");
                    break;
                }

                page = page.NextPage();
            }

            _lastEventStoreRevision = eventRevision;
            _hasCachedTimeline = true;
            EventQueryStatus = status;
            _timelineEvents = filtered;
            EventStatusMessage = BuildEventStatusMessage(status, filtered.Count);
        }

        private long NextRequestId()
        {
            _lastRequestId++;
            if (_lastRequestId <= 0) _lastRequestId = 1;
            return _lastRequestId;
        }

        private static string BuildStatusMessage(BattleDiagnosticQueryStatus status)
        {
            if (!status.CanDisplayResults && status.Phase != BattleDiagnosticQueryPhase.Empty)
            {
                return $"Buff 数据不可用：{status.Availability} {status.Message}";
            }

            return string.Empty;
        }

        private static string BuildEventStatusMessage(
            BattleDiagnosticQueryStatus status,
            int lifecycleEventCount)
        {
            if (status.Phase == BattleDiagnosticQueryPhase.Partial &&
                status.Availability == BattleDiagnosticDataAvailability.Truncated)
            {
                return lifecycleEventCount == 0
                    ? $"扫描范围内未找到结构化 Buff 生命周期事件。{status.Message}"
                    : $"显示最近 {lifecycleEventCount} 条生命周期事件。{status.Message}";
            }

            if (!status.CanDisplayResults && status.Phase != BattleDiagnosticQueryPhase.Empty)
            {
                return $"Buff 生命周期事件不可用：{status.Availability} {status.Message}";
            }

            if (lifecycleEventCount == 0)
            {
                return "当前 Actor 尚无结构化 Buff 生命周期事件。";
            }

            return status.HasMore
                ? $"显示最近 {lifecycleEventCount} 条生命周期事件。"
                : string.Empty;
        }
    }
}
