using System.Collections.Generic;
using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.Share.Common.Log;
using DisposeUtils = AbilityKit.Ability.Share.Common.Log.DisposeUtils;
using AbilityKit.Ability.Share.Common.Record.Lockstep;
using AbilityKit.Ability.Share.Common.SnapshotRouting;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Management;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Flow.Battle.ViewEvents.Snapshot;
using AbilityKit.Game.Flow.Battle.ViewEvents.Triggering;
using AbilityKit.Game.Flow.Modules;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;

namespace AbilityKit.Game.Flow
{
    internal sealed partial class BattleSessionHandles
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

                IDisposable inputSourceDisposable = InputSource;
                InputSource = null;
                DisposeUtils.TryDispose(ref inputSourceDisposable, ex => Log.Exception(ex));
                Consumable = null;
                Sink = null;
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

                IDisposable inputSourceDisposable = InputSource;
                InputSource = null;
                DisposeUtils.TryDispose(ref inputSourceDisposable, ex => Log.Exception(ex));
                Consumable = null;
                Sink = null;

                DisposeUtils.TryDispose(ref ViewCmdHandler, ex => Log.Exception(ex));
                DisposeUtils.TryDispose(ref ViewPipeline, ex => Log.Exception(ex));
                DisposeUtils.TryDispose(ref ViewSnapshots, ex => Log.Exception(ex));

                DisposeUtils.TryDispose(ref ViewSubLobby, ex => Log.Exception(ex));
                DisposeUtils.TryDispose(ref ViewSubActorTransform, ex => Log.Exception(ex));
                DisposeUtils.TryDispose(ref ViewSubStateHash, ex => Log.Exception(ex));

                Snapshots = null;
                ViewEventSink = null;

                SnapshotViewAdapter = null;
                TriggerBridge = null;

                ViewCtx = null;
                ViewFeature = null;
            }
        }

        internal sealed class GatewayRoomHandles
        {
            internal ConnectionManager Conn;
            internal GatewayRoomClient Client;
            internal System.Threading.Tasks.Task Task;

            internal readonly Dictionary<WorldId, GatewayWorldStartAnchor> WorldStartAnchors = new Dictionary<WorldId, GatewayWorldStartAnchor>();

            internal CancellationTokenSource TimeSyncCts;
            internal Task TimeSyncTask;

            public void Reset()
            {
                TimeSyncTask = null;

                if (TimeSyncCts != null)
                {
                    var cts = TimeSyncCts;
                    TimeSyncCts = null;

                    try
                    {
                        if (!cts.IsCancellationRequested)
                        {
                            cts.Cancel();
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex);
                    }

                    DisposeUtils.TryDispose(ref cts, ex => Log.Exception(ex));
                }

                if (Conn != null)
                {
                    var conn = Conn;
                    Conn = null;
                    DisposeUtils.TryDispose(ref conn, ex => Log.Exception(ex));
                }
                Client = null;
                Task = null;

                WorldStartAnchors.Clear();
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
