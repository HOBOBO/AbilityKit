using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Logging;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Game.Battle;

namespace AbilityKit.Game.Flow
{
    internal readonly struct BattleSnapshotRoutingBuildContext
    {
        internal BattleSnapshotRoutingBuildContext(
            BattleStartPlan plan,
            BattleContext context,
            BattleLogicSession session,
            INetAdapterContextHost netAdapterHost)
        {
            Plan = plan;
            Context = context;
            Session = session;
            NetAdapterHost = netAdapterHost;
        }

        internal BattleStartPlan Plan { get; }

        internal BattleContext Context { get; }

        internal BattleLogicSession Session { get; }

        internal INetAdapterContextHost NetAdapterHost { get; }
    }

    internal sealed class BattleSnapshotRoutingRuntime :
        IDisposable,
        ISessionSnapshotRoutingPort<BattleSnapshotRoutingBuildContext, FramePacket>
    {
        private readonly BattleSessionHandles _handles;
        private readonly BattleSessionDiagnostics _diagnostics;
        private readonly SessionSnapshotRoutingController<
            BattleSnapshotRoutingBuildContext,
            FramePacket> _controller;
        private BattleContext _context;
        private BattleLogicSession _session;
        private Action<FramePacket> _frameReceivedHandler;
        private FrameSnapshotDispatcher _snapshots;
        private SnapshotPipeline _pipeline;
        private SnapshotCmdHandler _cmdHandler;
        private SnapshotRoutingInstance _routing;
        private IBattleSessionNetAdapterContext _netContext;
        private BattleSessionNetAdapter _netAdapter;
        private bool _frameReceivedSubscribed;
        private long _contextBindingGeneration;
        private ISet<string> _enabledRegistryIds;

        internal BattleSnapshotRoutingRuntime(
            BattleSessionHandles handles,
            BattleSessionDiagnostics diagnostics)
        {
            _handles = handles ??
                throw new ArgumentNullException(nameof(handles));
            _diagnostics = diagnostics ??
                throw new ArgumentNullException(nameof(diagnostics));
            _controller = new SessionSnapshotRoutingController<
                BattleSnapshotRoutingBuildContext,
                FramePacket>(this);
        }

        internal bool IsBuilt => _controller.IsBuilt;

        internal SessionSnapshotRoutingState State => _controller.State;

        public void Build(
            BattleStartPlan plan,
            BattleContext ctx,
            BattleLogicSession session,
            INetAdapterContextHost netAdapterHost,
            Action<FramePacket> frameReceivedHandler)
        {
            _controller.Build(
                new BattleSnapshotRoutingBuildContext(
                    plan,
                    ctx,
                    session,
                    netAdapterHost),
                frameReceivedHandler);

            Log.Info(
                $"[BattleSnapshotRoutingRuntime] Built. dispatcher={RuntimeHelpers.GetHashCode(_snapshots)}, routing={RuntimeHelpers.GetHashCode(_routing)}, enabled={(_enabledRegistryIds == null ? "all" : string.Join(",", _enabledRegistryIds))}");
        }

        public void Dispose()
        {
            if (_controller.State != SessionSnapshotRoutingState.Idle)
            {
                Log.Info(
                    $"[BattleSnapshotRoutingRuntime] Disposing. dispatcher={(_snapshots == null ? "null" : RuntimeHelpers.GetHashCode(_snapshots).ToString())}, routing={(_routing == null ? "null" : RuntimeHelpers.GetHashCode(_routing).ToString())}");
            }

            _controller.Dispose();
        }

        public void Feed(FramePacket packet)
        {
            _controller.TryFeed(packet);
        }

        void ISessionSnapshotRoutingPort<
            BattleSnapshotRoutingBuildContext,
            FramePacket>.Create(BattleSnapshotRoutingBuildContext context)
        {
            _context = context.Context;
            _session = context.Session;
            _enabledRegistryIds = CreateEnabledRegistrySet(context.Plan);

            var catalog = CreateCatalog();
            _snapshots = new FrameSnapshotDispatcher();
            _routing = _enabledRegistryIds == null
                ? SnapshotRoutingBuilder.Build(
                    _context,
                    _snapshots,
                    catalog.Registries)
                : SnapshotRoutingBuilder.Build(
                    _context,
                    _snapshots,
                    catalog.Registries,
                    _enabledRegistryIds);
            _pipeline = _routing.Pipeline;
            _cmdHandler = _routing.CmdHandler;

            if (context.NetAdapterHost != null)
            {
                _netContext = new BattleSessionNetAdapterContext(
                    context.NetAdapterHost);
                _netAdapter = new BattleSessionNetAdapter(
                    _netContext,
                    _diagnostics);
            }
        }

        void ISessionSnapshotRoutingPort<
            BattleSnapshotRoutingBuildContext,
            FramePacket>.Publish()
        {
            _handles.Snapshot.Snapshots = _snapshots;
            _handles.Snapshot.Pipeline = _pipeline;
            _handles.Snapshot.CmdHandler = _cmdHandler;
            _handles.Snapshot.Routing = _routing;
            _handles.Net.Ctx = _netContext;
            _handles.Net.Adapter = _netAdapter;
        }

        void ISessionSnapshotRoutingPort<
            BattleSnapshotRoutingBuildContext,
            FramePacket>.Bind()
        {
            _contextBindingGeneration = _context?.BindSnapshotRouting(
                _snapshots,
                _pipeline,
                _cmdHandler) ?? 0;
        }

        void ISessionSnapshotRoutingPort<
            BattleSnapshotRoutingBuildContext,
            FramePacket>.Subscribe(Action<FramePacket> frameReceivedHandler)
        {
            _frameReceivedHandler = frameReceivedHandler;
            if (_session == null || frameReceivedHandler == null)
            {
                return;
            }

            _session.FrameReceived += frameReceivedHandler;
            _frameReceivedSubscribed = true;
        }

        void ISessionSnapshotRoutingPort<
            BattleSnapshotRoutingBuildContext,
            FramePacket>.Unsubscribe(Action<FramePacket> frameReceivedHandler)
        {
            if (_frameReceivedSubscribed &&
                _session != null &&
                _frameReceivedHandler != null)
            {
                _session.FrameReceived -= _frameReceivedHandler;
            }

            _frameReceivedSubscribed = false;
            _frameReceivedHandler = null;
        }

        void ISessionSnapshotRoutingPort<
            BattleSnapshotRoutingBuildContext,
            FramePacket>.Unbind()
        {
            _context?.ClearSnapshotRouting(
                _contextBindingGeneration,
                _snapshots);
            _contextBindingGeneration = 0;
        }

        void ISessionSnapshotRoutingPort<
            BattleSnapshotRoutingBuildContext,
            FramePacket>.Unpublish()
        {
            if (ReferenceEquals(_handles.Snapshot.Routing, _routing))
            {
                _handles.Snapshot.Routing = null;
            }
            if (ReferenceEquals(_handles.Snapshot.CmdHandler, _cmdHandler))
            {
                _handles.Snapshot.CmdHandler = null;
            }
            if (ReferenceEquals(_handles.Snapshot.Pipeline, _pipeline))
            {
                _handles.Snapshot.Pipeline = null;
            }
            if (ReferenceEquals(_handles.Snapshot.Snapshots, _snapshots))
            {
                _handles.Snapshot.Snapshots = null;
            }
            if (ReferenceEquals(_handles.Net.Adapter, _netAdapter))
            {
                _handles.Net.Adapter = null;
            }
            if (ReferenceEquals(_handles.Net.Ctx, _netContext))
            {
                _handles.Net.Ctx = null;
            }
        }

        void ISessionSnapshotRoutingPort<
            BattleSnapshotRoutingBuildContext,
            FramePacket>.Release()
        {
            if (_routing != null)
            {
                _routing.Dispose();
                _routing = null;
                _pipeline = null;
                _cmdHandler = null;
            }

            if (_snapshots != null)
            {
                _snapshots.Dispose();
                _snapshots = null;
            }

            _context = null;
            _contextBindingGeneration = 0;
            _session = null;
            _frameReceivedHandler = null;
            _netContext = null;
            _netAdapter = null;
            _enabledRegistryIds = null;
            _diagnostics.ClearJitterBuffer();
        }

        void ISessionSnapshotRoutingPort<
            BattleSnapshotRoutingBuildContext,
            FramePacket>.Feed(FramePacket packet)
        {
            _snapshots?.Feed(packet);
        }

        private static SnapshotRegistryCatalog CreateCatalog()
        {
            return new SnapshotRegistryCatalog()
                .Add(
                    "battle",
                    AbilityKit.Game.Flow.Snapshot
                        .BattleSnapshotRegistry.RegisterAll)
                .Add(
                    "shared",
                    AbilityKit.Game.Flow.Snapshot
                        .SharedSnapshotRegistry.RegisterAll);
        }

        private static ISet<string> CreateEnabledRegistrySet(
            BattleStartPlan plan)
        {
            var registryIds = plan.Sync.EnabledSnapshotRegistryIds;
            return registryIds != null && registryIds.Length > 0
                ? new HashSet<string>(
                    registryIds,
                    StringComparer.Ordinal)
                : null;
        }
    }
}
