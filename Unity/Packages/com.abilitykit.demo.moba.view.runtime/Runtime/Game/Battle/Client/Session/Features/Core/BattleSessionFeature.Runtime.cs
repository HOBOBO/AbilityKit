using System.Threading.Tasks;
using AbilityKit.Ability.Host;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Flow.Battle.Modules;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature :
        ISessionDispatchersRuntime,
        ISessionEventsRuntime,
        ISessionPlanRuntime,
        ISessionReplayRuntime,
        ISessionNetAdapterRuntime,
        ISessionTickLoopRuntime,
        ISessionLifecycleRuntime,
        ISessionGatewayRuntime,
        ISessionSnapshotRoutingRuntime,
#if UNITY_EDITOR
        ISessionEditorHooksRuntime,
#endif
        ISessionPlanHost,
        ISessionEventsHost
    {
        BattleSessionHandles ISessionDispatchersRuntime.Handles => _handles;
        SessionDispatchersController ISessionDispatchersRuntime.Dispatchers => _dispatchers;

        SessionEventsController ISessionEventsRuntime.Events => _eventsCtrl;

        IBattleBootstrapper ISessionPlanRuntime.Bootstrapper => _bootstrapper;
        BattleSessionState ISessionPlanRuntime.State => _state;
        BattleSessionHandles ISessionPlanRuntime.Handles => _handles;
        BattleContext ISessionPlanRuntime.Context => _ctx;
        BattleSessionHooks ISessionPlanRuntime.Hooks => Hooks;
        SessionPlanController ISessionPlanRuntime.PlanController => _planCtrl;

        BattleStartPlan ISessionReplayRuntime.Plan => _plan;
        BattleSessionState ISessionReplayRuntime.State => _state;
        BattleSessionHandles ISessionReplayRuntime.Handles => _handles;
        BattleContext ISessionReplayRuntime.Context => _ctx;
        SessionReplayController ISessionReplayRuntime.Replay => _replayCtrl;

        BattleLogicSession ISessionNetAdapterRuntime.Session => _session;
        BattleSessionNetAdapter ISessionNetAdapterRuntime.NetAdapter => _netAdapter;
        SessionNetAdapterController ISessionNetAdapterRuntime.Net => _net;

        TickLoopController ISessionTickLoopRuntime.TickLoop => _tickLoop;

        BattleSessionHooks ISessionLifecycleRuntime.Hooks => Hooks;

        BattleSessionHooks ISessionGatewayRuntime.Hooks => Hooks;
        bool ISessionGatewayRuntime.HasGatewayRoomConnection => HasGatewayRoomConnection;
        Task ISessionGatewayRuntime.GatewayRoomPreparationTask => GatewayRoomPreparationTask;
        bool ISessionGatewayRuntime.ShouldPrepareGatewayRoom() => ShouldPrepareGatewayRoom();
        void ISessionGatewayRuntime.StartGatewayRoomPreparation() => StartGatewayRoomPreparation();
        void ISessionGatewayRuntime.CompleteGatewayRoomPreparation() => CompleteGatewayRoomPreparation();
        void ISessionGatewayRuntime.StopGatewayRoomPreparation() => StopGatewayRoomPreparation();
        void ISessionGatewayRuntime.TickGatewayRoomConnection(float deltaTime) => TickGatewayRoomConnection(deltaTime);
        void ISessionGatewayRuntime.OnStartSessionRequested() => OnStartSessionRequested();
        void ISessionGatewayRuntime.NotifySessionFailed(System.Exception exception) => _eventsCtrl.NotifySessionFailed(this, exception);

        BattleSessionHooks ISessionSnapshotRoutingRuntime.Hooks => Hooks;
        void ISessionSnapshotRoutingRuntime.EnsureSnapshotRoutingBuilt() => EnsureSnapshotRoutingBuilt();
        void ISessionSnapshotRoutingRuntime.DisposeSnapshotRoutingIfAny() => DisposeSnapshotRoutingIfAny();

#if UNITY_EDITOR
        void ISessionEditorHooksRuntime.TryInstallEditorPlayModeStopHook() => TryInstallEditorPlayModeStopHook();
        void ISessionEditorHooksRuntime.TryUninstallEditorPlayModeStopHook() => TryUninstallEditorPlayModeStopHook();
#endif
    }
}
