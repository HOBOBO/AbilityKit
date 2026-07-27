#nullable enable

using System;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Protocol.Shooter;

namespace AbilityKit.Demo.Shooter.View
{
    public sealed class ShooterClientFrameSyncCoordinator
    {
        private readonly IShooterBattleRuntimePort _runtime;
        private readonly ShooterPresentationFacade _presentation;
        private readonly ShooterClientFrameSyncController _controller;

        public ShooterClientFrameSyncCoordinator(IShooterBattleRuntimePort runtime, ShooterPresentationFacade presentation, int tickRate)
            : this(runtime, presentation, tickRate, null)
        {
        }

        public ShooterClientFrameSyncCoordinator(IShooterBattleRuntimePort runtime, ShooterPresentationFacade presentation, int tickRate, ShooterGatewaySnapshotDecoder? decoder)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
            _controller = new ShooterClientFrameSyncController(_runtime, _presentation, tickRate, decoder);
        }

        public bool IsStarted => _runtime.IsStarted;

        public int CurrentFrame => _controller.CurrentFrame;

        public ShooterClientFrameSyncController Controller => _controller;

        public ShooterFrameworkSnapshotPipelineDiagnostics FrameworkSnapshotPipelineDiagnostics => _controller.FrameworkSnapshotPipelineDiagnostics;

        public ShooterClientReconciliationResult LastReconciliationResult => _controller.LastReconciliationResult;

        public bool NeedsFullSnapshotResync => _controller.NeedsFullSnapshotResync;

        public ShooterClientRecoveryState RecoveryState => _controller.RecoveryState;

        public AbilityKit.Network.Runtime.Sync.FastReconnectPhase FastReconnectPhase => _controller.FastReconnectPhase;

        public System.Collections.Generic.IReadOnlyList<AbilityKit.Network.Runtime.Sync.SyncHealthEvent> LastFastReconnectHealthEvents
            => _controller.LastFastReconnectHealthEvents;

        public ShooterClientResyncReason LastResyncReason => _controller.LastResyncReason;

        public int LastResyncClientFrame => _controller.LastResyncClientFrame;

        public int LastResyncAuthoritativeFrame => _controller.LastResyncAuthoritativeFrame;

        public uint LastResyncClientStateHash => _controller.LastResyncClientStateHash;

        public uint LastResyncAuthoritativeStateHash => _controller.LastResyncAuthoritativeStateHash;

        public bool StartGame(in ShooterStartGamePayload startGame)
        {
            if (!_runtime.StartGame(in startGame))
            {
                return false;
            }

            PublishRuntimeSnapshot();
            return true;
        }

        public int SubmitLocalInput(in ShooterPlayerCommand command)
        {
            return _controller.SubmitLocalInput(in command);
        }

        public ShooterClientFrameTickResult Tick(float deltaTime)
        {
            return _controller.Tick(deltaTime);
        }

        public ShooterClientFrameTickResult CatchUpToFrame(int targetFrame)
        {
            return _controller.CatchUpToFrame(targetFrame);
        }

        public bool TryEnterCatchUp(int authoritativeFrame)
        {
            return _controller.TryEnterCatchUp(authoritativeFrame);
        }

        public void MarkGatewayInputResyncRequested(int clientFrame, int authoritativeFrame, uint clientStateHash = 0u, uint authoritativeStateHash = 0u)
        {
            _controller.MarkGatewayInputResyncRequested(clientFrame, authoritativeFrame, clientStateHash, authoritativeStateHash);
        }

        public ShooterSnapshotApplyResult ApplyGatewayPush(uint opCode, ArraySegment<byte> payload)
        {
            return _controller.ApplyGatewayPush(opCode, payload);
        }

        private void PublishRuntimeSnapshot()
        {
            var snapshot = _runtime.GetSnapshotTransient();
            _presentation.ApplyLocalPredictionSnapshot(in snapshot);
        }
    }
}
