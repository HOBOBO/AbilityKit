using System;
using System.Collections.Generic;
using System.Globalization;
using AbilityKit.Scenario;

namespace AbilityKit.BattleScenario.Editor
{
    /// <summary>
    /// 编辑器的一维纠偏演练。用配方中自建的移动输入和权威位置生成只读演示序列，不驱动游戏同步运行时。
    /// </summary>
    internal static class LocalCorrectionPreview
    {
        internal const string Move = "local.preview.move";
        internal const string Correct = "local.preview.correct";
        internal const string Delta = "delta";
        internal const string AnchorTick = "anchorTick";
        internal const string Position = "position";

        internal enum Phase { Start, Predicted, Match, Mismatch, Restored, Replayed }

        internal readonly struct Frame
        {
            internal readonly Phase Stage;
            internal readonly int AtTick;
            internal readonly int SimulationTick;
            internal readonly int ViewPosition;
            internal readonly int? PreviousAnchorPosition;
            internal readonly int? AuthorityPosition;

            internal Frame(Phase stage, int atTick, int simulationTick, int viewPosition,
                int? previousAnchorPosition = null, int? authorityPosition = null)
            {
                Stage = stage;
                AtTick = atTick;
                SimulationTick = simulationTick;
                ViewPosition = viewPosition;
                PreviousAnchorPosition = previousAnchorPosition;
                AuthorityPosition = authorityPosition;
            }
        }

        internal static BattleScenarioDocument CreateRecipe() => new BattleScenarioDocument
        {
            CaseId = "local-correction-preview",
            DisplayName = "本地移动纠偏演练",
            Sections = new BattleScenarioSections
            {
                Settings = new List<BattleBlock>
                {
                    new ExecutionSettingsBlock { TickRate = 10, MaxDurationMs = 2_000, SettleDurationMs = 0 },
                },
                Timeline = new List<BattleBlock>
                {
                    CreateCommand(100, Move, "预测移动", (Delta, 4)),
                    CreateCommand(200, Move, "预测移动", (Delta, 3)),
                    CreateCommand(300, Move, "预测移动", (Delta, 2)),
                    CreateCommand(400, Correct, "交付权威位置", (AnchorTick, 1), (Position, 2)),
                },
            },
        };

        internal static IReadOnlyList<Frame> Compile(TestScenario scenario)
        {
            var execution = scenario.ResolveExecution();
            if (scenario.Commands.Count == 0 || scenario.Actors.Count != 0 || scenario.Setup.Count != 0 ||
                scenario.Obstacles.Count != 0 || scenario.Timeline.Count != 0 || scenario.NetworkWatches.Count != 0 ||
                scenario.Expectations != null || execution.SettleDurationMs != 0 ||
                execution.EndCondition?.Kind != TestEndConditionKinds.TimelineComplete)
                throw new InvalidOperationException("本地纠偏回放只支持命令配方，无战场、断言或收尾阶段。请先载入纠偏样例。");

            var plan = FixedTickCommandPlan.Compile<PreviewAction>(scenario, Bind);
            if (!plan[plan.Count - 1].Action.IsCorrection)
                throw new InvalidOperationException("配方须以一条权威位置交付结束。");

            var frames = new List<Frame> { new Frame(Phase.Start, 0, 0, 0) };
            var positionAtTick = new Dictionary<int, int> { [0] = 0 };
            var viewPosition = 0;
            for (var index = 0; index < plan.Count - 1; index++)
            {
                var step = plan[index];
                if (step.Action.IsCorrection || step.Tick == 0)
                    throw new InvalidOperationException($"commands[{step.SourceIndex}]：预测移动须在 Tick 0 之后、唯一一次权威交付之前。");

                viewPosition = checked(viewPosition + step.Action.Value);
                positionAtTick[step.Tick] = viewPosition;
                frames.Add(new Frame(Phase.Predicted, step.Tick, step.Tick, viewPosition));
            }

            var arrival = plan[plan.Count - 1];
            var anchorTick = arrival.Action.AnchorTick;
            if (!positionAtTick.TryGetValue(anchorTick, out var previousAnchor))
                throw new InvalidOperationException($"commands[{arrival.SourceIndex}]：权威锚点 Tick {anchorTick} 未记录预测位置。");

            var authority = arrival.Action.Value;
            if (previousAnchor == authority)
            {
                frames.Add(new Frame(Phase.Match, arrival.Tick, anchorTick, viewPosition, previousAnchor, authority));
                return frames.AsReadOnly();
            }

            frames.Add(new Frame(Phase.Mismatch, arrival.Tick, anchorTick, viewPosition, previousAnchor, authority));
            viewPosition = authority;
            frames.Add(new Frame(Phase.Restored, arrival.Tick, anchorTick, viewPosition, previousAnchor, authority));
            for (var index = 0; index < plan.Count - 1; index++)
            {
                var input = plan[index];
                if (input.Tick <= anchorTick) continue;
                viewPosition = checked(viewPosition + input.Action.Value);
                frames.Add(new Frame(Phase.Replayed, arrival.Tick, input.Tick, viewPosition, previousAnchor, authority));
            }
            return frames.AsReadOnly();
        }

        private static CommandBlock CreateCommand(int atMs, string name, string label, params (string Key, int Value)[] fields)
        {
            var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var field in fields)
                parameters.Add(field.Key, field.Value.ToString(CultureInfo.InvariantCulture));
            return new CommandBlock { AtMs = atMs, Name = name, DisplayName = label, Parameters = parameters };
        }

        private static bool Bind(TestCommand command, out PreviewAction action)
        {
            action = default;
            if (command.ActorAlias != null || command.TargetAlias != null)
                return false;
            var parameters = command.Parameters;
            if (command.Name == Move && parameters.Count == 1 && TryValue(parameters, Delta, out var delta))
            {
                action = new PreviewAction(false, 0, delta);
                return true;
            }
            if (command.Name == Correct && parameters.Count == 2 &&
                TryValue(parameters, AnchorTick, out var tick) && tick >= 0 && TryValue(parameters, Position, out var position))
            {
                action = new PreviewAction(true, tick, position);
                return true;
            }
            return false;
        }

        private static bool TryValue(IReadOnlyDictionary<string, string> fields, string key, out int value)
        {
            fields.TryGetValue(key, out var raw);
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private readonly struct PreviewAction
        {
            internal readonly bool IsCorrection;
            internal readonly int AnchorTick;
            internal readonly int Value;

            internal PreviewAction(bool isCorrection, int anchorTick, int value)
            {
                IsCorrection = isCorrection;
                AnchorTick = anchorTick;
                Value = value;
            }
        }
    }
}
