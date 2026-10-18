using System;
using System.Threading.Tasks;
using AbilityKit.Ability.Host;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Flow.Battle.Modules;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature : ISessionTickLoopPort, ISessionPlanHost, ISessionEventsHost
    {
        private sealed class SessionModuleRuntime :
            ISessionRuntimeFeature,
            ISessionDispatchersRuntime,
            ISessionEventsRuntime,
            ISessionPlanRuntime,
            ISessionReplayRuntime,
            ISessionNetAdapterRuntime,
            ISessionTickLoopRuntime,
            ISessionLifecycleRuntime,
            ISessionGatewayRuntime,
            ISessionSnapshotRoutingRuntime
#if UNITY_EDITOR
            , ISessionEditorHooksRuntime
#endif
        {
            private readonly BattleSessionFeature _owner;
            private readonly BattleSessionRuntime _runtime;

            public SessionModuleRuntime(BattleSessionFeature owner)
            {
                _owner = owner ?? throw new ArgumentNullException(nameof(owner));
                _runtime = owner._runtime;
            }

            public ISessionPlanHost PlanHost => _owner;
            public ISessionEventsHost EventsHost => _owner;

            void ISessionDispatchersRuntime.AttachDispatchers() =>
                _runtime.Dispatchers.OnAttach(_runtime.Handles);
            void ISessionDispatchersRuntime.DetachDispatchers() =>
                _runtime.Dispatchers.OnDetach(_runtime.Handles);
            SessionEventsController ISessionEventsRuntime.Events => _runtime.EventsController;

            SessionStartupTaskOwner ISessionPlanRuntime.StartupTasks => _runtime.StartupTasks;
            Task ISessionPlanRuntime.AttachPlanAsync(Func<bool> isCurrent) =>
                _runtime.PlanController.OnAttachAsync(
                    _owner,
                    _owner._bootstrapper,
                    _runtime.State,
                    _runtime.Handles,
                    _owner.Hooks,
                    _runtime.Handles.Phase.Ctx,
                    isCurrent);

            void ISessionReplayRuntime.SetupReplayOrRecord() =>
                _runtime.ReplayController.SetupReplayOrRecord(
                    _runtime.State.Plan, _runtime.Handles.Phase.Ctx, _runtime.Replay);
            void ISessionReplayRuntime.OnReplayFrameReceived(AbilityKit.Ability.Host.FramePacket packet) =>
                _runtime.ReplayController.OnFrameReceived(
                    _runtime.State.Plan, _runtime.State, _runtime.Handles.Phase.Ctx, packet);

            AbilityKit.Ability.Host.FramePacket ISessionNetAdapterRuntime.TransformFramePacket(
                AbilityKit.Ability.Host.FramePacket packet) =>
                _runtime.NetController.TransformFramePacket(
                    _runtime.Handles.Session, _runtime.Handles.Net.Adapter, packet);
            void ISessionTickLoopRuntime.TickSession(float deltaTime) =>
                _owner._clientDriver.Tick(deltaTime);
            BattleSessionHooks ISessionLifecycleRuntime.Hooks => _owner.Hooks;

            BattleSessionHooks ISessionGatewayRuntime.Hooks => _owner.Hooks;
            bool ISessionGatewayRuntime.HasGatewayRoomConnection => _owner.HasGatewayRoomConnection;
            Task ISessionGatewayRuntime.GatewayRoomPreparationTask => _owner.GatewayRoomPreparationTask;
            bool ISessionGatewayRuntime.ShouldPrepareGatewayRoom() => _owner.ShouldPrepareGatewayRoom();
            Task ISessionGatewayRuntime.StartGatewayRoomPreparation() => _owner.StartGatewayRoomPreparation();
            Task ISessionGatewayRuntime.CompleteGatewayRoomPreparationAsync() => _owner.CompleteGatewayRoomPreparationAsync();
            Task ISessionGatewayRuntime.StopGatewayRoomPreparationAsync() => _owner.StopGatewayRoomPreparationAsync();
            void ISessionGatewayRuntime.TickGatewayRoomConnection(float deltaTime) => _owner.TickGatewayRoomConnection(deltaTime);
            void ISessionGatewayRuntime.OnStartSessionRequested() => _owner.OnStartSessionRequested();
            void ISessionGatewayRuntime.NotifySessionFailed(Exception exception) =>
                _owner._eventsCtrl.NotifySessionFailed(_owner, exception);

            BattleSessionHooks ISessionSnapshotRoutingRuntime.Hooks => _owner.Hooks;
            void ISessionSnapshotRoutingRuntime.EnsureSnapshotRoutingBuilt() => _owner.EnsureSnapshotRoutingBuilt();
            void ISessionSnapshotRoutingRuntime.DisposeSnapshotRoutingIfAny() => _owner.DisposeSnapshotRoutingIfAny();

#if UNITY_EDITOR
            void ISessionEditorHooksRuntime.TryInstallEditorPlayModeStopHook() => _owner.TryInstallEditorPlayModeStopHook();
            void ISessionEditorHooksRuntime.TryUninstallEditorPlayModeStopHook() => _owner.TryUninstallEditorPlayModeStopHook();
#endif
        }
    }
}
