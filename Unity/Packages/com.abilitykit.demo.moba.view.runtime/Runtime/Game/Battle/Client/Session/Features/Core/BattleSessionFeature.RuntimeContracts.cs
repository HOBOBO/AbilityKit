using System;
using System.Threading.Tasks;
using AbilityKit.Ability.Host;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Flow.Battle.Modules;
using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    internal interface ISessionRuntimeFeature
    {
        ISessionPlanHost PlanHost { get; }
        ISessionEventsHost EventsHost { get; }
    }

    internal interface ISessionDispatchersRuntime
    {
        void AttachDispatchers();
        void DetachDispatchers();
    }

    internal interface ISessionEventsRuntime
    {
        SessionEventsController Events { get; }
    }

    internal interface ISessionPlanRuntime
    {
        SessionStartupTaskOwner StartupTasks { get; }
        Task AttachPlanAsync(Func<bool> isCurrent);
    }

    internal interface ISessionReplayRuntime
    {
        void SetupReplayOrRecord();
        void OnReplayFrameReceived(AbilityKit.Ability.Host.FramePacket packet);
    }

    internal interface ISessionNetAdapterRuntime
    {
        AbilityKit.Ability.Host.FramePacket TransformFramePacket(AbilityKit.Ability.Host.FramePacket packet);
    }

    internal interface ISessionTickLoopRuntime
    {
        void TickSession(float deltaTime);
    }

    internal interface ISessionLifecycleRuntime
    {
        BattleSessionHooks Hooks { get; }
    }

    internal interface ISessionGatewayRuntime
    {
        BattleSessionHooks Hooks { get; }
        bool HasGatewayRoomConnection { get; }
        Task GatewayRoomPreparationTask { get; }
        bool ShouldPrepareGatewayRoom();
        Task StartGatewayRoomPreparation();
        Task CompleteGatewayRoomPreparationAsync();
        Task StopGatewayRoomPreparationAsync();
        void TickGatewayRoomConnection(float deltaTime);
        void OnStartSessionRequested();
        void NotifySessionFailed(Exception exception);
    }

    internal interface ISessionSnapshotRoutingRuntime
    {
        BattleSessionHooks Hooks { get; }
        void EnsureSnapshotRoutingBuilt();
        void DisposeSnapshotRoutingIfAny();
    }

#if UNITY_EDITOR
    internal interface ISessionEditorHooksRuntime
    {
        void TryInstallEditorPlayModeStopHook();
        void TryUninstallEditorPlayModeStopHook();
    }
#endif

    internal static class BattleSessionFeatureRuntimeAccess
    {
        public static T Require<T>(in FeatureModuleContext<ISessionRuntimeFeature> ctx, string moduleId)
            where T : class
        {
            if (ctx.Feature is T runtime) return runtime;
            throw new InvalidOperationException(
                $"Session module '{moduleId}' requires runtime capability {typeof(T).Name}.");
        }

        public static bool TryGet<T>(in FeatureModuleContext<ISessionRuntimeFeature> ctx, out T runtime)
            where T : class
        {
            runtime = ctx.Feature as T;
            return runtime != null;
        }
    }
}
