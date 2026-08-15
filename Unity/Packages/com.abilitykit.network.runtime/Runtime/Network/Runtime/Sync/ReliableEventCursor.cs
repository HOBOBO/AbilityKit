#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Network.Runtime.Sync
{
    /// <summary>
    /// 指定可靠事件遇到序列空洞时的处理方式。
    /// </summary>
    public enum ReliableEventGapPolicy
    {
        /// <summary>发现序列空洞后立即拒绝批次并要求恢复。</summary>
        Reject = 0,

        /// <summary>在容量限制内缓存未来事件，等待缺失序列到达。</summary>
        BufferWithinCapacity = 1
    }

    /// <summary>
    /// 指定采用权威基线时如何更新已确认游标。
    /// </summary>
    public enum ReliableEventBaselineAcknowledgementPolicy
    {
        /// <summary>保留不超过新水位的既有确认位置，切换时间线时清零。</summary>
        PreserveConfirmedWithinWatermark = 0,

        /// <summary>将权威基线水位同时视为已投递和已确认位置。</summary>
        ConfirmWatermark = 1
    }

    /// <summary>
    /// 可靠事件游标的协议无关配置。
    /// </summary>
    public sealed class ReliableEventCursorOptions
    {
        /// <summary>获取或设置序列空洞策略。</summary>
        public ReliableEventGapPolicy GapPolicy { get; set; } = ReliableEventGapPolicy.Reject;

        /// <summary>获取或设置乱序缓存允许保留的最大事件数。</summary>
        public int MaxPendingEvents { get; set; } = 512;

        /// <summary>获取或设置采用权威基线时的确认游标策略。</summary>
        public ReliableEventBaselineAcknowledgementPolicy BaselineAcknowledgementPolicy { get; set; } =
            ReliableEventBaselineAcknowledgementPolicy.PreserveConfirmedWithinWatermark;

        /// <summary>获取或设置同一时间线的恢复基线是否必须覆盖已观察水位。</summary>
        public bool RequireBaselineAtObservedWatermark { get; set; }

        /// <summary>获取或设置是否根据首个可用序列推断服务端保留窗口缺口。</summary>
        public bool InferRetentionGapFromFirstAvailableSequence { get; set; } = true;

        /// <summary>获取或设置是否在批次通过标识校验后立即绑定时间线。</summary>
        public bool BindTimelineOnAdmission { get; set; }

        internal ReliableEventCursorOptions CloneValidated()
        {
            return new ReliableEventCursorOptions
            {
                GapPolicy = GapPolicy,
                MaxPendingEvents = Math.Max(1, MaxPendingEvents),
                BaselineAcknowledgementPolicy = BaselineAcknowledgementPolicy,
                RequireBaselineAtObservedWatermark = RequireBaselineAtObservedWatermark,
                InferRetentionGapFromFirstAvailableSequence = InferRetentionGapFromFirstAvailableSequence,
                BindTimelineOnAdmission = BindTimelineOnAdmission
            };
        }
    }

    /// <summary>
    /// 描述如何从业务事件中读取可靠投递所需的通用字段。
    /// </summary>
    public sealed class ReliableEventDescriptor<TEvent>
    {
        /// <summary>创建业务事件字段描述器。</summary>
        public ReliableEventDescriptor(
            Func<TEvent, string?> streamId,
            Func<TEvent, string?> timelineId,
            Func<TEvent, long> sequence,
            Func<TEvent, bool>? validator = null)
        {
            StreamId = streamId ?? throw new ArgumentNullException(nameof(streamId));
            TimelineId = timelineId ?? throw new ArgumentNullException(nameof(timelineId));
            Sequence = sequence ?? throw new ArgumentNullException(nameof(sequence));
            Validator = validator;
        }

        /// <summary>获取事件所属流标识的读取器。</summary>
        public Func<TEvent, string?> StreamId { get; }

        /// <summary>获取事件所属时间线标识的读取器。</summary>
        public Func<TEvent, string?> TimelineId { get; }

        /// <summary>获取事件序列号的读取器。</summary>
        public Func<TEvent, long> Sequence { get; }

        /// <summary>获取可选的业务事件校验器。</summary>
        public Func<TEvent, bool>? Validator { get; }
    }

    /// <summary>
    /// 表示可持久化的可靠事件确认位置。
    /// </summary>
    public readonly struct ReliableEventCheckpoint
    {
        /// <summary>创建可靠事件确认位置。</summary>
        public ReliableEventCheckpoint(string streamId, string timelineId, long lastAcknowledgedSequence)
        {
            StreamId = streamId ?? string.Empty;
            TimelineId = timelineId ?? string.Empty;
            LastAcknowledgedSequence = Math.Max(0L, lastAcknowledgedSequence);
        }

        /// <summary>获取事件流标识。</summary>
        public string StreamId { get; }

        /// <summary>获取时间线标识。</summary>
        public string TimelineId { get; }

        /// <summary>获取最后一个已由服务端确认的序列号。</summary>
        public long LastAcknowledgedSequence { get; }

        /// <summary>获取当前确认位置是否具备持久化所需的标识。</summary>
        public bool IsValid =>
            !string.IsNullOrWhiteSpace(StreamId) &&
            !string.IsNullOrWhiteSpace(TimelineId);
    }

    /// <summary>
    /// 表示一次来自传输层的可靠事件批次。
    /// </summary>
    public readonly struct ReliableEventBatch<TEvent>
    {
        /// <summary>创建传输层可靠事件批次。</summary>
        public ReliableEventBatch(
            string? streamId,
            string? timelineId,
            long firstAvailableSequence,
            long watermark,
            bool retentionGap,
            IReadOnlyList<TEvent>? events)
        {
            StreamId = streamId ?? string.Empty;
            TimelineId = timelineId ?? string.Empty;
            FirstAvailableSequence = firstAvailableSequence;
            Watermark = watermark;
            RetentionGap = retentionGap;
            Events = events ?? Array.Empty<TEvent>();
        }

        /// <summary>获取事件流标识。</summary>
        public string StreamId { get; }

        /// <summary>获取时间线标识。</summary>
        public string TimelineId { get; }

        /// <summary>获取服务端当前仍保留的首个序列号。</summary>
        public long FirstAvailableSequence { get; }

        /// <summary>获取服务端已产生事件的最高水位。</summary>
        public long Watermark { get; }

        /// <summary>获取服务端是否明确报告了保留窗口缺口。</summary>
        public bool RetentionGap { get; }

        /// <summary>获取本批事件。</summary>
        public IReadOnlyList<TEvent> Events { get; }
    }

    /// <summary>描述可靠事件批次的准入状态。</summary>
    public enum ReliableEventBatchStatus
    {
        /// <summary>批次包含可投递事件。</summary>
        Accepted = 0,
        /// <summary>批次为空、仅含重复事件或仍在等待前序事件。</summary>
        DuplicateOnly = 1,
        /// <summary>事件流标识无效或不匹配。</summary>
        InvalidStream = 2,
        /// <summary>时间线标识为空。</summary>
        InvalidTimeline = 3,
        /// <summary>批次或事件属于另一条时间线。</summary>
        TimelineChanged = 4,
        /// <summary>服务端保留窗口已无法覆盖当前游标。</summary>
        RetentionGap = 5,
        /// <summary>严格模式下发现序列空洞。</summary>
        SequenceGap = 6,
        /// <summary>乱序缓存容量不足以原子接纳整个批次。</summary>
        CapacityExceeded = 7,
        /// <summary>事件字段未通过描述器校验。</summary>
        InvalidEvent = 8
    }

    /// <summary>
    /// 表示可靠事件批次的准入结果；调用方完成业务投递后才应提交 CommitSequence。
    /// </summary>
    public readonly struct ReliableEventBatchResult<TEvent>
    {
        /// <summary>创建可靠事件批次准入结果。</summary>
        public ReliableEventBatchResult(
            ReliableEventBatchStatus status,
            TEvent[] events,
            string timelineId,
            long commitSequence,
            long expectedSequence,
            long receivedSequence)
        {
            Status = status;
            Events = events ?? Array.Empty<TEvent>();
            TimelineId = timelineId ?? string.Empty;
            CommitSequence = commitSequence;
            ExpectedSequence = expectedSequence;
            ReceivedSequence = receivedSequence;
        }

        /// <summary>获取准入状态。</summary>
        public ReliableEventBatchStatus Status { get; }

        /// <summary>获取当前连续且可交给业务层投递的事件。</summary>
        public TEvent[] Events { get; }

        /// <summary>获取批次时间线标识。</summary>
        public string TimelineId { get; }

        /// <summary>获取业务投递成功后应提交的序列号。</summary>
        public long CommitSequence { get; }

        /// <summary>获取发生拒绝时期待收到的序列号。</summary>
        public long ExpectedSequence { get; }

        /// <summary>获取发生拒绝时实际收到的序列号。</summary>
        public long ReceivedSequence { get; }

        /// <summary>获取本批是否可以继续处理而无需全量恢复。</summary>
        public bool Accepted =>
            Status == ReliableEventBatchStatus.Accepted ||
            Status == ReliableEventBatchStatus.DuplicateOnly;

        /// <summary>获取本批是否要求重新建立权威基线。</summary>
        public bool ShouldRequestFullResync => !Accepted;
    }

    /// <summary>
    /// 管理可靠有序事件的准入、投递提交、确认以及权威基线恢复。
    /// </summary>
    public sealed class ReliableEventCursor<TEvent> :
        IReliableEventDeliveryCursor<TEvent>,
        IReliableEventCheckpointRestorable
    {
        private static readonly TEvent[] EmptyEvents = Array.Empty<TEvent>();

        private readonly string _streamId;
        private readonly ReliableEventDescriptor<TEvent> _descriptor;
        private readonly ReliableEventCursorOptions _options;
        private readonly SortedDictionary<long, TEvent> _pending = new SortedDictionary<long, TEvent>();
        private string _timelineId = string.Empty;
        private long _lastDeliveredSequence;
        private long _lastAcknowledgedSequence;
        private long _lastObservedWatermark;

        /// <summary>创建指定事件流的可靠事件游标。</summary>
        public ReliableEventCursor(
            string streamId,
            ReliableEventDescriptor<TEvent> descriptor,
            ReliableEventCursorOptions? options = null)
        {
            _streamId = streamId ?? string.Empty;
            _descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            _options = (options ?? new ReliableEventCursorOptions()).CloneValidated();
        }

        /// <summary>获取当前事件流标识。</summary>
        public string StreamId => _streamId;

        /// <summary>获取当前时间线标识。</summary>
        public string TimelineId => _timelineId;

        /// <summary>获取已由业务层成功投递的最后序列号。</summary>
        public long LastDeliveredSequence => _lastDeliveredSequence;

        /// <summary>获取已由服务端确认的最后序列号。</summary>
        public long LastAcknowledgedSequence => _lastAcknowledgedSequence;

        /// <summary>获取从服务端批次观察到的最高水位。</summary>
        public long LastObservedWatermark => _lastObservedWatermark;

        /// <summary>从持久化确认位置恢复游标。</summary>
        public bool TryRestore(in ReliableEventCheckpoint checkpoint)
        {
            if (!checkpoint.IsValid ||
                (!string.IsNullOrEmpty(_streamId) &&
                 !string.Equals(_streamId, checkpoint.StreamId, StringComparison.Ordinal)))
            {
                return false;
            }

            _timelineId = checkpoint.TimelineId;
            _lastDeliveredSequence = checkpoint.LastAcknowledgedSequence;
            _lastAcknowledgedSequence = checkpoint.LastAcknowledgedSequence;
            _lastObservedWatermark = checkpoint.LastAcknowledgedSequence;
            _pending.Clear();
            return true;
        }

        /// <summary>创建仅包含服务端已确认位置的持久化检查点。</summary>
        public ReliableEventCheckpoint CreateCheckpoint()
        {
            return new ReliableEventCheckpoint(
                _streamId,
                _timelineId,
                _lastAcknowledgedSequence);
        }

        /// <summary>校验并准入一个可靠事件批次，但不提前推进投递游标。</summary>
        public ReliableEventBatchResult<TEvent> Admit(in ReliableEventBatch<TEvent> batch)
        {
            if (string.IsNullOrWhiteSpace(batch.StreamId) ||
                (!string.IsNullOrEmpty(_streamId) &&
                 !string.Equals(_streamId, batch.StreamId, StringComparison.Ordinal)))
            {
                return Reject(ReliableEventBatchStatus.InvalidStream, batch.TimelineId, _lastDeliveredSequence + 1, 0L);
            }

            if (string.IsNullOrWhiteSpace(batch.TimelineId))
            {
                return Reject(ReliableEventBatchStatus.InvalidTimeline, batch.TimelineId, _lastDeliveredSequence + 1, 0L);
            }

            if (!string.IsNullOrEmpty(_timelineId) &&
                !string.Equals(_timelineId, batch.TimelineId, StringComparison.Ordinal))
            {
                return Reject(ReliableEventBatchStatus.TimelineChanged, batch.TimelineId, _lastDeliveredSequence + 1, 0L);
            }

            if (_options.BindTimelineOnAdmission && string.IsNullOrEmpty(_timelineId))
            {
                _timelineId = batch.TimelineId;
            }

            _lastObservedWatermark = Math.Max(_lastObservedWatermark, batch.Watermark);
            var expected = _lastDeliveredSequence + 1;
            if (batch.RetentionGap ||
                (_options.InferRetentionGapFromFirstAvailableSequence &&
                 batch.FirstAvailableSequence > 0 &&
                 expected < batch.FirstAvailableSequence))
            {
                return Reject(
                    ReliableEventBatchStatus.RetentionGap,
                    batch.TimelineId,
                    expected,
                    batch.FirstAvailableSequence);
            }

            return _options.GapPolicy == ReliableEventGapPolicy.BufferWithinCapacity
                ? AdmitBuffered(in batch)
                : AdmitStrict(in batch);
        }

        /// <summary>在业务投递成功后提交连续投递位置。</summary>
        public bool CommitDelivered(string timelineId, long sequence)
        {
            if (string.IsNullOrWhiteSpace(timelineId) ||
                sequence < _lastDeliveredSequence ||
                (!string.IsNullOrEmpty(_timelineId) &&
                 !string.Equals(_timelineId, timelineId, StringComparison.Ordinal)))
            {
                return false;
            }

            _timelineId = timelineId;
            _lastDeliveredSequence = sequence;
            RemovePendingThrough(sequence);
            return true;
        }

        /// <summary>采用全量快照携带的权威事件水位，并清理旧的乱序缓存。</summary>
        public bool AdoptAuthoritativeBaseline(string timelineId, long eventWatermark)
        {
            if (string.IsNullOrWhiteSpace(timelineId) || eventWatermark < 0)
            {
                return false;
            }

            var sameTimeline = string.Equals(_timelineId, timelineId, StringComparison.Ordinal);
            if (_options.RequireBaselineAtObservedWatermark &&
                sameTimeline &&
                eventWatermark < _lastObservedWatermark)
            {
                return false;
            }

            _timelineId = timelineId;
            _lastDeliveredSequence = eventWatermark;
            _lastAcknowledgedSequence = _options.BaselineAcknowledgementPolicy ==
                                        ReliableEventBaselineAcknowledgementPolicy.ConfirmWatermark
                ? eventWatermark
                : sameTimeline
                    ? Math.Min(_lastAcknowledgedSequence, eventWatermark)
                    : 0L;
            _lastObservedWatermark = eventWatermark;
            _pending.Clear();
            return true;
        }

        /// <summary>确认服务端已接受的 ACK；较旧的乱序 ACK 按幂等成功处理。</summary>
        public bool ConfirmAcknowledged(string timelineId, long acceptedSequence)
        {
            if (string.IsNullOrEmpty(_timelineId) ||
                !string.Equals(_timelineId, timelineId, StringComparison.Ordinal) ||
                acceptedSequence < 0 ||
                acceptedSequence > _lastDeliveredSequence)
            {
                return false;
            }

            if (acceptedSequence > _lastAcknowledgedSequence)
            {
                _lastAcknowledgedSequence = acceptedSequence;
            }

            return true;
        }

        /// <summary>丢弃尚未形成连续投递区间的缓存事件。</summary>
        public void DiscardPending()
        {
            _pending.Clear();
        }

        /// <summary>清空时间线、投递位置、确认位置和乱序缓存。</summary>
        public void Reset()
        {
            _timelineId = string.Empty;
            _lastDeliveredSequence = 0L;
            _lastAcknowledgedSequence = 0L;
            _lastObservedWatermark = 0L;
            _pending.Clear();
        }

        private ReliableEventBatchResult<TEvent> AdmitStrict(in ReliableEventBatch<TEvent> batch)
        {
            var expected = _lastDeliveredSequence + 1;
            List<TEvent>? deliverable = null;
            for (var i = 0; i < batch.Events.Count; i++)
            {
                var item = batch.Events[i];
                var invalid = ValidateEvent(item, batch.StreamId, batch.TimelineId);
                if (invalid.HasValue)
                {
                    return Reject(invalid.Value, batch.TimelineId, expected, _descriptor.Sequence(item));
                }

                var sequence = _descriptor.Sequence(item);
                if (sequence <= _lastDeliveredSequence)
                {
                    continue;
                }

                if (sequence != expected)
                {
                    return Reject(ReliableEventBatchStatus.SequenceGap, batch.TimelineId, expected, sequence);
                }

                deliverable ??= new List<TEvent>(batch.Events.Count - i);
                deliverable.Add(item);
                expected++;
            }

            return Accepted(batch.TimelineId, deliverable, expected - 1);
        }

        private ReliableEventBatchResult<TEvent> AdmitBuffered(in ReliableEventBatch<TEvent> batch)
        {
            var additions = new List<KeyValuePair<long, TEvent>>();
            for (var i = 0; i < batch.Events.Count; i++)
            {
                var item = batch.Events[i];
                var invalid = ValidateEvent(item, batch.StreamId, batch.TimelineId);
                if (invalid.HasValue)
                {
                    return Reject(invalid.Value, batch.TimelineId, _lastDeliveredSequence + 1, _descriptor.Sequence(item));
                }

                var sequence = _descriptor.Sequence(item);
                if (sequence <= _lastDeliveredSequence ||
                    _pending.ContainsKey(sequence) ||
                    ContainsSequence(additions, sequence))
                {
                    continue;
                }

                if (_pending.Count + additions.Count >= _options.MaxPendingEvents)
                {
                    return Reject(
                        ReliableEventBatchStatus.CapacityExceeded,
                        batch.TimelineId,
                        _lastDeliveredSequence + 1,
                        sequence);
                }

                additions.Add(new KeyValuePair<long, TEvent>(sequence, item));
            }

            for (var i = 0; i < additions.Count; i++)
            {
                _pending.Add(additions[i].Key, additions[i].Value);
            }

            var expected = _lastDeliveredSequence + 1;
            List<TEvent>? deliverable = null;
            while (_pending.TryGetValue(expected, out var next))
            {
                deliverable ??= new List<TEvent>();
                deliverable.Add(next);
                expected++;
            }

            return Accepted(batch.TimelineId, deliverable, expected - 1);
        }

        private ReliableEventBatchStatus? ValidateEvent(TEvent item, string streamId, string timelineId)
        {
            if (!string.Equals(_descriptor.StreamId(item) ?? string.Empty, streamId, StringComparison.Ordinal))
            {
                return ReliableEventBatchStatus.InvalidStream;
            }

            if (!string.Equals(_descriptor.TimelineId(item) ?? string.Empty, timelineId, StringComparison.Ordinal))
            {
                return ReliableEventBatchStatus.TimelineChanged;
            }

            if (_descriptor.Sequence(item) <= 0 ||
                (_descriptor.Validator != null && !_descriptor.Validator(item)))
            {
                return ReliableEventBatchStatus.InvalidEvent;
            }

            return null;
        }

        private ReliableEventBatchResult<TEvent> Accepted(
            string timelineId,
            List<TEvent>? deliverable,
            long commitSequence)
        {
            if (deliverable == null || deliverable.Count == 0)
            {
                return new ReliableEventBatchResult<TEvent>(
                    ReliableEventBatchStatus.DuplicateOnly,
                    EmptyEvents,
                    timelineId,
                    _lastDeliveredSequence,
                    _lastDeliveredSequence + 1,
                    0L);
            }

            return new ReliableEventBatchResult<TEvent>(
                ReliableEventBatchStatus.Accepted,
                deliverable.ToArray(),
                timelineId,
                commitSequence,
                _lastDeliveredSequence + 1,
                commitSequence);
        }

        private ReliableEventBatchResult<TEvent> Reject(
            ReliableEventBatchStatus status,
            string timelineId,
            long expectedSequence,
            long receivedSequence)
        {
            return new ReliableEventBatchResult<TEvent>(
                status,
                EmptyEvents,
                timelineId,
                _lastDeliveredSequence,
                expectedSequence,
                receivedSequence);
        }

        private void RemovePendingThrough(long sequence)
        {
            if (_pending.Count == 0)
            {
                return;
            }

            var keys = new List<long>();
            foreach (var pair in _pending)
            {
                if (pair.Key > sequence)
                {
                    break;
                }

                keys.Add(pair.Key);
            }

            for (var i = 0; i < keys.Count; i++)
            {
                _pending.Remove(keys[i]);
            }
        }

        private static bool ContainsSequence(
            List<KeyValuePair<long, TEvent>> additions,
            long sequence)
        {
            for (var i = 0; i < additions.Count; i++)
            {
                if (additions[i].Key == sequence)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
