using System;
using System.Collections.Generic;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Network.Battle;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Network.Room;
using AbilityKit.Network.Sdk;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// Owns the transport bindings and mutable resources for one remote replication generation.
    /// BattleSessionFeature remains the business callback facade during the staged migration.
    /// </summary>
    internal sealed class BattleReplicationRuntime : IDisposable
    {
        internal const string SyncProfileName = "Moba.AuthoritativeRemoteInterpolation";

        private static readonly NetworkSyncProfile SyncProfile = new NetworkSyncProfile(
            NetworkSyncModel.AuthoritativeInterpolation,
            ClientPlaybackPolicy.AuthoritativeInterpolation,
            InputPolicy.ImmediateSubmit,
            SnapshotPolicy.FullSnapshot | SnapshotPolicy.DeltaSnapshot |
            SnapshotPolicy.FixedRateStateStream | SnapshotPolicy.EventStream,
            InterestPolicy.AllEntities,
            RecoveryPolicy.RequestFullSnapshot,
            ServerValidationPolicy.AuthoritativeOnly | ServerValidationPolicy.InputValidation,
            ReliableEventPolicy.OrderedDelivery | ReliableEventPolicy.AutomaticAcknowledgement |
            ReliableEventPolicy.PersistentCheckpoint |
            ReliableEventPolicy.AuthoritativeBaselineRecovery);
        private static readonly NetworkSyncProfileCatalog SyncProfileCatalog = CreateSyncProfileCatalog();
        private static readonly NetworkSyncProfileControllerRegistry<MobaClientAuthoritativeInterpolationSyncController, MobaSyncControllerContext>
            SyncControllerRegistry = CreateSyncControllerRegistry();

        private readonly object _inputSubmissionStatsGate = new object();
        private readonly Action _beforeInputSubmissionStatsBind;
        private int _generation;
        private Action<object> _snapshotPushed;
        private Action<object> _reliableEventsPushed;
        private Action _connectionClosed;
        private Action _connectionEstablished;
        private Action<Exception> _authenticationFailed;
        private Action<NetworkSubmitInputResponse> _submitInputCompleted;
        private Action<Exception> _submitInputFailed;
        private InputSubmissionStatsSnapshot _inputSubmissionStats;
        private Func<string> _getReliableEventEpoch;
        private Func<long> _getReliableEventLastAcknowledgedSequence;
        private Action<int> _submitInputAck;
        private Func<string> _previousGetReliableEventEpoch;
        private Func<long> _previousGetReliableEventLastAcknowledgedSequence;
        private Action<int> _previousSubmitInputAck;

        internal BattleReplicationRuntime(Action beforeInputSubmissionStatsBind = null)
        {
            _beforeInputSubmissionStatsBind = beforeInputSubmissionStatsBind;
        }

        internal NetworkTransport Transport { get; private set; }
        internal MobaClientAuthoritativeInterpolationSyncController InterpolationController { get; private set; }
        /// <summary>本次复制代际经过校验的同步会话描述。</summary>
        internal NetworkSyncSessionDescriptor SyncSession { get; private set; }
        internal RoomGatewayNetworkSyncSessionBinding SyncBinding { get; private set; }
        internal MobaClientReplicationPipeline ReplicationPipeline { get; private set; }
        internal MobaSynchronizationHealthEvaluator SynchronizationHealthEvaluator { get; private set; }
        internal MobaSynchronizationHealthSnapshot SynchronizationHealth { get; set; }
        internal SyncHealthReport SynchronizationHealthReport { get; set; } = SyncHealthReport.Empty;
        internal float SynchronizationHealthSampleElapsed { get; set; }
        internal MobaSnapshotAdmission SnapshotAdmission { get; private set; }
        internal MobaAuthoritativeSnapshotState AuthoritativeSnapshotState { get; private set; }
        internal MobaReliableBattleEventCursor ReliableEventCursor { get; private set; }
        internal Queue<WireReliableBattleEventPush> PendingReliableEventBatches { get; } =
            new Queue<WireReliableBattleEventPush>();
        internal int LastServerAckFrame { get; set; }
        internal bool PendingStateImport { get; set; }
        internal bool IsBuilt => Transport != null;

        internal bool Build(
            NetworkTransport transport,
            int tickRate,
            ulong roomId,
            string battleId,
            in MobaReliableBattleEventCheckpoint reliableEventCheckpoint,
            Action<object> onSnapshotPushed,
            Action<object> onReliableEventsPushed,
            Action onConnectionClosed,
            Action onConnectionEstablished,
            Action<Exception> onAuthenticationFailed = null,
            RoomGatewayNetworkSyncCapabilities remoteCapabilities = null)
        {
            if (transport == null) throw new ArgumentNullException(nameof(transport));
            if (onSnapshotPushed == null) throw new ArgumentNullException(nameof(onSnapshotPushed));
            if (onReliableEventsPushed == null) throw new ArgumentNullException(nameof(onReliableEventsPushed));
            if (onConnectionClosed == null) throw new ArgumentNullException(nameof(onConnectionClosed));
            if (onConnectionEstablished == null) throw new ArgumentNullException(nameof(onConnectionEstablished));

            Dispose();
            var generation = ++_generation;
            var checkpointAccepted = true;
            try
            {
                Transport = transport;
                var controllerContext = new MobaSyncControllerContext(tickRate);
                var capabilities = NetworkSyncCapabilities.FromProfile(
                    in SyncProfile,
                    minimumSchemaVersion: 0,
                    maximumSchemaVersion: GatewayStateSyncSnapshot.CurrentSchemaVersion);
                var syncBinding = RoomGatewayNetworkSyncSessionBinding.Create(
                    remoteCapabilities,
                    SyncProfileName);
                var sessionOptions = new NetworkSyncSessionOptions
                {
                    ProfileCatalog = SyncProfileCatalog,
                    RequiredProfileName = SyncProfileName,
                    RequiredMinimumSchemaVersion = 0,
                    RequiredMaximumSchemaVersion = GatewayStateSyncSnapshot.CurrentSchemaVersion,
                    AvailableCapabilities = capabilities,
                    ControllerSubjectName = "MOBA 客户端权威插值控制器"
                };
                syncBinding.ApplyTo(sessionOptions);
                var session = new NetworkSyncSessionBuilder<MobaClientAuthoritativeInterpolationSyncController, MobaSyncControllerContext>(
                    SyncControllerRegistry,
                    sessionOptions).Build(in controllerContext);
                InterpolationController = session.Controller;
                SyncSession = session.Descriptor;
                SyncBinding = syncBinding;
                ReplicationPipeline = new MobaClientReplicationPipeline(InterpolationController);
                SynchronizationHealthEvaluator = new MobaSynchronizationHealthEvaluator();
                SnapshotAdmission = new MobaSnapshotAdmission();
                SnapshotAdmission.Reset(roomId);
                AuthoritativeSnapshotState = new MobaAuthoritativeSnapshotState();
                ReliableEventCursor = new MobaReliableBattleEventCursor(battleId ?? string.Empty);
                checkpointAccepted = !reliableEventCheckpoint.IsValid ||
                    ReliableEventCursor.TryRestore(in reliableEventCheckpoint);
                PendingStateImport = true;

                _snapshotPushed = value =>
                {
                    if (IsCurrent(generation, transport)) onSnapshotPushed(value);
                };
                _reliableEventsPushed = value =>
                {
                    if (IsCurrent(generation, transport)) onReliableEventsPushed(value);
                };
                _connectionClosed = () =>
                {
                    if (IsCurrent(generation, transport)) onConnectionClosed();
                };
                _connectionEstablished = () =>
                {
                    if (IsCurrent(generation, transport)) onConnectionEstablished();
                };
                _authenticationFailed = ex =>
                {
                    if (IsCurrent(generation, transport)) onAuthenticationFailed?.Invoke(ex);
                };
                _beforeInputSubmissionStatsBind?.Invoke();
                _inputSubmissionStats = new InputSubmissionStatsSnapshot();
                InputSubmissionStatsProvider.Current = _inputSubmissionStats;
                _submitInputCompleted = response =>
                {
                    if (!IsCurrent(generation, transport)) return;
                    lock (_inputSubmissionStatsGate)
                    {
                        var previous = _inputSubmissionStats;
                        var snapshot = new InputSubmissionStatsSnapshot
                        {
                            CompletedCount = previous.CompletedCount + 1,
                            AcceptedCount = previous.AcceptedCount + (response.Accepted ? 1 : 0),
                            RejectedCount = previous.RejectedCount + (response.Accepted ? 0 : 1),
                            FailedCount = previous.FailedCount,
                            LastServerFrame = response.ServerFrame,
                            LastAcceptedFrame = response.AcceptedFrame,
                            LastReasonCode = response.ReasonCode,
                            LastShouldResync = response.ShouldResync,
                            LastStatus = response.Status,
                            LastMessage = response.Message,
                            LastFailure = previous.LastFailure
                        };
                        _inputSubmissionStats = snapshot;
                        InputSubmissionStatsProvider.Current = snapshot;
                    }
                };
                _submitInputFailed = ex =>
                {
                    if (!IsCurrent(generation, transport)) return;
                    lock (_inputSubmissionStatsGate)
                    {
                        var previous = _inputSubmissionStats;
                        var snapshot = new InputSubmissionStatsSnapshot
                        {
                            CompletedCount = previous.CompletedCount,
                            AcceptedCount = previous.AcceptedCount,
                            RejectedCount = previous.RejectedCount,
                            FailedCount = previous.FailedCount + 1,
                            LastServerFrame = previous.LastServerFrame,
                            LastAcceptedFrame = previous.LastAcceptedFrame,
                            LastReasonCode = previous.LastReasonCode,
                            LastShouldResync = previous.LastShouldResync,
                            LastStatus = previous.LastStatus,
                            LastMessage = previous.LastMessage,
                            LastFailure = ex?.ToString() ?? string.Empty
                        };
                        _inputSubmissionStats = snapshot;
                        InputSubmissionStatsProvider.Current = snapshot;
                    }
                };
                _getReliableEventEpoch = () =>
                    IsCurrent(generation, transport)
                        ? ReliableEventCursor?.Epoch ?? string.Empty
                        : string.Empty;
                _getReliableEventLastAcknowledgedSequence = () =>
                    IsCurrent(generation, transport)
                        ? ReliableEventCursor?.LastAcknowledgedSequence ?? 0L
                        : 0L;
                _submitInputAck = serverFrame =>
                {
                    if (!IsCurrent(generation, transport)) return;
                    LastServerAckFrame = serverFrame;
                    ReplicationPipeline?.AcknowledgeInput(serverFrame);
                };

                var options = transport.Options;
                _previousGetReliableEventEpoch = options.GetReliableEventEpoch;
                _previousGetReliableEventLastAcknowledgedSequence =
                    options.GetReliableEventLastAcknowledgedSequence;
                _previousSubmitInputAck = options.OnSubmitInputAck;
                options.GetReliableEventEpoch = _getReliableEventEpoch;
                options.GetReliableEventLastAcknowledgedSequence =
                    _getReliableEventLastAcknowledgedSequence;
                options.OnSubmitInputAck = _submitInputAck;

                transport.StateSyncSnapshotPushed += _snapshotPushed;
                transport.ReliableEventsPushed += _reliableEventsPushed;
                transport.ConnectionClosed += _connectionClosed;
                transport.ConnectionEstablished += _connectionEstablished;
                transport.AuthenticationFailed += _authenticationFailed;
                transport.SubmitInputCompleted += _submitInputCompleted;
                transport.SubmitInputFailed += _submitInputFailed;
                return checkpointAccepted;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

#if UNITY_5_3_OR_NEWER
        internal void TickPresentation(
            BattleContext context,
            bool enableClientPrediction,
            float deltaTime)
        {
            if (InterpolationController == null || ReplicationPipeline == null || context == null)
            {
                return;
            }

            ReplicationPipeline.Tick(deltaTime);
            TickSynchronizationHealth(context, deltaTime);

            if (!InterpolationController.TryProjectRemoteFrame(out var projected)) return;

            var localActorId = BattleRemoteInterpolationApplier.ResolveExcludedLocalActorId(
                enableClientPrediction,
                context.LocalActorId);
            BattleRemoteInterpolationApplier.Apply(context, in projected, localActorId);
        }
#endif

        public void Dispose()
        {
            _generation++;
            var transport = Transport;
            Transport = null;
            if (transport != null)
            {
                if (_snapshotPushed != null)
                    transport.StateSyncSnapshotPushed -= _snapshotPushed;
                if (_reliableEventsPushed != null)
                    transport.ReliableEventsPushed -= _reliableEventsPushed;
                if (_connectionClosed != null)
                    transport.ConnectionClosed -= _connectionClosed;
                if (_connectionEstablished != null)
                    transport.ConnectionEstablished -= _connectionEstablished;
                if (_authenticationFailed != null)
                    transport.AuthenticationFailed -= _authenticationFailed;
                if (_submitInputCompleted != null)
                    transport.SubmitInputCompleted -= _submitInputCompleted;
                if (_submitInputFailed != null)
                    transport.SubmitInputFailed -= _submitInputFailed;

                var options = transport.Options;
                if (ReferenceEquals(options.GetReliableEventEpoch, _getReliableEventEpoch))
                    options.GetReliableEventEpoch = _previousGetReliableEventEpoch;
                if (ReferenceEquals(
                        options.GetReliableEventLastAcknowledgedSequence,
                        _getReliableEventLastAcknowledgedSequence))
                {
                    options.GetReliableEventLastAcknowledgedSequence =
                        _previousGetReliableEventLastAcknowledgedSequence;
                }
                if (ReferenceEquals(options.OnSubmitInputAck, _submitInputAck))
                    options.OnSubmitInputAck = _previousSubmitInputAck;
            }

            InterpolationController?.Reset();
            ReplicationPipeline?.ResetDiagnostics();
            SynchronizationHealthEvaluator?.Reset();
            AuthoritativeSnapshotState?.Reset();
            ReliableEventCursor?.Reset();
            PendingReliableEventBatches.Clear();

            InterpolationController = null;
            SyncSession = null;
            SyncBinding = default;
            ReplicationPipeline = null;
            SynchronizationHealthEvaluator = null;
            SynchronizationHealth = default;
            SynchronizationHealthReport = SyncHealthReport.Empty;
            SynchronizationHealthSampleElapsed = 0f;
            SnapshotAdmission = null;
            AuthoritativeSnapshotState = null;
            ReliableEventCursor = null;
            LastServerAckFrame = 0;
            PendingStateImport = false;
            if (ReferenceEquals(InputSubmissionStatsProvider.Current, _inputSubmissionStats))
                InputSubmissionStatsProvider.Current = null;
            _snapshotPushed = null;
            _reliableEventsPushed = null;
            _connectionClosed = null;
            _connectionEstablished = null;
            _authenticationFailed = null;
            _submitInputCompleted = null;
            _submitInputFailed = null;
            _inputSubmissionStats = null;
            _getReliableEventEpoch = null;
            _getReliableEventLastAcknowledgedSequence = null;
            _submitInputAck = null;
            _previousGetReliableEventEpoch = null;
            _previousGetReliableEventLastAcknowledgedSequence = null;
            _previousSubmitInputAck = null;
        }

#if UNITY_5_3_OR_NEWER
        private void TickSynchronizationHealth(BattleContext context, float deltaTime)
        {
            if (SynchronizationHealthEvaluator == null ||
                InterpolationController == null ||
                ReplicationPipeline == null)
            {
                return;
            }

            SynchronizationHealthSampleElapsed += Math.Max(0f, deltaTime);
            if (SynchronizationHealthSampleElapsed < 0.5f) return;
            SynchronizationHealthSampleElapsed = 0f;

            var replication = ReplicationPipeline.GetDiagnostics();
            SynchronizationHealthReport = replication.Health;
            var interpolation = InterpolationController.GetInterpolationDiagnostics();
            var prediction = context.PredictionStats;
            var tuning = context.PredictionTuningControl;
            var sample = new MobaSynchronizationHealthSample(
                PendingStateImport || replication.Reconciliation.NeedsFullSnapshot,
                replication.UnacknowledgedInputFrames,
                Math.Max(0, replication.LastObservedFrame - replication.LastTick.Frame),
                interpolation.IsRemotePlaybackStarved,
                interpolation.BufferedRemoteSnapshotCount,
                interpolation.PlaybackDelayTicks,
                prediction?.CurrentBacklogEwma ?? 0f,
                prediction?.IsPredictionStalledByWindow ?? false,
                prediction?.IsPredictionStalledByIdealFrame ?? false,
                prediction?.IsReplaying ?? false,
                prediction?.TotalRollbackCount ?? 0L,
                prediction?.TotalRollbackRestoreFailed ?? 0L,
                prediction?.TotalReplayTimeout ?? 0L,
                prediction?.TotalReconcileMismatch ?? 0L,
                tuning?.MaxPredictionAheadFrames ?? prediction?.MaxPredictionAheadFrames ?? 6,
                tuning?.MinPredictionWindow ?? prediction?.MinPredictionWindow ?? 2,
                tuning?.BacklogEwmaAlpha ?? prediction?.BacklogEwmaAlpha ?? 0.2f);

            SynchronizationHealth = SynchronizationHealthEvaluator.Evaluate(in sample);
            ApplySynchronizationTuning(tuning, SynchronizationHealth.Tuning);
        }

        private static void ApplySynchronizationTuning(
            AbilityKit.Ability.Host.Extensions.FrameSync.IClientPredictionTuningControl tuning,
            MobaPredictionTuningRecommendation recommendation)
        {
            if (tuning == null || !recommendation.ShouldApply) return;
            if (recommendation.ResetDefaults)
            {
                tuning.ResetDefaults();
                return;
            }

            tuning.SetMaxPredictionAheadFrames(recommendation.MaxPredictionAheadFrames);
            tuning.SetMinPredictionWindow(recommendation.MinPredictionWindow);
            tuning.SetBacklogEwmaAlpha(recommendation.BacklogEwmaAlpha);
        }
#endif

        private static NetworkSyncProfileCatalog CreateSyncProfileCatalog()
        {
            var catalog = NetworkSyncProfileRegistry.CreateMutableCatalog();
            catalog.Register(SyncProfileName, SyncProfile);
            catalog.Freeze();
            return catalog;
        }

        private static NetworkSyncProfileControllerRegistry<MobaClientAuthoritativeInterpolationSyncController, MobaSyncControllerContext>
            CreateSyncControllerRegistry()
        {
            return new NetworkSyncProfileControllerRegistry<MobaClientAuthoritativeInterpolationSyncController, MobaSyncControllerContext>(
                new Dictionary<NetworkSyncProfile, NetworkSyncProfileControllerBuilder<MobaClientAuthoritativeInterpolationSyncController, MobaSyncControllerContext>>
                {
                    [SyncProfile] = CreateSyncController
                });
        }

        private static MobaClientAuthoritativeInterpolationSyncController CreateSyncController(
            in MobaSyncControllerContext context)
        {
            return new MobaClientAuthoritativeInterpolationSyncController(
                MobaRemoteInterpolationPlayback.CreateFrameTimelineConfig(context.TickRate));
        }

        private bool IsCurrent(int generation, NetworkTransport transport)
        {
            return generation == _generation && ReferenceEquals(Transport, transport);
        }

        /// <summary>MOBA 同步控制器装配所需的最小上下文。</summary>
        private readonly struct MobaSyncControllerContext
        {
            public MobaSyncControllerContext(int tickRate)
            {
                TickRate = tickRate;
            }

            public int TickRate { get; }
        }
    }
}
