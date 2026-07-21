using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Logging;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Game.Battle;

namespace AbilityKit.Game.Flow
{
    internal sealed class SessionSnapshotRoutingController
    {
        public void Build(
            BattleStartPlan plan,
            BattleSessionHandles handles,
            BattleContext ctx,
            BattleLogicSession session,
            INetAdapterContextHost netAdapterHost,
            Action<FramePacket> frameReceivedHandler)
        {
            if (handles == null) return;

            Dispose(handles, ctx, session, frameReceivedHandler);

            var catalog = CreateCatalog();
            var enabledRegistryIds = CreateEnabledRegistrySet(plan);

            handles.Snapshot.Snapshots = new FrameSnapshotDispatcher();
            if (session != null && frameReceivedHandler != null)
            {
                session.FrameReceived += frameReceivedHandler;
            }

            handles.Snapshot.Routing = enabledRegistryIds == null
                ? SnapshotRoutingBuilder.Build(ctx, handles.Snapshot.Snapshots, catalog.Registries)
                : SnapshotRoutingBuilder.Build(ctx, handles.Snapshot.Snapshots, catalog.Registries, enabledRegistryIds);
            Log.Info(
                $"[SessionSnapshotRoutingController] Built. dispatcher={RuntimeHelpers.GetHashCode(handles.Snapshot.Snapshots)}, routing={RuntimeHelpers.GetHashCode(handles.Snapshot.Routing)}, enabled={(enabledRegistryIds == null ? "all" : string.Join(",", enabledRegistryIds))}");

            handles.Snapshot.Pipeline = handles.Snapshot.Routing.Pipeline;
            handles.Snapshot.CmdHandler = handles.Snapshot.Routing.CmdHandler;

            if (netAdapterHost != null)
            {
                handles.Net.Ctx = new BattleSessionNetAdapterContext(netAdapterHost);
                handles.Net.Adapter = new BattleSessionNetAdapter(handles.Net.Ctx);
            }

            BindContext(ctx, handles);
        }

        public void Dispose(
            BattleSessionHandles handles,
            BattleContext ctx,
            BattleLogicSession session,
            Action<FramePacket> frameReceivedHandler)
        {
            if (handles == null) return;

            if (session != null && frameReceivedHandler != null)
            {
                session.FrameReceived -= frameReceivedHandler;
            }

            if (handles.Snapshot.Routing != null || handles.Snapshot.Snapshots != null)
            {
                Log.Info(
                    $"[SessionSnapshotRoutingController] Disposing. dispatcher={(handles.Snapshot.Snapshots == null ? "null" : RuntimeHelpers.GetHashCode(handles.Snapshot.Snapshots).ToString())}, routing={(handles.Snapshot.Routing == null ? "null" : RuntimeHelpers.GetHashCode(handles.Snapshot.Routing).ToString())}");
            }

            handles.Snapshot.Routing?.Dispose();
            handles.Snapshot.Routing = null;

            ClearContext(ctx);

            handles.Net.Adapter = null;
            handles.Net.Ctx = null;
            handles.Snapshot.CmdHandler = null;
            handles.Snapshot.Pipeline = null;
            handles.Snapshot.Snapshots = null;
        }

        public void Feed(BattleSessionHandles handles, FramePacket packet)
        {
            handles?.Snapshot.Snapshots?.Feed(packet);
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

        private static void BindContext(BattleContext ctx, BattleSessionHandles handles)
        {
            if (ctx == null) return;

            ctx.BindSnapshotRouting(
                handles.Snapshot.Snapshots,
                handles.Snapshot.Pipeline,
                handles.Snapshot.CmdHandler);
        }

        private static void ClearContext(BattleContext ctx)
        {
            if (ctx == null) return;

            ctx.ClearSnapshotRouting();
        }
    }
}
