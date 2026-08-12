using System;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Network.Battle.Projection;
using AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// Owns the mutable simulation resources for one battle session while the feature remains
    /// the compatibility facade for session composition and presentation callbacks.
    /// </summary>
    internal sealed class BattleSimulationRuntime
    {
        private readonly BattleSessionState _state;
        private readonly BattleSessionHandles _handles;
        private readonly IBattleSessionWorldInstaller _worldInstaller;
        private readonly BattlePresentationSessionResources _presentation;
        private readonly BattleSessionDiagnostics _diagnostics;
        private PredictionViewBridge _predictionViewBridge;

        internal BattleSimulationRuntime(
            BattleSessionState state,
            BattleSessionHandles handles,
            IBattleSessionWorldInstaller worldInstaller)
            : this(
                state,
                handles,
                worldInstaller,
                new BattlePresentationSessionResources(),
                new BattleSessionDiagnostics(new BattleReplicationRuntime()))
        {
        }

        internal BattleSimulationRuntime(
            BattleSessionState state,
            BattleSessionHandles handles,
            IBattleSessionWorldInstaller worldInstaller,
            BattlePresentationSessionResources presentation)
            : this(
                state,
                handles,
                worldInstaller,
                presentation,
                new BattleSessionDiagnostics(new BattleReplicationRuntime()))
        {
        }

        internal BattleSimulationRuntime(
            BattleSessionState state,
            BattleSessionHandles handles,
            IBattleSessionWorldInstaller worldInstaller,
            BattlePresentationSessionResources presentation,
            BattleSessionDiagnostics diagnostics)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _handles = handles ?? throw new ArgumentNullException(nameof(handles));
            _worldInstaller = worldInstaller ?? throw new ArgumentNullException(nameof(worldInstaller));
            _presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
            _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        }

        internal BattleSessionRemoteDrivenWorldRuntime RemoteDriven => _handles.RemoteDriven;
        internal BattleSessionConfirmedWorldRuntime Confirmed => _handles.Confirmed;

        internal int RemoteDrivenLastTickedFrame
        {
            get => _state.RemoteDriven.LastTickedFrame;
            set => _state.RemoteDriven.LastTickedFrame = value;
        }

        internal int ConfirmedLastTickedFrame
        {
            get => _state.Confirmed.LastTickedFrame;
            set => _state.Confirmed.LastTickedFrame = value;
        }

        internal void StartRemoteDriven(
            BattleStartPlan plan,
            BattleContext context,
            float fixedDeltaSeconds,
            Func<WorldId, int> resolveIdealFrameLimit,
            Func<bool> shouldForceHashMismatch)
        {
            var wasStarted = RemoteDriven.World != null;
            try
            {
                _worldInstaller.EnsureRemoteDrivenStarted(new RemoteDrivenWorldInstallOptions(
                    plan,
                    context,
                    RemoteDriven,
                    _diagnostics,
                    fixedDeltaSeconds,
                    resolveIdealFrameLimit,
                    shouldForceHashMismatch,
                    () => RemoteDrivenLastTickedFrame = 0));
            }
            catch (Exception startFailure) when (!wasStarted)
            {
                RollbackRemoteDrivenStart(plan, startFailure);
            }
        }

        internal void StartConfirmedAuthority(
            BattleStartPlan plan,
            BattleContext context,
            GameFlowDomain flow,
            bool hasSession,
            float fixedDeltaSeconds,
            Func<WorldId, int> resolveIdealFrameLimit,
            Action<IEntity> destroyEntityTree)
        {
            var wasStarted = Confirmed.World != null;
            try
            {
                _worldInstaller.EnsureConfirmedAuthorityStarted(new ConfirmedAuthorityWorldInstallOptions(
                    plan,
                    context,
                    flow,
                    Confirmed,
                    _diagnostics,
                    hasSession,
                    fixedDeltaSeconds,
                    resolveIdealFrameLimit,
                    () => ConfirmedLastTickedFrame = 0));

                _presentation.EnsureConfirmedViewInstalled(
                    context,
                    flow,
                    ConfirmedAuthorityWorldId.Create(plan),
                    ConfirmedViewSideInstaller.ShouldRenderConfirmedView(plan),
                    destroyEntityTree);
            }
            catch (Exception startFailure) when (!wasStarted)
            {
                RollbackConfirmedStart(plan, context, flow, destroyEntityTree, startFailure);
            }
        }

        internal void TickRemoteDriven(
            BattleStartPlan plan,
            BattleContext context,
            SessionWorldCatchUpController worldCatchUp,
            FrameSnapshotDispatcher snapshots,
            float fixedDeltaSeconds,
            int lastServerAckFrame)
        {
            RemoteDrivenLastTickedFrame = RemoteDrivenWorldTickDriver.Tick(new RemoteDrivenWorldTickOptions(
                plan,
                RemoteDriven,
                worldCatchUp,
                snapshots,
                RemoteDrivenLastTickedFrame,
                fixedDeltaSeconds,
                SessionSimRuntimeTuning.MaxCatchUpStepsPerUpdate,
                lastServerAckFrame));

            if (!plan.Authority.EnableClientPrediction || context == null) return;

            var world = RemoteDriven.World;
            if (world?.Services == null) return;
            if (!world.Services.TryResolve<IActorProjectionProducer>(out var producer) || producer == null) return;

            _predictionViewBridge ??= new PredictionViewBridge(context.EntityWorld, context.EntityLookup);
            _predictionViewBridge.SyncLocalPlayer(producer, context.LocalActorId);
        }

        internal void TickConfirmedAuthority(
            BattleStartPlan plan,
            BattleContext context,
            SessionWorldCatchUpController worldCatchUp,
            float fixedDeltaSeconds)
        {
            ConfirmedLastTickedFrame = ConfirmedAuthorityWorldTickDriver.Tick(
                new ConfirmedAuthorityWorldTickOptions(
                    plan,
                    context,
                    Confirmed,
                    _diagnostics,
                    _presentation.ConfirmedSnapshots,
                    worldCatchUp,
                    ConfirmedLastTickedFrame,
                    fixedDeltaSeconds,
                    SessionSimRuntimeTuning.MaxCatchUpStepsPerUpdate));
        }

        internal void DestroyBattleWorlds(BattleStartPlan plan)
        {
            SessionSimRuntimeDisposer.DestroyBattleWorlds(
                () => RemoteDriven.DestroyWorld(new WorldId(plan.World.WorldId)),
                () => Confirmed.DestroyWorld(ConfirmedAuthorityWorldId.Create(plan)));
        }

        internal void DisposeConfirmedView(GameFlowDomain flow, Action<IEntity> destroyEntityTree)
        {
            _presentation.DisposeConfirmedView(flow, destroyEntityTree);
        }

        internal void DisposeRemoteDrivenWorld()
        {
            try
            {
                SessionSimRuntimeDisposer.DisposeRemoteDrivenWorld(
                    RemoteDriven,
                    () => RemoteDrivenLastTickedFrame = 0);
            }
            finally
            {
                _predictionViewBridge = null;
            }
        }

        internal void DisposeConfirmedWorld(BattleContext context)
        {
            SessionSimRuntimeDisposer.DisposeConfirmedWorld(
                context,
                Confirmed,
                _diagnostics,
                () => ConfirmedLastTickedFrame = 0);
        }

        private void RollbackRemoteDrivenStart(BattleStartPlan plan, Exception startFailure)
        {
            try
            {
                SessionSimRuntimeDisposer.ExecuteCleanupSteps(
                    "Failed to roll back remote-driven world startup.",
                    () => RemoteDriven.DestroyWorld(new WorldId(plan.World.WorldId)),
                    DisposeRemoteDrivenWorld);
            }
            catch (Exception cleanupFailure)
            {
                throw new AggregateException(
                    "Remote-driven world startup failed and rollback was incomplete.",
                    startFailure,
                    cleanupFailure);
            }

            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(startFailure).Throw();
        }

        private void RollbackConfirmedStart(
            BattleStartPlan plan,
            BattleContext context,
            GameFlowDomain flow,
            Action<IEntity> destroyEntityTree,
            Exception startFailure)
        {
            try
            {
                SessionSimRuntimeDisposer.ExecuteCleanupSteps(
                    "Failed to roll back confirmed-authority world startup.",
                    () => DisposeConfirmedView(flow, destroyEntityTree),
                    () => Confirmed.DestroyWorld(ConfirmedAuthorityWorldId.Create(plan)),
                    () => DisposeConfirmedWorld(context));
            }
            catch (Exception cleanupFailure)
            {
                throw new AggregateException(
                    "Confirmed-authority world startup failed and rollback was incomplete.",
                    startFailure,
                    cleanupFailure);
            }

            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(startFailure).Throw();
        }
    }
}
