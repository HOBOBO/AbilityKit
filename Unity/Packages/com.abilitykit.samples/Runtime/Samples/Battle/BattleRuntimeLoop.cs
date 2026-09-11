#nullable enable

using System.Collections.Generic;
using AbilityKit.Ability.Host.Extensions.Server.BattleHost;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Battle
{
    [Sample(650, "battle", "runtime", "loop", "package-api", "web", "deterministic", "fixed-frame")]
    public sealed class BattleRuntimeLoop : SampleBase
    {
        public override string Title => "Battle Runtime Loop";
        public override string Description => "使用 BattleInputFrameScheduler、BattleInputBuffer 和 BattleTickDriver 驱动固定帧战斗循环";
        public override SampleCategory Category => SampleCategory.Combat;

        protected override void OnRun()
        {
            var state = new BattleHostState();
            state.Initialize(1001, "training-room", 20);

            var buffer = new BattleInputBuffer<PlayerCommand>();
            var simulation = new TrainingBattleSimulation();
            var driver = new BattleTickDriver<PlayerCommand>(simulation.SubmitInputs, simulation.TickWorld);

            Section("调度客户端输入帧");
            ScheduleInput(buffer, state, requestedFrame: 0, new PlayerCommand("player-1", "MoveRight"));
            ScheduleInput(buffer, state, requestedFrame: -1, new PlayerCommand("player-2", "Guard"));
            ScheduleInput(buffer, state, requestedFrame: 4, new PlayerCommand("player-1", "CastFireball"));
            KeyValue("BattleRuntimeLoop.Scheduled", "count=3");

            Divider();
            Section("固定帧 Tick");
            for (var i = 0; i < 6; i++)
            {
                var result = driver.Tick(state, buffer);
                KeyValue(
                    $"Frame {result.Frame}",
                    $"inputs={result.InputCount}, commands={result.CommandCount}, ticked={result.WorldTicked}, hp={simulation.TargetHp}, next={state.Frame}");
                KeyValue(
                    $"BattleRuntimeLoop.Frame[{result.Frame}]",
                    $"inputs={result.InputCount}, commands={result.CommandCount}, ticked={result.WorldTicked}, hp={simulation.TargetHp}, next={state.Frame}");
            }

            KeyValue("BattleRuntimeLoop.FinalHp", simulation.TargetHp.ToString());
            KeyValue("BattleRuntimeLoop.FinalFrame", state.Frame.ToString());
        }

        private void ScheduleInput(
            IBattleInputBuffer<PlayerCommand> buffer,
            BattleHostState state,
            int requestedFrame,
            PlayerCommand command)
        {
            var result = BattleInputFrameScheduler.Schedule(
                requestedFrame,
                state.Frame,
                inputDelayFrames: 1,
                BattleInputFrameSchedulerOptions.Default);

            KeyValue(
                command.Action,
                $"request={requestedFrame}, accepted={result.Accepted}, frame={result.AcceptedFrame}, status={result.Status}");
            KeyValue(
                $"BattleRuntimeLoop.Schedule[{command.Action}]",
                $"request={requestedFrame}, accepted={result.Accepted}, frame={result.AcceptedFrame}, status={result.Status}");

            if (result.Accepted)
            {
                buffer.Enqueue(result.AcceptedFrame, command);
            }
        }

        private readonly struct PlayerCommand
        {
            public PlayerCommand(string playerId, string action)
            {
                PlayerId = playerId;
                Action = action;
            }

            public string PlayerId { get; }
            public string Action { get; }
        }

        private sealed class TrainingBattleSimulation
        {
            private readonly List<string> _commands = new List<string>();

            public int TargetHp { get; private set; } = 100;

            public int SubmitInputs(int frame, IReadOnlyList<PlayerCommand> inputs)
            {
                for (var i = 0; i < inputs.Count; i++)
                {
                    var input = inputs[i];
                    _commands.Add($"{frame}:{input.PlayerId}:{input.Action}");

                    if (input.Action == "CastFireball")
                    {
                        TargetHp -= 35;
                    }
                }

                return inputs.Count;
            }

            public bool TickWorld(int frame, int tickRate, float deltaTime)
            {
                if (frame > 0 && frame % tickRate == 0)
                {
                    TargetHp -= 1;
                }

                return deltaTime > 0f;
            }
        }
    }
}
