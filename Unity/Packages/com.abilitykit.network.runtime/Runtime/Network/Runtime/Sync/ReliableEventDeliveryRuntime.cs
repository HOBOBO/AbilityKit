#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AbilityKit.Network.Runtime.Sync
{
    /// <summary>指定可靠事件提交后的确认责任归属。</summary>
    public enum ReliableEventAcknowledgementStrategy
    {
        /// <summary>不更新确认位置，适合只关心本地投递的宿主。</summary>
        Disabled = 0,

        /// <summary>提交后立即更新本地确认位置，远端 ACK 由宿主在运行时外发送。</summary>
        External = 1,

        /// <summary>由运行时发送远端 ACK，成功后再更新确认位置。</summary>
        Automatic = 2
    }

    /// <summary>描述可靠事件交付阶段可观察的失败类型。</summary>
    public enum ReliableEventDeliveryFailureKind
    {
        /// <summary>批次未通过可靠事件游标准入。</summary>
        BatchRejected = 0,
        /// <summary>等待权威基线期间的批次队列超过容量。</summary>
        PendingQueueOverflow = 1,
        /// <summary>业务事件处理器抛出异常。</summary>
        EventSinkFailed = 2,
        /// <summary>业务投递成功后，游标拒绝提交位置。</summary>
        CommitRejected = 3,
        /// <summary>一次远端 ACK 调用抛出异常。</summary>
        AcknowledgementAttemptFailed = 4,
        /// <summary>在最大尝试次数内未完成远端 ACK。</summary>
        AcknowledgementIncomplete = 5
    }

    /// <summary>可靠事件交付运行时的协议无关配置。</summary>
    public sealed class ReliableEventDeliveryOptions
    {
        /// <summary>获取或设置等待权威基线期间允许缓存的最大批次数。</summary>
        public int MaxPendingBatches { get; set; } = 32;

        /// <summary>获取或设置自动 ACK 的最大尝试次数。</summary>
        public int MaxAcknowledgementAttempts { get; set; } = 3;

        /// <summary>获取或设置确认责任策略。</summary>
        public ReliableEventAcknowledgementStrategy AcknowledgementStrategy { get; set; } =
            ReliableEventAcknowledgementStrategy.Automatic;

        /// <summary>获取或设置采用非零权威基线后是否同步确认该水位。</summary>
        public bool AcknowledgeAuthoritativeBaseline { get; set; } = true;

        /// <summary>获取或设置是否只回放与新权威基线属于同一时间线的缓存批次。</summary>
        public bool ReplayOnlyMatchingTimeline { get; set; } = true;

        /// <summary>获取或设置业务事件处理器抛出异常后是否要求重新建立权威基线。</summary>
        public bool InvalidateOnEventSinkFailure { get; set; }

        /// <summary>获取或设置 ACK 重试前的异步等待策略，参数为刚失败的尝试序号。</summary>
        public Func<int, Task>? AcknowledgementRetryDelay { get; set; }

        internal ReliableEventDeliveryOptions CloneValidated()
        {
            if (AcknowledgementStrategy != ReliableEventAcknowledgementStrategy.Disabled &&
                AcknowledgementStrategy != ReliableEventAcknowledgementStrategy.External &&
                AcknowledgementStrategy != ReliableEventAcknowledgementStrategy.Automatic)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(AcknowledgementStrategy),
                    AcknowledgementStrategy,
                    "可靠事件确认策略不是受支持的枚举值。");
            }

            return new ReliableEventDeliveryOptions
            {
                MaxPendingBatches = Math.Max(1, MaxPendingBatches),
                MaxAcknowledgementAttempts = Math.Max(1, MaxAcknowledgementAttempts),
                AcknowledgementStrategy = AcknowledgementStrategy,
                AcknowledgeAuthoritativeBaseline = AcknowledgeAuthoritativeBaseline,
                ReplayOnlyMatchingTimeline = ReplayOnlyMatchingTimeline,
                InvalidateOnEventSinkFailure = InvalidateOnEventSinkFailure,
                AcknowledgementRetryDelay = AcknowledgementRetryDelay ??
                                                (attempt => Task.Delay(50 * attempt))
            };
        }
    }

    /// <summary>提供交付状态机需要的可靠事件游标能力。</summary>
    public interface IReliableEventDeliveryCursor<TEvent>
    {
        /// <summary>获取事件流标识。</summary>
        string StreamId { get; }
        /// <summary>获取当前时间线标识。</summary>
        string TimelineId { get; }
        /// <summary>获取最后成功投递的序列号。</summary>
        long LastDeliveredSequence { get; }
        /// <summary>获取最后确认的序列号。</summary>
        long LastAcknowledgedSequence { get; }
        /// <summary>获取从服务端批次观察到的最高水位。</summary>
        long LastObservedWatermark { get; }
        /// <summary>准入一个协议无关事件批次。</summary>
        ReliableEventBatchResult<TEvent> Admit(in ReliableEventBatch<TEvent> batch);
        /// <summary>在业务投递成功后提交连续位置。</summary>
        bool CommitDelivered(string timelineId, long sequence);
        /// <summary>采用权威基线携带的事件水位。</summary>
        bool AdoptAuthoritativeBaseline(string timelineId, long eventWatermark);
        /// <summary>确认服务端或外置 ACK 责任方已接受的位置。</summary>
        bool ConfirmAcknowledged(string timelineId, long acceptedSequence);
        /// <summary>创建当前确认位置的持久化检查点。</summary>
        ReliableEventCheckpoint CreateCheckpoint();
        /// <summary>丢弃尚未形成连续区间的缓存事件。</summary>
        void DiscardPending();
    }

    /// <summary>
    /// 表示游标能够从通用可靠事件检查点恢复。自定义游标实现此接口后，SDK 可以统一接管检查点加载。
    /// </summary>
    public interface IReliableEventCheckpointRestorable
    {
        /// <summary>尝试采用持久化确认位置；检查点不属于当前事件流时返回 false。</summary>
        bool TryRestore(in ReliableEventCheckpoint checkpoint);
    }

    /// <summary>包含一次可靠事件交付失败的结构化上下文。</summary>
    public readonly struct ReliableEventDeliveryFailure
    {
        /// <summary>创建可靠事件交付失败上下文。</summary>
        public ReliableEventDeliveryFailure(
            ReliableEventDeliveryFailureKind kind,
            string timelineId,
            long requestedSequence,
            int attempt,
            ReliableEventBatchStatus? batchStatus = null,
            Exception? exception = null)
        {
            Kind = kind;
            TimelineId = timelineId ?? string.Empty;
            RequestedSequence = requestedSequence;
            Attempt = attempt;
            BatchStatus = batchStatus;
            Exception = exception;
        }

        /// <summary>获取失败类型。</summary>
        public ReliableEventDeliveryFailureKind Kind { get; }
        /// <summary>获取失败所属时间线标识。</summary>
        public string TimelineId { get; }
        /// <summary>获取失败涉及的序列号。</summary>
        public long RequestedSequence { get; }
        /// <summary>获取 ACK 尝试序号，非 ACK 失败为零。</summary>
        public int Attempt { get; }
        /// <summary>获取可选的批次准入状态。</summary>
        public ReliableEventBatchStatus? BatchStatus { get; }
        /// <summary>获取失败阶段捕获的异常。</summary>
        public Exception? Exception { get; }
    }

    /// <summary>
    /// 管理一个会话世代中的可靠事件基线等待、业务投递、提交、确认与检查点保存。
    /// 任意异步 ACK 完成前都会重新校验世代和时间线，避免旧连接污染新会话。
    /// </summary>
    public sealed class ReliableEventDeliveryRuntime<TEvent> : IDisposable
    {
        private readonly ReliableEventDeliveryOptions _options;
        private readonly Queue<ReliableEventBatch<TEvent>> _pendingBatches =
            new Queue<ReliableEventBatch<TEvent>>();
        private int _generation;
        private IReliableEventDeliveryCursor<TEvent>? _cursor;
        private Func<string, long, Task<long>>? _acknowledge;
        private Action<ReliableEventCheckpoint>? _saveCheckpoint;
        private Action<TEvent>? _eventSink;
        private Action<ReliableEventDeliveryFailure>? _failureObserved;
        private Action<ReliableEventDeliveryFailure>? _timelineInvalidated;
        private bool _awaitingBaseline;

        /// <summary>使用经过校验的交付配置创建运行时。</summary>
        public ReliableEventDeliveryRuntime(ReliableEventDeliveryOptions? options = null)
        {
            _options = (options ?? new ReliableEventDeliveryOptions()).CloneValidated();
        }

        /// <summary>获取当前事件流标识。</summary>
        public string StreamId => _cursor?.StreamId ?? string.Empty;
        /// <summary>获取当前时间线标识。</summary>
        public string TimelineId => _cursor?.TimelineId ?? string.Empty;
        /// <summary>获取最后成功投递的序列号。</summary>
        public long LastDeliveredSequence => _cursor?.LastDeliveredSequence ?? 0L;
        /// <summary>获取最后确认的序列号。</summary>
        public long LastAcknowledgedSequence => _cursor?.LastAcknowledgedSequence ?? 0L;
        /// <summary>获取当前是否正在等待权威基线。</summary>
        public bool AwaitingBaseline => _awaitingBaseline;
        /// <summary>获取权威基线建立前缓存的批次数。</summary>
        public int PendingBatchCount => _pendingBatches.Count;

        /// <summary>开始新的会话世代，并使此前尚未完成的 ACK 自动失效。</summary>
        public void BeginGeneration(
            IReliableEventDeliveryCursor<TEvent> cursor,
            Action<TEvent> eventSink,
            Action<ReliableEventDeliveryFailure> timelineInvalidated,
            Func<string, long, Task<long>>? acknowledge = null,
            Action<ReliableEventCheckpoint>? saveCheckpoint = null,
            Action<ReliableEventDeliveryFailure>? failureObserved = null,
            bool awaitAuthoritativeBaseline = true)
        {
            if (cursor == null) throw new ArgumentNullException(nameof(cursor));
            if (eventSink == null) throw new ArgumentNullException(nameof(eventSink));
            if (timelineInvalidated == null) throw new ArgumentNullException(nameof(timelineInvalidated));
            if (_options.AcknowledgementStrategy == ReliableEventAcknowledgementStrategy.Automatic &&
                acknowledge == null)
            {
                throw new ArgumentNullException(nameof(acknowledge));
            }

            ResetGeneration();
            _cursor = cursor;
            _eventSink = eventSink;
            _timelineInvalidated = timelineInvalidated;
            _acknowledge = acknowledge;
            _saveCheckpoint = saveCheckpoint;
            _failureObserved = failureObserved;
            _awaitingBaseline = awaitAuthoritativeBaseline;
        }

        /// <summary>阻止后续批次继续投递，直到采用新的权威基线。</summary>
        public void RequireAuthoritativeBaseline()
        {
            if (_cursor != null)
            {
                _awaitingBaseline = true;
            }
        }

        /// <summary>处理一个已完成线协议映射的可靠事件批次。</summary>
        public void Handle(in ReliableEventBatch<TEvent> batch)
        {
            if (_cursor == null) return;
            if (_awaitingBaseline)
            {
                Queue(in batch);
                return;
            }

            Deliver(in batch);
        }

        /// <summary>采用权威事件水位，并按配置回放基线建立前缓存的批次。</summary>
        public bool AdoptAuthoritativeBaseline(string timelineId, long eventWatermark)
        {
            var cursor = _cursor;
            if (cursor == null || !cursor.AdoptAuthoritativeBaseline(timelineId, eventWatermark))
            {
                _pendingBatches.Clear();
                return false;
            }

            PersistCheckpoint(cursor);
            if (_options.AcknowledgeAuthoritativeBaseline && eventWatermark > 0)
            {
                ConfirmOrAcknowledge(timelineId, eventWatermark);
            }

            _awaitingBaseline = false;
            while (!_awaitingBaseline && _pendingBatches.Count > 0)
            {
                var pending = _pendingBatches.Dequeue();
                if (_options.ReplayOnlyMatchingTimeline &&
                    !string.Equals(pending.TimelineId, timelineId, StringComparison.Ordinal))
                {
                    continue;
                }

                Deliver(in pending);
            }

            return !_awaitingBaseline;
        }

        /// <summary>结束当前会话世代并释放所有回调引用。</summary>
        public void Dispose()
        {
            ResetGeneration();
        }

        private void Deliver(in ReliableEventBatch<TEvent> batch)
        {
            var generation = _generation;
            var cursor = _cursor;
            if (cursor == null) return;

            var result = cursor.Admit(in batch);
            if (!result.Accepted)
            {
                var failure = new ReliableEventDeliveryFailure(
                    ReliableEventDeliveryFailureKind.BatchRejected,
                    result.TimelineId,
                    result.ReceivedSequence,
                    0,
                    result.Status);
                ObserveFailure(in failure);
                Invalidate(in failure);
                if (generation == _generation && ReferenceEquals(cursor, _cursor))
                {
                    Queue(in batch);
                }

                return;
            }

            if (result.Events.Length == 0)
            {
                if (cursor.LastDeliveredSequence > cursor.LastAcknowledgedSequence)
                {
                    ConfirmOrAcknowledge(cursor.TimelineId, cursor.LastDeliveredSequence);
                }

                return;
            }

            try
            {
                for (var i = 0; i < result.Events.Length; i++)
                {
                    _eventSink?.Invoke(result.Events[i]);
                }
            }
            catch (Exception ex)
            {
                var failure = new ReliableEventDeliveryFailure(
                    ReliableEventDeliveryFailureKind.EventSinkFailed,
                    result.TimelineId,
                    result.CommitSequence,
                    0,
                    exception: ex);
                ObserveFailure(in failure);
                if (_options.InvalidateOnEventSinkFailure)
                {
                    Invalidate(in failure);
                }

                return;
            }

            if (generation != _generation || !ReferenceEquals(cursor, _cursor)) return;

            if (!cursor.CommitDelivered(result.TimelineId, result.CommitSequence))
            {
                var failure = new ReliableEventDeliveryFailure(
                    ReliableEventDeliveryFailureKind.CommitRejected,
                    result.TimelineId,
                    result.CommitSequence,
                    0);
                ObserveFailure(in failure);
                Invalidate(in failure);
                return;
            }

            ConfirmOrAcknowledge(result.TimelineId, result.CommitSequence);
        }

        private void Queue(in ReliableEventBatch<TEvent> batch)
        {
            if (_pendingBatches.Count >= _options.MaxPendingBatches)
            {
                _pendingBatches.Clear();
                var failure = new ReliableEventDeliveryFailure(
                    ReliableEventDeliveryFailureKind.PendingQueueOverflow,
                    batch.TimelineId,
                    batch.Watermark,
                    0);
                ObserveFailure(in failure);
                Invalidate(in failure);
                return;
            }

            _pendingBatches.Enqueue(batch);
        }

        private void ConfirmOrAcknowledge(string timelineId, long sequence)
        {
            var cursor = _cursor;
            if (cursor == null || string.IsNullOrWhiteSpace(timelineId) || sequence <= 0) return;

            switch (_options.AcknowledgementStrategy)
            {
                case ReliableEventAcknowledgementStrategy.Disabled:
                    return;
                case ReliableEventAcknowledgementStrategy.External:
                    if (cursor.ConfirmAcknowledged(timelineId, sequence))
                    {
                        PersistCheckpoint(cursor);
                    }

                    return;
                default:
                    _ = AcknowledgeAsync(timelineId, sequence);
                    return;
            }
        }

        private async Task AcknowledgeAsync(string timelineId, long sequence)
        {
            var generation = _generation;
            var cursor = _cursor;
            var acknowledge = _acknowledge;
            if (cursor == null || acknowledge == null) return;

            var acceptedSequence = -1L;
            for (var attempt = 1; attempt <= _options.MaxAcknowledgementAttempts; attempt++)
            {
                try
                {
                    acceptedSequence = await acknowledge(timelineId, sequence);
                }
                catch (Exception ex)
                {
                    if (!IsCurrent(generation, cursor, acknowledge, timelineId)) return;
                    var attemptFailure = new ReliableEventDeliveryFailure(
                        ReliableEventDeliveryFailureKind.AcknowledgementAttemptFailed,
                        timelineId,
                        sequence,
                        attempt,
                        exception: ex);
                    ObserveFailure(in attemptFailure);
                }

                if (!IsCurrent(generation, cursor, acknowledge, timelineId)) return;
                if (cursor.LastAcknowledgedSequence >= sequence) return;
                if (acceptedSequence >= sequence &&
                    cursor.ConfirmAcknowledged(timelineId, acceptedSequence))
                {
                    PersistCheckpoint(cursor);
                    return;
                }

                if (attempt < _options.MaxAcknowledgementAttempts)
                {
                    await _options.AcknowledgementRetryDelay!(attempt);
                    if (!IsCurrent(generation, cursor, acknowledge, timelineId)) return;
                }
            }

            if (!IsCurrent(generation, cursor, acknowledge, timelineId) ||
                cursor.LastAcknowledgedSequence >= sequence)
            {
                return;
            }

            var failure = new ReliableEventDeliveryFailure(
                ReliableEventDeliveryFailureKind.AcknowledgementIncomplete,
                timelineId,
                sequence,
                _options.MaxAcknowledgementAttempts);
            ObserveFailure(in failure);
            Invalidate(in failure);
        }

        private bool IsCurrent(
            int generation,
            IReliableEventDeliveryCursor<TEvent> cursor,
            Func<string, long, Task<long>> acknowledge,
            string timelineId)
        {
            return generation == _generation &&
                   ReferenceEquals(cursor, _cursor) &&
                   ReferenceEquals(acknowledge, _acknowledge) &&
                   string.Equals(cursor.TimelineId, timelineId, StringComparison.Ordinal);
        }

        private void PersistCheckpoint(IReliableEventDeliveryCursor<TEvent> cursor)
        {
            var saveCheckpoint = _saveCheckpoint;
            if (saveCheckpoint == null || !ReferenceEquals(cursor, _cursor)) return;

            var checkpoint = cursor.CreateCheckpoint();
            if (checkpoint.IsValid)
            {
                saveCheckpoint(checkpoint);
            }
        }

        private void ObserveFailure(in ReliableEventDeliveryFailure failure)
        {
            _failureObserved?.Invoke(failure);
        }

        private void Invalidate(in ReliableEventDeliveryFailure failure)
        {
            _awaitingBaseline = true;
            _timelineInvalidated?.Invoke(failure);
        }

        private void ResetGeneration()
        {
            _generation++;
            _pendingBatches.Clear();
            _awaitingBaseline = false;
            _cursor = null;
            _acknowledge = null;
            _saveCheckpoint = null;
            _eventSink = null;
            _failureObserved = null;
            _timelineInvalidated = null;
        }
    }
}
