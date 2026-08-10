using System;
using System.Threading.Tasks;
using AbilityKit.Ability.Host;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Flow.Battle.Modules;
using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    internal interface ISessionDispatchersRuntime
    {
        BattleSessionHandles Handles { get; }
        SessionDispatchersController Dispatchers { get; }
    }

    internal interface ISessionEventsRuntime
    {
        SessionEventsController Events { get; }
    }

    internal interface ISessionPlanRuntime
    {
        IBattleBootstrapper Bootstrapper { get; }
        BattleSessionState State { get; }
        BattleSessionHandles Handles { get; }
        BattleContext Context { get; }
        BattleSessionHooks Hooks { get; }
        SessionPlanController PlanController { get; }
    }

    internal interface ISessionReplayRuntime
    {
        BattleStartPlan Plan { get; }
        BattleSessionState State { get; }
        BattleSessionHandles Handles { get; }
        BattleContext Context { get; }
        SessionReplayController Replay { get; }
    }

    internal interface ISessionNetAdapterRuntime
    {
        BattleLogicSession Session { get; }
        BattleSessionNetAdapter NetAdapter { get; }
        SessionNetAdapterController Net { get; }
    }

    internal interface ISessionTickLoopRuntime
    {
        TickLoopController TickLoop { get; }
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
        void StartGatewayRoomPreparation();
        void CompleteGatewayRoomPreparation();
        void StopGatewayRoomPreparation();
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
        public static bool TryGet<T>(in FeatureModuleContext<BattleSessionFeature> ctx, out T runtime)
            where T : class
        {
            runtime = ctx.Feature as T;
            return runtime != null;
        }
    }
}
