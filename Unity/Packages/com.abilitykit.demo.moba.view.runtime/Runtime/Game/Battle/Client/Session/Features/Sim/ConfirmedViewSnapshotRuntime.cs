using System;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Protocol.Moba;
using FrameSnapshotDispatcher = AbilityKit.Core.Snapshots.Routing.FrameSnapshotDispatcher;

namespace AbilityKit.Game.Flow
{
    internal sealed class ConfirmedViewSnapshotRuntime : IDisposable
    {
        private readonly BattleContext _ctx;
        private readonly long _contextBindingGeneration;

        private readonly BattleSubscriptionGroup _subscriptions = new BattleSubscriptionGroup(4);

        public FrameSnapshotDispatcher Snapshots { get; private set; }
        public SnapshotPipeline Pipeline { get; private set; }
        public SnapshotCmdHandler CmdHandler { get; private set; }

        private ConfirmedViewSnapshotRuntime(
            BattleContext ctx,
            long contextBindingGeneration,
            FrameSnapshotDispatcher snapshots,
            SnapshotPipeline pipeline,
            SnapshotCmdHandler cmdHandler)
        {
            _ctx = ctx;
            _contextBindingGeneration = contextBindingGeneration;
            Snapshots = snapshots;
            Pipeline = pipeline;
            CmdHandler = cmdHandler;
        }

        public static ConfirmedViewSnapshotRuntime Create(BattleContext ctx)
        {
            if (ctx == null) return null;

            var snapshots = new FrameSnapshotDispatcher();
            var pipeline = new SnapshotPipeline(ctx, snapshots);
            var cmdHandler = new SnapshotCmdHandler(ctx, snapshots);

            AbilityKit.Game.Flow.Snapshot.BattleSnapshotRegistry.RegisterAll(
                snapshots,
                pipeline,
                pipeline,
                cmdHandler);

            AbilityKit.Game.Flow.Snapshot.SharedSnapshotRegistry.RegisterAll(
                snapshots,
                pipeline,
                pipeline,
                cmdHandler);

            var contextBindingGeneration =
                ctx.BindSnapshotRouting(snapshots, pipeline, cmdHandler);

            var runtime = new ConfirmedViewSnapshotRuntime(
                ctx,
                contextBindingGeneration,
                snapshots,
                pipeline,
                cmdHandler);
            runtime.Subscribe(ctx);
            return runtime;
        }

        public void Dispose()
        {
            _ctx?.ClearSnapshotRouting(
                _contextBindingGeneration,
                Snapshots);

            _subscriptions.Clear();

            CmdHandler?.Dispose();
            CmdHandler = null;

            Pipeline?.Dispose();
            Pipeline = null;

            Snapshots?.Dispose();
            Snapshots = null;
        }

        private void Subscribe(BattleContext ctx)
        {
            if (Snapshots == null || ctx == null) return;

            _subscriptions.Clear();
            _subscriptions.Add(
                Snapshots.Subscribe<ActorTransformData[]>(
                    MobaOpCodes.Snapshot.ActorTransform,
                    (packet, entries) => BattleSnapshotEntityApplier.ApplyTransform(ctx, entries)));
            _subscriptions.Add(
                Snapshots.Subscribe<StateHashData>(
                    MobaOpCodes.Snapshot.StateHash,
                    (packet, snap) => BattleSnapshotEntityApplier.ApplyStateHash(ctx, snap)));
            _subscriptions.Add(
                Snapshots.Subscribe<ActorSpawnData[]>(
                    MobaOpCodes.Snapshot.ActorSpawn,
                    (packet, entries) => BattleSnapshotEntityApplier.ApplySpawn(ctx, entries)));
            _subscriptions.Add(
                Snapshots.Subscribe<ActorDespawnData[]>(
                    MobaOpCodes.Snapshot.ActorDespawn,
                    (packet, entries) => BattleSnapshotEntityApplier.ApplyDespawn(ctx, entries)));
        }
    }
}
