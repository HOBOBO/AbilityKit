using System;
using AbilityKit.Core.Recording.FrameRecord;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Flow.Battle.Replay;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Runtime.Conditioning;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// Owns the mutable state and runtime resources of one battle session.
    /// The feature remains a compatibility facade during the staged migration.
    /// </summary>
    internal sealed class BattleReplayRuntime : IDisposable
    {
        private readonly BattleReplaySessionOwner _sessionOwner;
        private IFrameRecordWriter _recordWriter;
        private BattleContext _recordContext;
        private long _recordContextBindingGeneration;

        internal BattleReplayRuntime(IBattleLogicSessionRegistry registry = null)
            : this(new BattleReplaySessionOwner(registry))
        {
        }

        internal BattleReplayRuntime(BattleReplaySessionOwner sessionOwner)
        {
            _sessionOwner = sessionOwner ?? throw new ArgumentNullException(nameof(sessionOwner));
        }

        internal bool IsActive => _sessionOwner.IsActive;
        internal bool IsPlaying => _sessionOwner.IsPlaying;
        internal int CurrentFrame => _sessionOwner.CurrentFrame;
        internal int LastFrame => _sessionOwner.LastFrame;
        internal string ReplayPath => _sessionOwner.ReplayPath;
        internal Exception LastFailure => _sessionOwner.LastFailure;

        internal float PlaybackSpeed
        {
            get => _sessionOwner.PlaybackSpeed;
            set => _sessionOwner.PlaybackSpeed = value;
        }

        internal bool TryStart(BattleStartPlan plan, string path, out string error) =>
            _sessionOwner.TryStart(plan, path, out error);

        internal void Play() => _sessionOwner.Play();
        internal void Pause() => _sessionOwner.Pause();
        internal bool Tick(float deltaSeconds) => _sessionOwner.Tick(deltaSeconds);
        internal bool SeekToFrame(int frame) => _sessionOwner.SeekToFrame(frame);
        internal void Stop() => _sessionOwner.Stop();

        internal void BindRecordWriter(BattleContext context, IFrameRecordWriter writer)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            if (ReferenceEquals(_recordWriter, writer))
            {
                if (ReferenceEquals(_recordContext, context)) return;

                var previousContext = _recordContext;
                var previousGeneration = _recordContextBindingGeneration;
                _recordContext = context;
                _recordContextBindingGeneration = 0;
                previousContext?.ClearInputRecordWriter(previousGeneration, writer);
                if (context != null)
                {
                    _recordContextBindingGeneration = context.BindInputRecordWriter(writer);
                }
                return;
            }

            try
            {
                DisposeRecordWriter();
            }
            catch (Exception replacementFailure)
            {
                try
                {
                    writer.Dispose();
                }
                catch (Exception candidateCleanupFailure)
                {
                    throw new AggregateException(
                        "Failed to replace the replay record writer and dispose its candidate.",
                        replacementFailure,
                        candidateCleanupFailure);
                }

                throw;
            }

            _recordWriter = writer;
            _recordContext = context;
            _recordContextBindingGeneration = context?.BindInputRecordWriter(writer) ?? 0;
        }

        internal void DisposeRecordWriter()
        {
            var writer = _recordWriter;
            var context = _recordContext;
            var bindingGeneration = _recordContextBindingGeneration;
            if (writer == null)
            {
                _recordContext = null;
                _recordContextBindingGeneration = 0;
                return;
            }

            writer.Dispose();
            if (ReferenceEquals(_recordWriter, writer)) _recordWriter = null;
            if (ReferenceEquals(_recordContext, context))
            {
                _recordContext = null;
                _recordContextBindingGeneration = 0;
            }
            context?.ClearInputRecordWriter(bindingGeneration, writer);
        }

        public void Dispose()
        {
            SessionSimRuntimeDisposer.ExecuteCleanupSteps(
                "Failed to dispose replay runtime resources.",
                Stop,
                DisposeRecordWriter);
        }
    }

    internal sealed class BattleSessionRuntime
    {
        internal BattleSessionState State { get; }
        internal BattleSessionHandles Handles { get; }
        internal SessionOrchestrator Orchestrator { get; private set; }
        internal BattleSnapshotRoutingRuntime SnapshotRouting { get; }
        internal GatewaySessionRuntime GatewayRoom { get; private set; }
        internal BattleReplicationRuntime Replication { get; }
        internal ReliableBattleEventDeliveryRuntime ReliableEvents { get; private set; }
        internal AuthoritativeStateRecoveryRuntime Recovery { get; private set; }
        internal BattleSessionDiagnostics Diagnostics { get; }
        internal BattleInputRuntime Input { get; }
        internal BattlePredictionRuntime Prediction { get; }
        internal BattlePresentationSessionResources Presentation { get; }
        internal BattleAssetLeaseOwner Assets { get; }
        internal BattleReplayRuntime Replay { get; }
        internal SpectatorSessionRuntime Spectator { get; }
        internal BattleSimulationRuntime Simulation { get; private set; }

        internal BattleSessionRuntime()
        {
            State = new BattleSessionState();
            Handles = new BattleSessionHandles();
            Replication = new BattleReplicationRuntime();
            Diagnostics = new BattleSessionDiagnostics(Replication);
            Input = new BattleInputRuntime();
            Prediction = new BattlePredictionRuntime();
            SnapshotRouting = new BattleSnapshotRoutingRuntime(Handles, Diagnostics);
            Presentation = new BattlePresentationSessionResources();
            Assets = new BattleAssetLeaseOwner();
            Replay = new BattleReplayRuntime();
            Spectator = new SpectatorSessionRuntime();
        }

        internal void BindContext(BattleContext context)
        {
            context?.BindInputRuntime(Input);
            context?.BindPredictionRuntime(Prediction);
        }

        internal void UnbindContext(BattleContext context)
        {
            context?.UnbindPredictionRuntime(Prediction);
            context?.UnbindInputRuntime(Input);
        }

        internal void ConfigureSimulation(IBattleSessionWorldInstaller worldInstaller)
        {
            if (worldInstaller == null) throw new ArgumentNullException(nameof(worldInstaller));
            if (Simulation != null)
            {
                throw new InvalidOperationException("Battle session simulation has already been configured.");
            }

            Simulation = new BattleSimulationRuntime(
                State,
                Handles,
                worldInstaller,
                Presentation,
                Diagnostics);
            ReliableEvents = new ReliableBattleEventDeliveryRuntime();
            Recovery = new AuthoritativeStateRecoveryRuntime(
                Replication,
                ReliableEvents,
                new BattleAuthoritativeWorldRecoveryPort(Simulation, Handles));
        }

        internal void ConfigureGatewayRoom(
            IAbilityKitConnectionRegistry connectionRegistry,
            IBattleSessionGatewayConnectionFactory connectionFactory,
            IBattleSessionGatewayRoomClientFactory clientFactory,
            NetworkConditionController networkCondition)
        {
            if (GatewayRoom != null)
            {
                throw new InvalidOperationException("Battle session gateway room has already been configured.");
            }

            GatewayRoom = new GatewaySessionRuntime(
                Handles,
                connectionRegistry,
                connectionFactory,
                clientFactory,
                networkCondition);
        }

        internal void ConfigureOrchestrator(ISessionOrchestratorHost host)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (Orchestrator != null)
            {
                throw new InvalidOperationException("Battle session orchestrator has already been configured.");
            }

            Orchestrator = new SessionOrchestrator(State, Handles, host);
        }

        internal void DisposeReplication()
        {
            Recovery?.Dispose();
            Replication.Dispose();
        }
    }
}
