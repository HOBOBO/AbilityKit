#nullable enable

using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Sync
{
    [Sample(951, "sync", "world", "snapshot", "package-api", "web", "deterministic", "fixed-frame")]
    public sealed class WorldSnapshot : SampleBase
    {
        private const int OpWorldSummary = 3001;

        public override string Title => "Sync World Snapshot";
        public override string Description => "使用 SnapshotRoutingBuilder、FrameSnapshotDispatcher 和 SnapshotPipeline 路由并应用 World 快照";
        public override SampleCategory Category => SampleCategory.World;

        protected override void OnRun()
        {
            var worldId = new WorldId("snapshot-world");
            var state = new SnapshotWorldState();
            var directEvents = new List<string>();

            using var routing = SnapshotRoutingBuilder.Build(
                state,
                SnapshotRoutingBuilder.From("world-summary", (dispatcherDecoders, pipelineDecoders, pipeline, cmd) =>
                {
                    dispatcherDecoders.RegisterDecoder<WorldSummary>(OpWorldSummary, DecodeWorldSummary);
                    pipelineDecoders.RegisterDecoder<WorldSummary>(OpWorldSummary, DecodeWorldSummary);

                    cmd.RegisterCmdHandler<WorldSummary>(OpWorldSummary, (ctx, envelope, summary) =>
                    {
                        directEvents.Add($"cmd frame={summary.Frame}, hp={summary.HitPoints}, alive={summary.Alive}");
                    });

                    pipeline.AddPipelineStage<WorldSummary>(OpWorldSummary, order: 0, (ctx, envelope, summary) =>
                    {
                        var target = (SnapshotWorldState)ctx;
                        target.LastFrame = summary.Frame;
                        target.LastHitPoints = summary.HitPoints;
                    });

                    pipeline.AddPipelineStage<WorldSummary>(OpWorldSummary, order: 10, (ctx, envelope, summary) =>
                    {
                        var target = (SnapshotWorldState)ctx;
                        target.IsAlive = summary.Alive;
                        target.AppliedCount++;
                        target.Trace.Add($"pipeline frame={summary.Frame}, hp={summary.HitPoints}, alive={summary.Alive}");
                    });
                }));
            KeyValue("WorldSnapshot.RoutingBuilt", "registry=world-summary,op=3001");

            Section("构建并投递快照包");
            FeedSnapshot(routing, worldId, frame: 0, hitPoints: 100, alive: true);
            FeedSnapshot(routing, worldId, frame: 1, hitPoints: 75, alive: true);
            FeedSnapshot(routing, worldId, frame: 2, hitPoints: 0, alive: false);
            KeyValue("WorldSnapshot.Fed", "count=3,lastFrame=2");

            Divider();
            Section("直接订阅记录");
            foreach (var item in directEvents)
            {
                Log(item);
            }
            KeyValue("WorldSnapshot.DirectEvents", directEvents.Count.ToString());

            Divider();
            Section("Pipeline 应用结果");
            foreach (var item in state.Trace)
            {
                Log(item);
            }

            KeyValue("Applied", state.AppliedCount.ToString());
            KeyValue("WorldSnapshot.Applied", state.AppliedCount.ToString());
            KeyValue("LastFrame", state.LastFrame.ToString());
            KeyValue("WorldSnapshot.LastFrame", state.LastFrame.ToString());
            KeyValue("LastHp", state.LastHitPoints.ToString());
            KeyValue("Alive", state.IsAlive.ToString());
            KeyValue("WorldSnapshot.Alive", state.IsAlive.ToString());
        }

        private static void FeedSnapshot(SnapshotRoutingInstance routing, WorldId worldId, int frame, int hitPoints, bool alive)
        {
            var snapshot = new WorldStateSnapshot(OpWorldSummary, EncodeWorldSummary(frame, hitPoints, alive));
            var packet = new FramePacket(worldId, new FrameIndex(frame), new PlayerInputCommand[0], snapshot);
            routing.Snapshots!.Feed(packet);
        }

        private static byte[] EncodeWorldSummary(int frame, int hitPoints, bool alive)
        {
            return new[]
            {
                (byte)frame,
                (byte)hitPoints,
                alive ? (byte)1 : (byte)0
            };
        }

        private static bool DecodeWorldSummary(in WorldStateSnapshot snapshot, out WorldSummary summary)
        {
            if (snapshot.Payload == null || snapshot.Payload.Length < 3)
            {
                summary = default;
                return false;
            }

            summary = new WorldSummary(snapshot.Payload[0], snapshot.Payload[1], snapshot.Payload[2] != 0);
            return true;
        }

        private readonly struct WorldSummary
        {
            public WorldSummary(int frame, int hitPoints, bool alive)
            {
                Frame = frame;
                HitPoints = hitPoints;
                Alive = alive;
            }

            public int Frame { get; }
            public int HitPoints { get; }
            public bool Alive { get; }
        }

        private sealed class SnapshotWorldState
        {
            public readonly List<string> Trace = new List<string>();

            public int AppliedCount { get; set; }
            public int LastFrame { get; set; }
            public int LastHitPoints { get; set; }
            public bool IsAlive { get; set; }
        }
    }
}
