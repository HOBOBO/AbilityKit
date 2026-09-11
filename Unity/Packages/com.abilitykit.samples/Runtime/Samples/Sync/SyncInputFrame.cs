#nullable enable

using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Management;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Sync
{
    [Sample(950, "sync", "input", "frame", "package-api", "web", "deterministic", "fixed-frame")]
    public sealed class SyncInputFrame : SampleBase
    {
        private const int OpMove = 1001;
        private const int OpCast = 2001;

        public override string Title => "Sync Input Frame";
        public override string Description => "使用 FramePacket、RemoteFrameAggregator 和 WorldManagerFrameDriver 聚合并提交输入帧";
        public override SampleCategory Category => SampleCategory.World;

        protected override void OnRun()
        {
            var worldId = new WorldId("sync-world");
            var aggregator = new RemoteFrameAggregator();
            var worldManager = new InputFrameWorldManager();
            var inputSink = new RecordingInputSink();
            var driver = new WorldManagerFrameDriver(worldManager);

            Section("接收远端 FramePacket");
            AddPacket(aggregator, worldId, frame: 0, CreateInput(0, "player-1", OpMove, 1));
            AddPacket(aggregator, worldId, frame: 1, CreateInput(1, "player-1", OpCast, 9));
            AddPacket(aggregator, worldId, frame: 1, CreateInput(1, "player-2", OpMove, 2));
            AddPacket(aggregator, worldId, frame: 3, CreateInput(3, "player-2", OpCast, 7));
            KeyValue("SyncInput.Packet", "count=4");

            Divider();
            Section("按帧构建输入并推进 World");
            for (var frameValue = 0; frameValue < 4; frameValue++)
            {
                var frame = new FrameIndex(frameValue);
                var remoteFrame = aggregator.BuildInputFrame(frame);
                KeyValue("SyncInput.FrameBuilt", $"frame={frame.Value},commands={remoteFrame.Commands.Length}");
                inputSink.Submit(frame, remoteFrame.Commands);
                KeyValue("SyncInput.Submitted", $"frame={frame.Value},commands={remoteFrame.Commands.Length}");
                worldManager.Apply(remoteFrame.Commands);
                driver.Step(0.05f);
                KeyValue("SyncInput.WorldTick", $"ticks={worldManager.TickCount},driverNext={driver.Frame.Value},score={worldManager.Score}");
                aggregator.TrimBefore(driver.Frame.Value - 1);
                KeyValue("SyncInput.Trimmed", $"minFrame={driver.Frame.Value - 1}");

                KeyValue(
                    $"Frame {frame.Value}",
                    $"commands={remoteFrame.Commands.Length}, worldTicks={worldManager.TickCount}, driverNext={driver.Frame.Value}, score={worldManager.Score}");
            }

            Divider();
            Section("输入提交记录");
            foreach (var line in inputSink.Submissions)
            {
                Log(line);
            }
        }

        private void AddPacket(RemoteFrameAggregator aggregator, WorldId worldId, int frame, PlayerInputCommand input)
        {
            var packet = new FramePacket(worldId, new FrameIndex(frame), new[] { input }, snapshot: null);
            aggregator.AddPacket(packet);
            KeyValue($"Packet {frame}", $"player={input.Player}, op={input.OpCode}, payload={input.Payload[0]}");
        }

        private static PlayerInputCommand CreateInput(int frame, string playerId, int opCode, byte payload)
        {
            return new PlayerInputCommand(
                new FrameIndex(frame),
                new PlayerId(playerId),
                opCode,
                new[] { payload });
        }

        private sealed class InputFrameWorldManager : IWorldManager
        {
            public IReadOnlyDictionary<WorldId, IWorld> Worlds { get; } = new Dictionary<WorldId, IWorld>();

            public int TickCount { get; private set; }
            public int Score { get; private set; }

            public IWorld Create(WorldCreateOptions options) => throw new System.NotSupportedException();
            public bool TryGet(WorldId id, out IWorld world)
            {
                world = null;
                return false;
            }

            public bool Destroy(WorldId id) => false;

            public void Tick(float deltaTime)
            {
                TickCount++;
            }

            public void DisposeAll()
            {
            }

            public void Apply(IReadOnlyList<PlayerInputCommand> commands)
            {
                for (var i = 0; i < commands.Count; i++)
                {
                    var command = commands[i];
                    if (command.OpCode == OpMove)
                    {
                        Score += command.Payload[0];
                    }
                    else if (command.OpCode == OpCast)
                    {
                        Score += command.Payload[0] * 10;
                    }
                }
            }
        }

        private sealed class RecordingInputSink : IWorldInputSink
        {
            private readonly List<string> _submissions = new List<string>();

            public IReadOnlyList<string> Submissions => _submissions;

            public void Submit(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs)
            {
                _submissions.Add($"frame={frame.Value}, count={inputs.Count}");
            }

            public void Dispose()
            {
            }
        }
    }
}
