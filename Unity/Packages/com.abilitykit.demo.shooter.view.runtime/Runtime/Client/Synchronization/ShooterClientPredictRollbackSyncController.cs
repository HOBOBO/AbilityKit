#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Protocol.Shooter;

namespace AbilityKit.Demo.Shooter.View
{
    /// <summary>
    /// <see cref="NetworkSyncModel.PredictRollback"/> 客户端控制器。
    /// 将现有本地预测、权威快照、回滚与重放链路
    /// （<see cref="ShooterClientFrameSyncController"/> + <see cref="ShooterClientInputCoordinator"/>）
    /// 包装到通用 <see cref="IShooterClientSyncController"/> 接缝之后，让会话无需了解当前同步模型即可委托执行。
    /// </summary>
    public sealed class ShooterClientPredictRollbackSyncController : IShooterClientSyncController
    {
        private readonly ShooterClientSyncCore _core;

        public ShooterClientPredictRollbackSyncController(
            IShooterBattleRuntimePort runtime,
            ShooterPresentationFacade presentation,
            int tickRate,
            ShooterGatewaySnapshotDecoder? decoder,
            IShooterRoomGatewayClient? gateway)
        {
            _core = new ShooterClientSyncCore(runtime, presentation, tickRate, decoder, gateway);
        }

        public NetworkSyncModel SyncModel => NetworkSyncModel.PredictRollback;

        public bool IsStarted => _core.IsStarted;

        public int CurrentFrame => _core.CurrentFrame;

        public int GatewayInputFrame => CurrentFrame;

        public ShooterClientFrameSyncController FrameSync => _core.FrameSync;

        public ShooterClientInputCoordinator InputCoordinator => _core.InputCoordinator;

        public ShooterFrameworkSnapshotPipelineDiagnostics FrameworkSnapshotPipelineDiagnostics => _core.FrameworkSnapshotPipelineDiagnostics;

        public ShooterClientReconciliationResult LastReconciliationResult => _core.LastReconciliationResult;

        public bool NeedsFullSnapshotResync => _core.NeedsFullSnapshotResync;

        public ShooterClientRecoveryState RecoveryState => _core.RecoveryState;

        public AbilityKit.Network.Runtime.Sync.FastReconnectPhase FastReconnectPhase => _core.FastReconnectPhase;

        public IReadOnlyList<SyncHealthEvent> LastFastReconnectHealthEvents => _core.LastFastReconnectHealthEvents;

        public ShooterClientResyncReason LastResyncReason => _core.LastResyncReason;

        public int LastResyncClientFrame => _core.LastResyncClientFrame;

        public int LastResyncAuthoritativeFrame => _core.LastResyncAuthoritativeFrame;

        public uint LastResyncClientStateHash => _core.LastResyncClientStateHash;

        public uint LastResyncAuthoritativeStateHash => _core.LastResyncAuthoritativeStateHash;

        public bool HasGateway => _core.HasGateway;

        public bool StartGame(in ShooterStartGamePayload startGame)
        {
            return _core.StartGame(in startGame);
        }

        public ShooterClientInputSubmitResult SubmitLocalInput(int playerId, float moveX, float moveY, float aimX, float aimY, bool fire)
        {
            return _core.SubmitLocalInput(playerId, moveX, moveY, aimX, aimY, fire);
        }

        public ShooterClientInputSubmitResult SubmitLocalInput(in ShooterPlayerCommand command)
        {
            return _core.SubmitLocalInput(in command);
        }

        public Task<ShooterClientGatewayInputSubmitResult> SubmitLocalInputToGatewayAsync(
            ShooterGatewayBattleInputContext context,
            ShooterPlayerCommand command,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            return _core.SubmitLocalInputToGatewayAsync(context, command, timeout, cancellationToken);
        }

        public Task<ShooterClientGatewayInputSubmitResult> SubmitAcceptedInputToGatewayAsync(
            ShooterGatewayBattleInputContext context,
            ShooterClientInputSubmitResult local,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            return _core.SubmitAcceptedInputToGatewayAsync(context, local, timeout, cancellationToken);
        }

        public ShooterClientFrameTickResult Tick(float deltaTime)
        {
            return _core.Tick(deltaTime);
        }

        public ShooterClientFrameTickResult CatchUpToFrame(int targetFrame)
        {
            return _core.CatchUpToFrame(targetFrame);
        }

        public bool TryEnterCatchUp(int authoritativeFrame)
        {
            return _core.TryEnterCatchUp(authoritativeFrame);
        }

        public ShooterSnapshotApplyResult ApplyGatewayPush(uint opCode, ArraySegment<byte> payload)
        {
            return _core.ApplyGatewayPush(opCode, payload);
        }

        // --- IClientSyncStrategy<ShooterPlayerCommand, ShooterRemoteSnapshotSample> ---
        // 显式框架契约接口，映射到现有示例行为。

        SyncTickResult IClientSyncStrategy<ShooterPlayerCommand, ShooterRemoteSnapshotSample>.Tick(float deltaSeconds)
        {
            return ShooterClientSyncStrategyMapping.ToSyncTickResult(Tick(deltaSeconds));
        }

        void IClientSyncStrategy<ShooterPlayerCommand, ShooterRemoteSnapshotSample>.SubmitInput(in ShooterPlayerCommand input)
        {
            SubmitLocalInput(in input);
        }

        void IClientSyncStrategy<ShooterPlayerCommand, ShooterRemoteSnapshotSample>.ObserveRemote(in ShooterRemoteSnapshotSample sample)
        {
            // 预测回滚不消费逐 actor 远端样本：它通过 ApplyGatewayPush 导入打包权威快照字节，
            // 再回滚/前滚本地模拟完成校正。因此对该模型来说，观察已解码样本是空操作。
        }

        SyncReconciliationReport IClientSyncStrategy<ShooterPlayerCommand, ShooterRemoteSnapshotSample>.GetReconciliationReport()
        {
            return ShooterClientSyncStrategyMapping.ToReconciliationReport(this);
        }
    }
}
