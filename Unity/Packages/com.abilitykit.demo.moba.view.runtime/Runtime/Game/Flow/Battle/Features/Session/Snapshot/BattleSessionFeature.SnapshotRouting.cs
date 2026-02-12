using System;
using System.Collections.Generic;
using AbilityKit.Ability.Share.Common.SnapshotRouting;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private void EnsureSnapshotRoutingBuilt() => BuildSnapshotRouting();

        private void DisposeSnapshotRoutingIfAny() => DisposeSnapshotRouting();

        private void BuildSnapshotRouting()
        {
            var catalog = new SnapshotRegistryCatalog()
                .Add("battle", AbilityKit.Game.Flow.Snapshot.BattleSnapshotRegistry.RegisterAll)
                .Add("shared", AbilityKit.Game.Flow.Snapshot.SharedSnapshotRegistry.RegisterAll);

            ISet<string> enabledRegistryIds = null;
            if (_plan.EnabledSnapshotRegistryIds != null && _plan.EnabledSnapshotRegistryIds.Length > 0)
            {
                enabledRegistryIds = new HashSet<string>(_plan.EnabledSnapshotRegistryIds, StringComparer.Ordinal);
            }

            _routing = enabledRegistryIds == null
                ? SnapshotRoutingBuilder.Build(_ctx, catalog)
                : SnapshotRoutingBuilder.Build(_ctx, catalog, enabledRegistryIds);

            _snapshots = _routing.Snapshots;
            _pipeline = _routing.Pipeline;
            _cmdHandler = _routing.CmdHandler;

            _netAdapterCtx = new BattleSessionNetAdapterContext((INetAdapterContextHost)this);
            _netAdapter = new BattleSessionNetAdapter(_netAdapterCtx);

            if (_ctx != null)
            {
                _ctx.FrameSnapshots = _snapshots;
                _ctx.SnapshotPipeline = _pipeline;
                _ctx.CmdHandler = _cmdHandler;
            }
        }

        private void DisposeSnapshotRouting()
        {
            _routing?.Dispose();
            _routing = null;

            if (_ctx != null)
            {
                _ctx.SnapshotPipeline = null;
                _ctx.CmdHandler = null;
                _ctx.FrameSnapshots = null;
            }

            _netAdapter = null;
            _netAdapterCtx = null;
            _cmdHandler = null;
            _pipeline = null;
            _snapshots = null;
        }
    }
}
