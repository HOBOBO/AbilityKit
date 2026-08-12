using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Logging;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Game.Battle;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleSnapshotRoutingRuntime : IDisposable
    {
        private readonly BattleSessionHandles _handles;
        private readonly BattleSessionDiagnostics _diagnostics;
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

        internal BattleSnapshotRoutingRuntime(
            BattleSessionHandles handles,
            BattleSessionDiagnostics diagnostics)
        {
            _handles = handles ?? throw new ArgumentNullException(nameof(handles));
            _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        }

        internal bool IsBuilt => _routing != null;

        public void Build(
            BattleStartPlan plan,
            BattleContext ctx,
            BattleLogicSession session,
            INetAdapterContextHost netAdapterHost,
            Action<FramePacket> frameReceivedHandler)
        {
            Dispose();

            _context = ctx;
            _session = session;
            _frameReceivedHandler = frameReceivedHandler;

            try
            {
                var catalog = CreateCatalog();
                var enabledRegistryIds = CreateEnabledRegistrySet(plan);

                _snapshots = new FrameSnapshotDispatcher();
                _routing = enabledRegistryIds == null
                    ? SnapshotRoutingBuilder.Build(ctx, _snapshots, catalog.Registries)
                    : SnapshotRoutingBuilder.Build(ctx, _snapshots, catalog.Registries, enabledRegistryIds);
                _pipeline = _routing.Pipeline;
                _cmdHandler = _routing.CmdHandler;

                if (netAdapterHost != null)
                {
                    _netContext = new BattleSessionNetAdapterContext(netAdapterHost);
                    _netAdapter = new BattleSessionNetAdapter(_netContext, _diagnostics);
                }

                PublishHandles();
                BindContext(ctx, _snapshots, _pipeline, _cmdHandler);

                if (session != null && frameReceivedHandler != null)
                {
                    session.FrameReceived += frameReceivedHandler;
                    _frameReceivedSubscribed = true;
                }

                Log.Info(
                    $"[BattleSnapshotRoutingRuntime] Built. dispatcher={RuntimeHelpers.GetHashCode(_snapshots)}, routing={RuntimeHelpers.GetHashCode(_routing)}, enabled={(enabledRegistryIds == null ? "all" : string.Join(",", enabledRegistryIds))}");
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            if (_frameReceivedSubscribed && _session != null && _frameReceivedHandler != null)
            {
                _session.FrameReceived -= _frameReceivedHandler;
            }
            _frameReceivedSubscribed = false;

            if (_routing != null || _snapshots != null)
            {
                Log.Info(
                    $"[BattleSnapshotRoutingRuntime] Disposing. dispatcher={(_snapshots == null ? "null" : RuntimeHelpers.GetHashCode(_snapshots).ToString())}, routing={(_routing == null ? "null" : RuntimeHelpers.GetHashCode(_routing).ToString())}");
            }

            if (_context != null && _context.IsSnapshotRoutingBoundTo(_snapshots, _pipeline, _cmdHandler))
            {
                _context.ClearSnapshotRouting();
            }

            _routing?.Dispose();
            ClearPublishedHandles();

            _context = null;
            _session = null;
            _frameReceivedHandler = null;
            _snapshots = null;
            _pipeline = null;
            _cmdHandler = null;
            _routing = null;
            _netContext = null;
            _netAdapter = null;
            _diagnostics.ClearJitterBuffer();
        }

        public void Feed(FramePacket packet)
        {
            _snapshots?.Feed(packet);
        }

        private static SnapshotRegistryCatalog CreateCatalog()
        {
            return new SnapshotRegistryCatalog()
                .Add("battle", AbilityKit.Game.Flow.Snapshot.BattleSnapshotRegistry.RegisterAll)
                .Add("shared", AbilityKit.Game.Flow.Snapshot.SharedSnapshotRegistry.RegisterAll);
        }

        private static ISet<string> CreateEnabledRegistrySet(BattleStartPlan plan)
        {
            var registryIds = plan.Sync.EnabledSnapshotRegistryIds;
            return registryIds != null && registryIds.Length > 0
                ? new HashSet<string>(registryIds, StringComparer.Ordinal)
                : null;
        }

        private void PublishHandles()
        {
            _handles.Snapshot.Snapshots = _snapshots;
            _handles.Snapshot.Pipeline = _pipeline;
            _handles.Snapshot.CmdHandler = _cmdHandler;
            _handles.Snapshot.Routing = _routing;
            _handles.Net.Ctx = _netContext;
            _handles.Net.Adapter = _netAdapter;
        }

        private void ClearPublishedHandles()
        {
            if (ReferenceEquals(_handles.Snapshot.Routing, _routing)) _handles.Snapshot.Routing = null;
            if (ReferenceEquals(_handles.Snapshot.CmdHandler, _cmdHandler)) _handles.Snapshot.CmdHandler = null;
            if (ReferenceEquals(_handles.Snapshot.Pipeline, _pipeline)) _handles.Snapshot.Pipeline = null;
            if (ReferenceEquals(_handles.Snapshot.Snapshots, _snapshots)) _handles.Snapshot.Snapshots = null;
            if (ReferenceEquals(_handles.Net.Adapter, _netAdapter)) _handles.Net.Adapter = null;
            if (ReferenceEquals(_handles.Net.Ctx, _netContext)) _handles.Net.Ctx = null;
        }

        private static void BindContext(
            BattleContext ctx,
            FrameSnapshotDispatcher snapshots,
            SnapshotPipeline pipeline,
            SnapshotCmdHandler cmdHandler)
        {
            if (ctx == null) return;
            ctx.BindSnapshotRouting(snapshots, pipeline, cmdHandler);
        }
    }
}
