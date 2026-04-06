using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AbilityKit.Ability.Share.Common.SnapshotRouting;
using AbilityKit.Ability.Share.Common.Record.Lockstep;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Game.Flow.Battle.Replay;
using AbilityKit.Game.Flow.Modules;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Runtime;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private GamePhaseContext _phaseCtx
        {
            get => _handles.Phase.PhaseCtx;
            set => _handles.Phase.PhaseCtx = value;
        }

        private BattleLogicSession _session
        {
            get => _handles.Session;
            set => _handles.Session = value;
        }

        private BattleStartPlan _plan
        {
            get => _state.Plan;
            set => _state.Plan = value;
        }

        private FrameSnapshotDispatcher _snapshots
        {
            get => _handles.Snapshot.Snapshots;
            set => _handles.Snapshot.Snapshots = value;
        }

        private SnapshotPipeline _pipeline
        {
            get => _handles.Snapshot.Pipeline;
            set => _handles.Snapshot.Pipeline = value;
        }

        private SnapshotCmdHandler _cmdHandler
        {
            get => _handles.Snapshot.CmdHandler;
            set => _handles.Snapshot.CmdHandler = value;
        }

        private SnapshotRoutingInstance _routing
        {
            get => _handles.Snapshot.Routing;
            set => _handles.Snapshot.Routing = value;
        }

        private LockstepReplayDriver _replay
        {
            get => _handles.Replay.Driver;
            set => _handles.Replay.Driver = value;
        }

        private BattleSessionNetAdapter _netAdapter
        {
            get => _handles.Net.Adapter;
            set => _handles.Net.Adapter = value;
        }

        private IBattleSessionNetAdapterContext _netAdapterCtx
        {
            get => _handles.Net.Ctx;
            set => _handles.Net.Ctx = value;
        }

        private ConnectionManager _gatewayConn
        {
            get => _handles.GatewayRoom.Conn;
            set => _handles.GatewayRoom.Conn = value;
        }

        private GatewayRoomClient _gatewayClient
        {
            get => _handles.GatewayRoom.Client;
            set => _handles.GatewayRoom.Client = value;
        }

        private Task _gatewayTask
        {
            get => _handles.GatewayRoom.Task;
            set => _handles.GatewayRoom.Task = value;
        }

        private System.Threading.CancellationTokenSource _gatewayTimeSyncCts
        {
            get => _handles.GatewayRoom.TimeSyncCts;
            set => _handles.GatewayRoom.TimeSyncCts = value;
        }

        private Task _gatewayTimeSyncTask
        {
            get => _handles.GatewayRoom.TimeSyncTask;
            set => _handles.GatewayRoom.TimeSyncTask = value;
        }

        private Dictionary<WorldId, GatewayWorldStartAnchor> _gatewayWorldStartAnchors => _handles.GatewayRoom.WorldStartAnchors;

        private IDispatcher _unityDispatcher
        {
            get => _handles.Dispatchers.UnityDispatcher;
            set => _handles.Dispatchers.UnityDispatcher = value;
        }

        private DedicatedThreadDispatcher _networkIoDispatcher
        {
            get => _handles.Dispatchers.NetworkIoDispatcher;
            set => _handles.Dispatchers.NetworkIoDispatcher = value;
        }

        private void ResetHandles() => _handles.Reset();

        private int _lastFrame
        {
            get => _state.Tick.LastFrame;
            set => _state.Tick.LastFrame = value;
        }

        private float _tickAcc
        {
            get => _state.Tick.TickAcc;
            set => _state.Tick.TickAcc = value;
        }

        private bool _firstFrameReceived
        {
            get => _state.Tick.FirstFrameReceived;
            set => _state.Tick.FirstFrameReceived = value;
        }

        private int _remoteDrivenLastTickedFrame
        {
            get => _state.RemoteDriven.LastTickedFrame;
            set => _state.RemoteDriven.LastTickedFrame = value;
        }

        private int _remoteDrivenLastLoggedFrame
        {
            get => _state.RemoteDriven.LastLoggedFrame;
            set => _state.RemoteDriven.LastLoggedFrame = value;
        }

        private bool _remoteDrivenFirstSnapshotLogged
        {
            get => _state.RemoteDriven.FirstSnapshotLogged;
            set => _state.RemoteDriven.FirstSnapshotLogged = value;
        }

        private bool _remoteDrivenFirstSpawnLogged
        {
            get => _state.RemoteDriven.FirstSpawnLogged;
            set => _state.RemoteDriven.FirstSpawnLogged = value;
        }

        private int _confirmedLastTickedFrame
        {
            get => _state.Confirmed.LastTickedFrame;
            set => _state.Confirmed.LastTickedFrame = value;
        }

        private bool _tickEnteredLogged
        {
            get => _state.Flags.TickEnteredLogged;
            set => _state.Flags.TickEnteredLogged = value;
        }

        private bool _autoPlanLogged
        {
            get => _state.Flags.AutoPlanLogged;
            set => _state.Flags.AutoPlanLogged = value;
        }

        private Exception _pendingModuleValidationFailure
        {
            get => _state.PendingModuleValidationFailure;
            set => _state.PendingModuleValidationFailure = value;
        }

        private BattleContext _ctx
        {
            get => _handles.Phase.Ctx;
            set => _handles.Phase.Ctx = value;
        }

        private AbilityKit.World.ECS.IEntity _root
        {
            get => _handles.Phase.Root;
            set => _handles.Phase.Root = value;
        }

        private List<ISessionSubFeature<BattleSessionFeature>> _subFeatures
        {
            get => _handles.Phase.SubFeatures;
            set => _handles.Phase.SubFeatures = value;
        }

        private ModuleHost<FeatureModuleContext<BattleSessionFeature>, ISessionSubFeature<BattleSessionFeature>> _subFeatureHost
        {
            get => _handles.Phase.SubFeatureHost;
            set => _handles.Phase.SubFeatureHost = value;
        }

        private GameFlowDomain _flow
        {
            get => _handles.Phase.Flow;
            set => _handles.Phase.Flow = value;
        }

#if UNITY_EDITOR
        private bool _editorPlayModeHookActive
        {
            get => _state.EditorHooks.PlayModeHookActive;
            set => _state.EditorHooks.PlayModeHookActive = value;
        }
#endif
    }
}
