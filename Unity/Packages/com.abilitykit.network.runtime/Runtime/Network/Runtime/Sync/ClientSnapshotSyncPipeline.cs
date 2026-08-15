#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Network.Runtime.Sync
{
    /// <summary>一次客户端快照应用尝试的协议无关结果。</summary>
    public enum ClientSnapshotSyncStatus
    {
        /// <summary>完整基线已应用并提交。</summary>
        AppliedFullBaseline = 0,

        /// <summary>增量快照已应用并提交。</summary>
        AppliedDelta = 1,

        /// <summary>重复或过期快照已忽略。</summary>
        IgnoredStale = 2,

        /// <summary>数据流需要新的完整基线才能继续接受增量。</summary>
        NeedsFullBaseline = 3,

        /// <summary>快照结构版本超出支持范围。</summary>
        UnsupportedVersion = 4
    }

    /// <summary>一次管线应用产生的结果、校验详情与健康事件。</summary>
    public readonly struct ClientSnapshotSyncResult
    {
        private static readonly IReadOnlyList<SyncHealthEvent> EmptyHealthEvents = Array.Empty<SyncHealthEvent>();
        private readonly IReadOnlyList<SyncHealthEvent>? _healthEvents;

        internal ClientSnapshotSyncResult(
            ClientSnapshotSyncStatus status,
            in SnapshotStreamValidationResult validation,
            IReadOnlyList<SyncHealthEvent>? healthEvents)
        {
            Status = status;
            Validation = validation;
            _healthEvents = healthEvents;
        }

        /// <summary>通用应用状态。</summary>
        public ClientSnapshotSyncStatus Status { get; }

        /// <summary>底层数据流校验详情。</summary>
        public SnapshotStreamValidationResult Validation { get; }

        /// <summary>本次应用产生的标准同步健康事件。</summary>
        public IReadOnlyList<SyncHealthEvent> HealthEvents => _healthEvents ?? EmptyHealthEvents;

        /// <summary>快照是否已执行项目应用回调并完成提交。</summary>
        public bool Applied => Status == ClientSnapshotSyncStatus.AppliedFullBaseline ||
            Status == ClientSnapshotSyncStatus.AppliedDelta;
    }

    /// <summary>管线权威流游标与恢复状态的只读快照。</summary>
    public readonly struct ClientSnapshotSyncState
    {
        internal ClientSnapshotSyncState(SnapshotStreamStateMachine stream)
        {
            HasAppliedSnapshot = stream.HasAppliedSnapshot;
            CurrentWorldId = stream.CurrentWorldId;
            LastAppliedSequence = stream.LastAppliedSequence;
            LastAppliedFrame = stream.LastAppliedFrame;
            LastAppliedStateHash = stream.LastAppliedStateHash;
            LastBaselineFrame = stream.LastBaselineFrame;
            LastBaselineHash = stream.LastBaselineHash;
            NeedsFullBaselineRecovery = stream.NeedsFullBaselineRecovery;
            LastRecoveryReason = stream.LastRecoveryReason;
            LastIgnoredFrame = stream.LastIgnoredFrame;
            LastRecoveryFrame = stream.LastRecoveryFrame;
            LastRecoveryStateHash = stream.LastRecoveryStateHash;
        }

        /// <summary>是否至少应用过一个快照。</summary>
        public bool HasAppliedSnapshot { get; }
        /// <summary>当前权威世界标识。</summary>
        public ulong CurrentWorldId { get; }
        /// <summary>最近提交快照的序列号。</summary>
        public long LastAppliedSequence { get; }
        /// <summary>最近提交快照的帧号。</summary>
        public int LastAppliedFrame { get; }
        /// <summary>最近提交快照的状态哈希。</summary>
        public uint LastAppliedStateHash { get; }
        /// <summary>当前有效完整基线的帧号。</summary>
        public int LastBaselineFrame { get; }
        /// <summary>当前有效完整基线的哈希。</summary>
        public uint LastBaselineHash { get; }
        /// <summary>是否在完整基线到达前阻止增量快照。</summary>
        public bool NeedsFullBaselineRecovery { get; }
        /// <summary>最近一次进入恢复状态的原因。</summary>
        public SnapshotStreamRecoveryReason LastRecoveryReason { get; }
        /// <summary>最近一次重复或过期快照的帧号。</summary>
        public int LastIgnoredFrame { get; }
        /// <summary>最近一次触发恢复状态的帧号。</summary>
        public int LastRecoveryFrame { get; }
        /// <summary>最近一次恢复状态关联的状态哈希。</summary>
        public uint LastRecoveryStateHash { get; }
    }

    /// <summary>
    /// 面向客户端基线/增量快照流的协议无关编排器。调用方提供信封映射与表现应用逻辑，
    /// 管线负责校验、恢复信号、标准健康事件与提交顺序。
    /// </summary>
    public sealed class ClientSnapshotSyncPipeline<TSnapshot>
    {
        private readonly SnapshotStreamStateMachine _stream;
        private readonly SnapshotEnvelopeFactory<TSnapshot> _createEnvelope;
        private readonly SnapshotApplyHandler<TSnapshot> _applySnapshot;
        private readonly SnapshotSequenceAdvancePolicy<TSnapshot>? _maximumSequenceAdvance;
        private readonly SnapshotEntityCountProvider<TSnapshot>? _entityCount;
        private readonly SnapshotRecoveryStrategy<TSnapshot>? _recoveryStrategy;
        private readonly SnapshotRecoveryHandler<TSnapshot>? _recoveryHandler;
        private readonly ClientSnapshotSyncHealthEventPolicy<TSnapshot>? _healthEventPolicy;
        private readonly IClientSnapshotSyncObserver<TSnapshot>? _observer;
        private readonly ClientSnapshotSyncObserverErrorHandler? _observerErrorHandler;

        /// <summary>使用经过校验的构造期选项创建管线。</summary>
        public ClientSnapshotSyncPipeline(ClientSnapshotSyncOptions<TSnapshot> options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            options.Validate();
            var minimumSupportedVersion = options.MinimumSupportedVersion;
            var maximumSupportedVersion = options.MaximumSupportedVersion;
            if (options.RequiredProfile.HasValue && options.AvailableCapabilities.HasValue)
            {
                var profile = options.RequiredProfile.Value;
                var capabilities = options.AvailableCapabilities.Value;
                var negotiation = NetworkSyncConfigurationValidator.Negotiate(
                    in profile,
                    minimumSupportedVersion,
                    maximumSupportedVersion,
                    in capabilities);
                negotiation.Report.ThrowIfInvalid("客户端快照同步能力协商");
                Negotiation = negotiation;
                minimumSupportedVersion = negotiation.MinimumSchemaVersion;
                maximumSupportedVersion = negotiation.MaximumSchemaVersion;
            }

            MinimumSupportedVersion = minimumSupportedVersion;
            MaximumSupportedVersion = maximumSupportedVersion;
            _stream = new SnapshotStreamStateMachine(
                minimumSupportedVersion,
                maximumSupportedVersion);
            _createEnvelope = options.CreateEnvelope!;
            _applySnapshot = options.ApplySnapshot!;
            _maximumSequenceAdvance = options.MaximumSequenceAdvance;
            _entityCount = options.EntityCount;
            _recoveryStrategy = options.RecoveryStrategy;
            _recoveryHandler = options.RecoveryHandler;
            _healthEventPolicy = options.HealthEventPolicy;
            _observer = options.Observer;
            _observerErrorHandler = options.ObserverErrorHandler;
        }

        /// <summary>兼容旧调用点的构造函数。新接入应使用 <see cref="ClientSnapshotSyncOptions{TSnapshot}"/>。</summary>
        public ClientSnapshotSyncPipeline(
            int minimumSupportedVersion,
            int maximumSupportedVersion,
            SnapshotEnvelopeFactory<TSnapshot> createEnvelope,
            SnapshotApplyHandler<TSnapshot> applySnapshot,
            SnapshotSequenceAdvancePolicy<TSnapshot>? maximumSequenceAdvance = null,
            SnapshotEntityCountProvider<TSnapshot>? entityCount = null)
            : this(new ClientSnapshotSyncOptions<TSnapshot>(
                minimumSupportedVersion,
                maximumSupportedVersion,
                createEnvelope,
                applySnapshot)
            {
                MaximumSequenceAdvance = maximumSequenceAdvance,
                EntityCount = entityCount
            })
        {
        }

        /// <summary>当前只读同步状态。</summary>
        public ClientSnapshotSyncState State => new ClientSnapshotSyncState(_stream);

        /// <summary>完成能力协商后实际接受的最低协议结构版本。</summary>
        public int MinimumSupportedVersion { get; }

        /// <summary>完成能力协商后实际接受的最高协议结构版本。</summary>
        public int MaximumSupportedVersion { get; }

        /// <summary>启用能力协商时生成的结构化协商结果；未配置协商时为空。</summary>
        public NetworkSyncNegotiationResult? Negotiation { get; }

        /// <summary>校验、应用、提交并上报一个协议快照。</summary>
        public ClientSnapshotSyncResult Apply(in TSnapshot snapshot)
        {
            var envelope = _createEnvelope(in snapshot);
            var validation = _maximumSequenceAdvance == null
                ? _stream.Validate(in envelope)
                : _stream.Validate(in envelope, NormalizeSequenceAdvance(_maximumSequenceAdvance(in snapshot)));

            if (validation.Status == SnapshotStreamValidationStatus.IgnoredDuplicate ||
                validation.Status == SnapshotStreamValidationStatus.IgnoredStale)
            {
                return Complete(
                    in snapshot,
                    ClientSnapshotSyncStatus.IgnoredStale,
                    in validation,
                    default,
                    entityCount: 0);
            }

            if (validation.Status == SnapshotStreamValidationStatus.UnsupportedVersion)
            {
                return Complete(
                    in snapshot,
                    ClientSnapshotSyncStatus.UnsupportedVersion,
                    in validation,
                    default,
                    entityCount: 0);
            }

            if (validation.NeedsFullBaseline)
            {
                var requestKind = _recoveryStrategy == null
                    ? SnapshotRecoveryRequestKind.FullSnapshot
                    : _recoveryStrategy(in snapshot, in validation);
                if (!Enum.IsDefined(typeof(SnapshotRecoveryRequestKind), requestKind))
                {
                    throw new InvalidOperationException($"Recovery strategy returned unsupported request kind '{requestKind}'.");
                }

                var recoveryRequest = new SnapshotRecoveryRequest(requestKind, in validation);
                var recoveryResult = CreateResult(
                    in snapshot,
                    ClientSnapshotSyncStatus.NeedsFullBaseline,
                    in validation,
                    in recoveryRequest,
                    entityCount: 0);
                if (recoveryRequest.ShouldDispatch)
                {
                    _recoveryHandler?.Invoke(in snapshot, in recoveryRequest);
                    NotifyRecoveryRequested(in snapshot, in recoveryRequest);
                }

                NotifyResult(in snapshot, in recoveryResult);
                return recoveryResult;
            }

            var acceptedStatus = envelope.IsFullBaseline
                ? ClientSnapshotSyncStatus.AppliedFullBaseline
                : ClientSnapshotSyncStatus.AppliedDelta;
            var acceptedResult = CreateResult(
                in snapshot,
                acceptedStatus,
                in validation,
                default,
                _entityCount == null ? 0 : _entityCount(in snapshot));
            _applySnapshot(in snapshot);
            _stream.CommitApplied(in validation);
            NotifyResult(in snapshot, in acceptedResult);
            return acceptedResult;
        }

        /// <summary>清除权威游标，后续增量必须等待新基线。</summary>
        public void Reset()
        {
            var previousState = State;
            _stream.Reset();
            NotifyReset(in previousState);
        }

        private static int NormalizeSequenceAdvance(int value)
        {
            return value > 0 ? value : int.MaxValue;
        }

        private ClientSnapshotSyncResult Complete(
            in TSnapshot snapshot,
            ClientSnapshotSyncStatus status,
            in SnapshotStreamValidationResult validation,
            in SnapshotRecoveryRequest recoveryRequest,
            int entityCount)
        {
            var result = CreateResult(
                in snapshot,
                status,
                in validation,
                in recoveryRequest,
                entityCount);
            NotifyResult(in snapshot, in result);
            return result;
        }

        private void NotifyResult(in TSnapshot snapshot, in ClientSnapshotSyncResult result)
        {
            if (_observer == null) return;

            try
            {
                _observer.OnResult(in snapshot, in result, State);
            }
            catch (Exception exception)
            {
                ReportObserverError(ClientSnapshotSyncObserverStage.Result, exception);
            }
        }

        private void NotifyRecoveryRequested(in TSnapshot snapshot, in SnapshotRecoveryRequest request)
        {
            if (_observer == null) return;

            try
            {
                _observer.OnRecoveryRequested(in snapshot, in request, State);
            }
            catch (Exception exception)
            {
                ReportObserverError(ClientSnapshotSyncObserverStage.RecoveryRequested, exception);
            }
        }

        private void NotifyReset(in ClientSnapshotSyncState previousState)
        {
            if (_observer == null) return;

            try
            {
                _observer.OnReset(in previousState);
            }
            catch (Exception exception)
            {
                ReportObserverError(ClientSnapshotSyncObserverStage.Reset, exception);
            }
        }

        private void ReportObserverError(ClientSnapshotSyncObserverStage stage, Exception exception)
        {
            if (_observerErrorHandler == null) return;

            try
            {
                _observerErrorHandler(stage, exception);
            }
            catch
            {
                // 诊断旁路不得改变同步状态或结果交付。
            }
        }

        private ClientSnapshotSyncResult CreateResult(
            in TSnapshot snapshot,
            ClientSnapshotSyncStatus status,
            in SnapshotStreamValidationResult validation,
            in SnapshotRecoveryRequest recoveryRequest,
            int entityCount)
        {
            var context = new ClientSnapshotSyncEventContext<TSnapshot>(
                in snapshot,
                status,
                in validation,
                in recoveryRequest,
                entityCount);
            var standardEvents = CreateStandardHealthEvents(in context);
            var healthEvents = _healthEventPolicy == null
                ? standardEvents
                : CopyHealthEvents(_healthEventPolicy(in context, standardEvents) ??
                    throw new InvalidOperationException("Health event policy returned null."));
            return new ClientSnapshotSyncResult(status, in validation, healthEvents);
        }

        private static IReadOnlyList<SyncHealthEvent> CreateStandardHealthEvents(
            in ClientSnapshotSyncEventContext<TSnapshot> context)
        {
            var envelope = context.Envelope;
            switch (context.Status)
            {
                case ClientSnapshotSyncStatus.IgnoredStale:
                    return new[]
                    {
                        SyncHealthEvent.Warning(
                            SyncHealthEventKind.SnapshotStale,
                            envelope.Frame,
                            context.Validation.LastAppliedFrame)
                    };
                case ClientSnapshotSyncStatus.UnsupportedVersion:
                    return new[]
                    {
                        SyncHealthEvent.Warning(
                            SyncHealthEventKind.SnapshotDropped,
                            envelope.Frame,
                            envelope.SchemaVersion)
                    };
                case ClientSnapshotSyncStatus.NeedsFullBaseline:
                    return CreateRecoveryHealthEvents(in context);
                case ClientSnapshotSyncStatus.AppliedFullBaseline:
                    return new[]
                    {
                        SyncHealthEvent.Info(SyncHealthEventKind.SnapshotReceived, envelope.Frame, context.EntityCount),
                        SyncHealthEvent.Info(SyncHealthEventKind.FullSnapshotApplied, envelope.Frame, envelope.BaselineFrame)
                    };
                case ClientSnapshotSyncStatus.AppliedDelta:
                    return new[]
                    {
                        SyncHealthEvent.Info(SyncHealthEventKind.SnapshotReceived, envelope.Frame, context.EntityCount)
                    };
                default:
                    return Array.Empty<SyncHealthEvent>();
            }
        }

        private static IReadOnlyList<SyncHealthEvent> CreateRecoveryHealthEvents(
            in ClientSnapshotSyncEventContext<TSnapshot> context)
        {
            var envelope = context.Envelope;
            var hasStandardRecoveryEvent = TryCreateRecoveryHealthEvent(in context, out var recoveryEvent);

            if (context.Validation.Status == SnapshotStreamValidationStatus.SequenceGap)
            {
                if (!hasStandardRecoveryEvent)
                {
                    return new[]
                    {
                        SyncHealthEvent.Error(SyncHealthEventKind.SnapshotGap, envelope.Frame, context.Validation.GapCount)
                    };
                }

                return new[]
                {
                    SyncHealthEvent.Error(SyncHealthEventKind.SnapshotGap, envelope.Frame, context.Validation.GapCount),
                    recoveryEvent
                };
            }

            return hasStandardRecoveryEvent
                ? new[] { recoveryEvent }
                : Array.Empty<SyncHealthEvent>();
        }

        private static bool TryCreateRecoveryHealthEvent(
            in ClientSnapshotSyncEventContext<TSnapshot> context,
            out SyncHealthEvent healthEvent)
        {
            var envelope = context.Envelope;
            var value = RecoveryEventValue(in context);
            switch (context.RecoveryRequest.Kind)
            {
                case SnapshotRecoveryRequestKind.FullSnapshot:
                    healthEvent = SyncHealthEvent.Info(SyncHealthEventKind.FullSnapshotRequested, envelope.Frame, value);
                    return true;
                case SnapshotRecoveryRequestKind.KeyFrame:
                    healthEvent = SyncHealthEvent.Info(SyncHealthEventKind.KeyFrameRequested, envelope.Frame, value);
                    return true;
                case SnapshotRecoveryRequestKind.AoiSlice:
                    healthEvent = SyncHealthEvent.Info(SyncHealthEventKind.AoiSliceRequested, envelope.Frame, value);
                    return true;
                case SnapshotRecoveryRequestKind.None:
                case SnapshotRecoveryRequestKind.Custom:
                default:
                    healthEvent = default;
                    return false;
            }
        }

        private static long RecoveryEventValue(in ClientSnapshotSyncEventContext<TSnapshot> context)
        {
            return context.Validation.Status == SnapshotStreamValidationStatus.SequenceGap
                ? context.Validation.GapCount
                : (long)context.Validation.RecoveryReason;
        }

        private static IReadOnlyList<SyncHealthEvent> CopyHealthEvents(IReadOnlyList<SyncHealthEvent> source)
        {
            if (source.Count == 0)
            {
                return Array.Empty<SyncHealthEvent>();
            }

            var copy = new SyncHealthEvent[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                copy[i] = source[i];
            }

            return copy;
        }
    }
}
