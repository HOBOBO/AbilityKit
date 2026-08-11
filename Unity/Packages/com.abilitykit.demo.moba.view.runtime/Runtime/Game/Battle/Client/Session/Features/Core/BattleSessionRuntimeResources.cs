using System;
using System.Collections.Generic;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Management;
using AbilityKit.Core.Logging;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Core.Utilities;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Game.Flow.Battle;
using AbilityKit.Game.Flow.Battle.Replay;
using AbilityKit.Game.Flow.Battle.ViewEvents;
using AbilityKit.Game.Flow.Battle.ViewEvents.Snapshot;
using AbilityKit.Game.Flow.Battle.ViewEvents.Triggering;
using AbilityKit.Game.Flow.Modules;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleSessionSnapshotRuntime
    {
        internal FrameSnapshotDispatcher Snapshots;
        internal SnapshotPipeline Pipeline;
        internal SnapshotCmdHandler CmdHandler;
        internal SnapshotRoutingInstance Routing;

        public void Reset()
        {
            if (CmdHandler != null) DisposeUtils.TryDispose(ref CmdHandler, ex => Log.Exception(ex));
            if (Pipeline != null) DisposeUtils.TryDispose(ref Pipeline, ex => Log.Exception(ex));
            if (Snapshots != null) DisposeUtils.TryDispose(ref Snapshots, ex => Log.Exception(ex));

            if (Routing != null)
            {
                try { Routing.Dispose(); }
                catch (Exception ex) { Log.Exception(ex); }
                Routing = null;
            }

            Snapshots = null;
            Pipeline = null;
            CmdHandler = null;
        }
    }

    internal sealed class BattleSessionNetworkRuntime
    {
        internal BattleSessionNetAdapter Adapter;
        internal IBattleSessionNetAdapterContext Ctx;

        public void Reset()
        {
            Adapter = null;
            Ctx = null;
        }
    }

    internal sealed class BattleSessionDispatcherRuntime
    {
        internal IDispatcher UnityDispatcher;
        internal DedicatedThreadDispatcher NetworkIoDispatcher;

        public void Reset()
        {
            UnityDispatcher = null;
            DisposeNetworkIoDispatcher();
        }

        public void DisposeNetworkIoDispatcher()
        {
            DisposeUtils.TryDispose(ref NetworkIoDispatcher, ex => Log.Exception(ex));
        }
    }

    internal sealed class BattleSessionReplayRuntime
    {
        internal FrameReplayDriver Driver;

        public void Reset()
        {
            Driver = null;
        }
    }

    internal sealed class BattleSessionPhaseRuntime
    {
        internal GamePhaseContext PhaseCtx;
        internal BattleContext Ctx;
        internal AbilityKit.World.ECS.IEntity Root;
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

    internal sealed class BattleSessionGatewayRoomRuntime
    {
        internal object ConnectionOwner;
        internal IConnection Conn;
        internal IGatewayRoomClient Client;

        public void Reset()
        {
            ConnectionOwner = null;
            Conn = null;
            Client = null;
        }
    }

    internal sealed class BattleSessionConfirmedWorldRuntime
    {
        internal ConfirmedAuthorityWorldRuntime WorldRuntime;
        internal IWorldManager Worlds;
        internal HostRuntime Runtime;
        internal IWorld World;
        internal ConfirmedAuthorityInputRuntime InputRuntime;
        internal IRemoteFrameSource<PlayerInputCommand[]> InputSource;
        internal IConsumableRemoteFrameSource<PlayerInputCommand[]> Consumable;
        internal IRemoteFrameSink<PlayerInputCommand[]> Sink;
        internal ConfirmedViewEventPipeline ViewEventPipeline;
        internal FrameSnapshotDispatcher Snapshots;
        internal DebugBattleViewEventSink ViewEventSink;
        internal BattleSnapshotViewAdapter SnapshotViewAdapter;
        internal BattleTriggerEventViewBridge TriggerBridge;

        internal void BindWorldRuntime(ConfirmedAuthorityWorldRuntime runtime)
        {
            WorldRuntime = runtime;
            Worlds = runtime != null ? runtime.Worlds : null;
            Runtime = runtime != null ? runtime.Runtime : null;
            World = runtime != null ? runtime.World : null;
        }

        internal void BindInputRuntime(ConfirmedAuthorityInputRuntime runtime)
        {
            InputRuntime = runtime;
            InputSource = runtime != null ? runtime.Source : null;
            Consumable = runtime != null ? runtime.Consumable : null;
            Sink = runtime != null ? runtime.Sink : null;
        }

        internal void BindViewEventPipeline(ConfirmedViewEventPipeline pipeline)
        {
            ViewEventPipeline = pipeline;
            Snapshots = pipeline != null ? pipeline.Snapshots : null;
            ViewEventSink = pipeline != null ? pipeline.EventSink : null;
            SnapshotViewAdapter = pipeline != null ? pipeline.SnapshotViewAdapter : null;
            TriggerBridge = pipeline != null ? pipeline.TriggerBridge : null;
        }

        internal void DestroyWorld(WorldId fallbackWorldId)
        {
            if (WorldRuntime != null)
            {
                WorldRuntime.DestroyWorld();
                return;
            }

            Runtime?.DestroyWorld(fallbackWorldId);
        }

        internal void ClearWorldRuntime()
        {
            Worlds = null;
            Runtime = null;
            World = null;
            WorldRuntime = null;
        }

        internal void DisposeInput()
        {
            if (InputRuntime != null)
            {
                InputRuntime.Dispose();
                InputRuntime = null;
            }
            else if (InputSource is IDisposable inputSourceDisposable)
            {
                inputSourceDisposable.Dispose();
            }

            InputSource = null;
            Consumable = null;
            Sink = null;
        }

        internal void DisposeViewEventPipeline()
        {
            if (ViewEventPipeline != null)
            {
                ViewEventPipeline.Dispose();
                ViewEventPipeline = null;
            }
            else
            {
                SessionSimRuntimeDisposer.ExecuteCleanupSteps(
                    "Failed to dispose confirmed view event pipeline resources.",
                    () => Snapshots?.Dispose(),
                    () => SnapshotViewAdapter?.Dispose(),
                    () => TriggerBridge?.Dispose());
            }

            Snapshots = null;
            SnapshotViewAdapter = null;
            TriggerBridge = null;
            ViewEventSink = null;
        }

        public void Reset()
        {
            ClearWorldRuntime();
            DisposeInput();
            DisposeViewEventPipeline();
        }
    }

    internal sealed class BattleSessionRemoteDrivenWorldRuntime
    {
        internal RemoteDrivenWorldRuntime WorldRuntime;
        internal IWorldManager Worlds;
        internal HostRuntime Runtime;
        internal IWorld World;
        internal RemoteDrivenInputRuntime InputRuntime;
        internal IRemoteFrameSource<PlayerInputCommand[]> InputSource;
        internal IConsumableRemoteFrameSource<PlayerInputCommand[]> Consumable;
        internal IRemoteFrameSink<PlayerInputCommand[]> Sink;

        internal void BindWorldRuntime(RemoteDrivenWorldRuntime runtime)
        {
            WorldRuntime = runtime;
            Worlds = runtime != null ? runtime.Worlds : null;
            Runtime = runtime != null ? runtime.Runtime : null;
            World = runtime != null ? runtime.World : null;
        }

        internal void BindInputRuntime(RemoteDrivenInputRuntime runtime)
        {
            InputRuntime = runtime;
            InputSource = runtime != null ? runtime.Source : null;
            Consumable = runtime != null ? runtime.Consumable : null;
            Sink = runtime != null ? runtime.Sink : null;
        }

        internal void DestroyWorld(WorldId fallbackWorldId)
        {
            if (WorldRuntime != null)
            {
                WorldRuntime.DestroyWorld();
                return;
            }

            Runtime?.DestroyWorld(fallbackWorldId);
        }

        internal void ClearWorldRuntime()
        {
            WorldRuntime = null;
            Worlds = null;
            Runtime = null;
            World = null;
        }

        internal void DisposeInput()
        {
            if (InputRuntime != null)
            {
                InputRuntime.Dispose();
                InputRuntime = null;
            }
            else if (InputSource is IDisposable inputSourceDisposable)
            {
                inputSourceDisposable.Dispose();
            }

            InputSource = null;
            Consumable = null;
            Sink = null;
        }

        public void Reset()
        {
            ClearWorldRuntime();
            DisposeInput();
        }
    }
}
