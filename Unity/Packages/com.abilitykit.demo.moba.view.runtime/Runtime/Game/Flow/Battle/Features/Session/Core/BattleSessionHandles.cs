using System.Collections.Generic;
using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.Common.Record.Lockstep;
using AbilityKit.Ability.Share.Common.SnapshotRouting;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Management;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Flow.Battle.ViewEvents.Snapshot;
using AbilityKit.Game.Flow.Battle.ViewEvents.Triggering;
using AbilityKit.Game.Flow.Battle.Replay;
using AbilityKit.Game.Flow.Modules;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleSessionHandles
    {
        internal sealed class PhaseHandles
        {
            internal GamePhaseContext PhaseCtx;
            internal BattleContext Ctx;
            internal AbilityKit.Ability.EC.Entity Root;

            internal List<ISessionSubFeature<BattleSessionFeature>> SubFeatures;
            internal ModuleHost<FeatureModuleContext<BattleSessionFeature>, ISessionSubFeature<BattleSessionFeature>> SubFeatureHost;

            internal GameFlowDomain Flow;

            public void Reset()
            {
                PhaseCtx = default;
                Ctx = null;
                Root = default;
                SubFeatures = null;
                SubFeatureHost = null;
                Flow = null;
            }
        }

        internal sealed class SnapshotHandles
        {
            internal FrameSnapshotDispatcher Snapshots;
            internal SnapshotPipeline Pipeline;
            internal SnapshotCmdHandler CmdHandler;
            internal SnapshotRoutingInstance Routing;

            public void Reset()
            {
                if (CmdHandler != null)
                {
                    try
                    {
                        CmdHandler.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex);
                    }
                    CmdHandler = null;
                }

                if (Pipeline != null)
                {
                    try
                    {
                        Pipeline.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex);
                    }
                    Pipeline = null;
                }

                if (Snapshots != null)
                {
                    try
                    {
                        Snapshots.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex);
                    }
                    Snapshots = null;
                }

                if (Routing != null)
                {
                    try
                    {
                        Routing.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex);
                    }
                    Routing = null;
                }

                Snapshots = null;
                Pipeline = null;
                CmdHandler = null;
                Routing = null;
            }
        }

        internal sealed class RemoteDrivenHandles
        {
            internal IWorldManager Worlds;
            internal HostRuntime Runtime;
            internal IWorld World;

            internal IRemoteFrameSource<PlayerInputCommand[]> InputSource;
            internal IConsumableRemoteFrameSource<PlayerInputCommand[]> Consumable;
            internal IRemoteFrameSink<PlayerInputCommand[]> Sink;

            public void Reset()
            {
                Worlds = null;
                Runtime = null;
                World = null;

                if (InputSource is IDisposable d)
                {
                    try
                    {
                        d.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex);
                    }
                }

                InputSource = null;
                Consumable = null;
                Sink = null;

                if (SnapshotViewAdapter != null)
                {
                    try
                    {
                        SnapshotViewAdapter.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex);
                    }
                    SnapshotViewAdapter = null;
                }

                if (TriggerBridge != null)
                {
                    try
                    {
                        TriggerBridge.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex);
                    }
                    TriggerBridge = null;
                }

                if (Snapshots != null)
                {
                    try
                    {
                        Snapshots.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex);
                    }
                    Snapshots = null;
                }
            }
        }

        internal sealed class ConfirmedHandles
        {
            internal IWorldManager Worlds;
            internal HostRuntime Runtime;
            internal IWorld World;

            internal IRemoteFrameSource<PlayerInputCommand[]> InputSource;
            internal IConsumableRemoteFrameSource<PlayerInputCommand[]> Consumable;
            internal IRemoteFrameSink<PlayerInputCommand[]> Sink;

            internal FrameSnapshotDispatcher Snapshots;

            internal BattleSessionFeature.DebugBattleViewEventSink ViewEventSink;

            internal BattleSnapshotViewAdapter SnapshotViewAdapter;
            internal BattleTriggerEventViewBridge TriggerBridge;

            internal BattleContext ViewCtx;
            internal FrameSnapshotDispatcher ViewSnapshots;
            internal SnapshotPipeline ViewPipeline;
            internal SnapshotCmdHandler ViewCmdHandler;
            internal ConfirmedBattleViewFeature ViewFeature;

            internal IDisposable ViewSubLobby;
            internal IDisposable ViewSubActorTransform;
            internal IDisposable ViewSubStateHash;

            public void Reset()
            {
                Worlds = null;
                Runtime = null;
                World = null;

                if (InputSource is IDisposable d)
                {
                    try
                    {
                        d.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex);
                    }
                }

                InputSource = null;
                Consumable = null;
                Sink = null;

                Snapshots = null;
                ViewEventSink = null;

                SnapshotViewAdapter = null;
                TriggerBridge = null;

                ViewCtx = null;
                ViewSnapshots = null;
                ViewPipeline = null;
                ViewCmdHandler = null;
                ViewFeature = null;

                if (ViewCmdHandler != null)
                {
                    try
                    {
                        ViewCmdHandler.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex);
                    }
                    ViewCmdHandler = null;
                }

                if (ViewPipeline != null)
                {
                    try
                    {
                        ViewPipeline.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex);
                    }
                    ViewPipeline = null;
                }

                if (ViewSnapshots != null)
                {
                    try
                    {
                        ViewSnapshots.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex);
                    }
                    ViewSnapshots = null;
                }

                if (ViewSubLobby != null)
                {
                    try
                    {
                        ViewSubLobby.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex);
                    }
                    ViewSubLobby = null;
                }

                if (ViewSubActorTransform != null)
                {
                    try
                    {
                        ViewSubActorTransform.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex);
                    }
                    ViewSubActorTransform = null;
                }

                if (ViewSubStateHash != null)
                {
                    try
                    {
                        ViewSubStateHash.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex);
                    }
                    ViewSubStateHash = null;
                }

                ViewSubLobby = null;
                ViewSubActorTransform = null;
                ViewSubStateHash = null;
            }
        }

        internal sealed class GatewayRoomHandles
        {
            internal ConnectionManager Conn;
            internal GatewayRoomClient Client;
            internal Task Task;

            internal readonly Dictionary<WorldId, GatewayWorldStartAnchor> WorldStartAnchors = new Dictionary<WorldId, GatewayWorldStartAnchor>();

            internal CancellationTokenSource TimeSyncCts;
            internal Task TimeSyncTask;

            public void Reset()
            {
                if (TimeSyncCts != null)
                {
                    try
                    {
                        if (!TimeSyncCts.IsCancellationRequested)
                        {
                            TimeSyncCts.Cancel();
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex);
                    }

                    try
                    {
                        TimeSyncCts.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex);
                    }

                    TimeSyncCts = null;
                }

                if (Conn != null)
                {
                    try
                    {
                        Conn.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex);
                    }
                    Conn = null;
                }

                Conn = null;
                Client = null;
                Task = null;

                WorldStartAnchors.Clear();

                TimeSyncTask = null;
            }
        }

        internal sealed class NetHandles
        {
            internal BattleSessionNetAdapter Adapter;
            internal IBattleSessionNetAdapterContext Ctx;

            public void Reset()
            {
                Adapter = null;
                Ctx = null;
            }
        }

        internal sealed class DispatcherHandles
        {
            internal IDispatcher UnityDispatcher;
            internal DedicatedThreadDispatcher NetworkIoDispatcher;

            public void Reset()
            {
                UnityDispatcher = null;
                if (NetworkIoDispatcher != null)
                {
                    try
                    {
                        NetworkIoDispatcher.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex);
                    }
                    NetworkIoDispatcher = null;
                }

                NetworkIoDispatcher = null;
            }
        }

        internal sealed class ReplayHandles
        {
            internal LockstepReplayDriver Driver;

            public void Reset()
            {
                if (Driver is IDisposable d)
                {
                    try
                    {
                        d.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex);
                    }
                }
                Driver = null;
            }
        }

        internal BattleLogicSession Session;

        internal readonly SnapshotHandles Snapshot = new SnapshotHandles();
        internal readonly NetHandles Net = new NetHandles();

        internal readonly DispatcherHandles Dispatchers = new DispatcherHandles();

        internal readonly ReplayHandles Replay = new ReplayHandles();

        internal readonly PhaseHandles Phase = new PhaseHandles();

        internal readonly GatewayRoomHandles GatewayRoom = new GatewayRoomHandles();

        internal readonly ConfirmedHandles Confirmed = new ConfirmedHandles();

        internal readonly RemoteDrivenHandles RemoteDriven = new RemoteDrivenHandles();

        public void Reset()
        {
            Session = null;

            Snapshot.Reset();
            Net.Reset();
            Dispatchers.Reset();
            Replay.Reset();
            Phase.Reset();
            GatewayRoom.Reset();
            Confirmed.Reset();
            RemoteDriven.Reset();
        }
    }
}
