using System;
using System.Collections.Generic;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Management;
using AbilityKit.Core.Logging;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.StateImport;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Game.Flow.Battle;
using AbilityKit.Game.Flow.Battle.Replay;
using AbilityKit.Game.Flow.Battle.ViewEvents;
using AbilityKit.Game.Flow.Battle.ViewEvents.Snapshot;
using AbilityKit.Game.Flow.Battle.ViewEvents.Triggering;
using AbilityKit.Game.Flow.Modules;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Battle.Projection;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;

using HostWorldStateSnapshotProvider = AbilityKit.Ability.Host.IWorldStateSnapshotProvider;

namespace AbilityKit.Game.Flow
{
    internal static class BattleSessionResourceDisposer
    {
        public static void Dispose<T>(ref T resource) where T : class, IDisposable
        {
            var owned = resource;
            resource = null;
            if (owned == null) return;

            try
            {
                owned.Dispose();
            }
            catch (Exception exception)
            {
                Log.Exception(exception);
            }
        }
    }

    internal sealed class BattleSessionSnapshotRuntime
    {
        internal FrameSnapshotDispatcher Snapshots;
        internal SnapshotPipeline Pipeline;
        internal SnapshotCmdHandler CmdHandler;
        internal SnapshotRoutingInstance Routing;

        public void Reset()
        {
            BattleSessionResourceDisposer.Dispose(ref CmdHandler);
            BattleSessionResourceDisposer.Dispose(ref Pipeline);
            BattleSessionResourceDisposer.Dispose(ref Snapshots);
            BattleSessionResourceDisposer.Dispose(ref Routing);
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
            BattleSessionResourceDisposer.Dispose(ref NetworkIoDispatcher);
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

    internal sealed class BattleSessionWorldCapabilities
    {
        internal IWorld OwnerWorld { get; private set; }
        internal IActorProjectionProducer ProjectionProducer { get; private set; }
        internal HostWorldStateSnapshotProvider SnapshotProvider { get; private set; }
        internal IBattleDiagnosticMetricSink MetricSink { get; private set; }
        internal MobaLogicWorldStateImporter StateImporter { get; private set; }
        internal MobaAuthorityFrameService AuthorityFrames { get; private set; }

        internal void Bind(IWorld world)
        {
            if (ReferenceEquals(OwnerWorld, world)) return;

            Clear();
            OwnerWorld = world;
            var services = world?.Services;
            if (services == null) return;

            ProjectionProducer = Resolve<IActorProjectionProducer>(services);
            SnapshotProvider = Resolve<HostWorldStateSnapshotProvider>(services);
            MetricSink = Resolve<IBattleDiagnosticMetricSink>(services);
            StateImporter = Resolve<MobaLogicWorldStateImporter>(services);
            AuthorityFrames = Resolve<MobaAuthorityFrameService>(services);
        }

        internal bool Clear(IWorld ownerWorld)
        {
            if (!ReferenceEquals(OwnerWorld, ownerWorld)) return false;

            Clear();
            return true;
        }

        internal void Clear()
        {
            OwnerWorld = null;
            ProjectionProducer = null;
            SnapshotProvider = null;
            MetricSink = null;
            StateImporter = null;
            AuthorityFrames = null;
        }

        private static T Resolve<T>(AbilityKit.Ability.World.DI.IWorldResolver services)
            where T : class
        {
            try
            {
                services.TryResolve(out T capability);
                return capability;
            }
            catch (Exception exception)
            {
                Log.Exception(
                    exception,
                    $"[BattleSessionWorldCapabilities] Failed to resolve {typeof(T).FullName}.");
                return null;
            }
        }
    }

    internal sealed class BattleSessionConfirmedWorldRuntime
    {
        internal ConfirmedAuthorityWorldRuntime WorldRuntime;
        internal IWorldManager Worlds;
        internal HostRuntime Runtime;
        internal IWorld World;
        internal readonly BattleSessionWorldCapabilities Capabilities = new BattleSessionWorldCapabilities();
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
            Capabilities.Bind(World);
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
            ClearWorldRuntime(World);
        }

        internal bool ClearWorldRuntime(IWorld ownerWorld)
        {
            if (!ReferenceEquals(World, ownerWorld)) return false;

            Capabilities.Clear(ownerWorld);
            Worlds = null;
            Runtime = null;
            World = null;
            WorldRuntime = null;
            return true;
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
        internal readonly BattleSessionWorldCapabilities Capabilities = new BattleSessionWorldCapabilities();
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
            Capabilities.Bind(World);
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
            ClearWorldRuntime(World);
        }

        internal bool ClearWorldRuntime(IWorld ownerWorld)
        {
            if (!ReferenceEquals(World, ownerWorld)) return false;

            Capabilities.Clear(ownerWorld);
            WorldRuntime = null;
            Worlds = null;
            Runtime = null;
            World = null;
            return true;
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
