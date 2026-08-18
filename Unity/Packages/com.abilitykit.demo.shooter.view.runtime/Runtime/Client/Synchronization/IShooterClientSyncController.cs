#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Protocol.Shooter;

namespace AbilityKit.Demo.Shooter.View
{
    /// <summary>
    /// Optional frame-sync capability exposed by controllers that own a local frame timeline.
    /// </summary>
    public interface IShooterClientFrameSyncCapability
    {
        ShooterClientFrameSyncController FrameSync { get; }
    }

    /// <summary>
    /// Optional input coordination capability exposed by controllers that submit local commands.
    /// </summary>
    public interface IShooterClientInputCapability
    {
        ShooterClientInputCoordinator InputCoordinator { get; }
    }

    /// <summary>
    /// Capability lookup helpers keep optional controller features discoverable without putting
    /// concrete implementations on the base synchronization contract.
    /// </summary>
    public static class ShooterClientSyncControllerCapabilityExtensions
    {
        public static bool TryGetCapability<TCapability>(
            this IShooterClientSyncController controller,
            out TCapability? capability)
            where TCapability : class
        {
            if (controller == null) throw new ArgumentNullException(nameof(controller));

            capability = controller as TCapability;
            return capability != null;
        }

        public static bool TryGetFrameSync(
            this IShooterClientSyncController controller,
            out ShooterClientFrameSyncController? frameSync)
        {
            if (controller is IShooterClientFrameSyncCapability capability)
            {
                frameSync = capability.FrameSync;
                return true;
            }

            frameSync = null;
            return false;
        }

        public static bool TryGetInputCoordinator(
            this IShooterClientSyncController controller,
            out ShooterClientInputCoordinator? inputCoordinator)
        {
            if (controller is IShooterClientInputCapability capability)
            {
                inputCoordinator = capability.InputCoordinator;
                return true;
            }

            inputCoordinator = null;
            return false;
        }
    }

    /// <summary>
    /// 单个 Shooter <see cref="NetworkSyncModel"/> 的客户端同步控制器。
    /// 它承接 <see cref="ShooterClientSession"/> 门面委托的运行时行为：启动世界、推进帧、
    /// 提交本地输入、消费网关推送并驱动恢复。不同同步模型（预测回滚、权威插值、批量状态同步）
    /// 提供不同实现，而会话层保持模式无关。
    ///
    /// 从迁移步骤 3 起，这里也绑定框架的玩法无关 <see cref="IClientSyncStrategy{TInput, TSample}"/>
    /// 契约（使用 Shooter 的命令与远端样本类型），让示例同步模型能通过共享 A 轴抽象访问，
    /// 同时保留更丰富的示例侧接口。
    /// </summary>
    public interface IShooterClientSyncController
        : INetworkSyncController, IClientSyncStrategy<ShooterPlayerCommand, ShooterRemoteSnapshotSample>
    {
        int GatewayInputFrame { get; }

        ShooterFrameworkSnapshotPipelineDiagnostics FrameworkSnapshotPipelineDiagnostics { get; }

        ShooterClientReconciliationResult LastReconciliationResult { get; }

        bool NeedsFullSnapshotResync { get; }

        ShooterClientRecoveryState RecoveryState { get; }

        /// <summary>
        /// 内嵌 <see cref="FastReconnectSession"/> 从 <see cref="RecoveryState"/> 投影出的当前框架
        /// <see cref="FastReconnectPhase"/>（审计 §10.4：FastReconnect 的真实消费方）。
        /// </summary>
        FastReconnectPhase FastReconnectPhase { get; }

        /// <summary>
        /// 会话在最近一次恢复/心跳步骤中发出的框架 <see cref="SyncHealthEvent"/>，
        /// 用于转发到 DemoHarness 遥测（设计 §4.4.5）。
        /// </summary>
        System.Collections.Generic.IReadOnlyList<SyncHealthEvent> LastFastReconnectHealthEvents { get; }

        ShooterClientResyncReason LastResyncReason { get; }

        int LastResyncClientFrame { get; }

        int LastResyncAuthoritativeFrame { get; }

        uint LastResyncClientStateHash { get; }

        uint LastResyncAuthoritativeStateHash { get; }

        bool HasGateway { get; }

        bool StartGame(in ShooterStartGamePayload startGame);

        ShooterClientInputSubmitResult SubmitLocalInput(int playerId, float moveX, float moveY, float aimX, float aimY, bool fire);

        ShooterClientInputSubmitResult SubmitLocalInput(in ShooterPlayerCommand command);

        Task<ShooterClientGatewayInputSubmitResult> SubmitLocalInputToGatewayAsync(
            ShooterGatewayBattleInputContext context,
            ShooterPlayerCommand command,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);

        Task<ShooterClientGatewayInputSubmitResult> SubmitAcceptedInputToGatewayAsync(
            ShooterGatewayBattleInputContext context,
            ShooterClientInputSubmitResult local,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);

        new ShooterClientFrameTickResult Tick(float deltaTime);

        ShooterClientFrameTickResult CatchUpToFrame(int targetFrame);

        bool TryEnterCatchUp(int authoritativeFrame);

        ShooterSnapshotApplyResult ApplyGatewayPush(uint opCode, ArraySegment<byte> payload);
    }
}
