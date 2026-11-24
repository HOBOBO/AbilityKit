using System;
using System.Collections.Generic;
using AbilityKit.Scenario;

namespace AbilityKit.BattleScenario
{
    /// <summary>
    /// 在预检阶段将场景命令解析为宿主动作数据。返回失败表示名称或参数不受支持。
    /// 绑定不得执行动作或修改模拟状态；绑定器抛出的异常直接传给调用方。
    /// </summary>
    public delegate bool TryBindFixedTickCommand<TAction>(TestCommand command, out TAction action);

    /// <summary>
    /// 一条已绑定的命令及其固定步长位置。动作数据由宿主定义和持有。
    /// </summary>
    public readonly struct FixedTickCommand<TAction>
    {
        internal FixedTickCommand(TestCommand command, int tick, int sourceIndex, TAction action)
        {
            Name = command.Name;
            AtMs = command.AtMs;
            Tick = tick;
            SourceIndex = sourceIndex;
            Action = action;
        }

        /// <summary>
        /// 来源命令名称。
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 来源命令的虚拟时间，单位为毫秒。
        /// </summary>
        public int AtMs { get; }

        /// <summary>
        /// 从零开始的虚拟 Tick；不表示任何同步模式的权威帧。
        /// </summary>
        public int Tick { get; }

        /// <summary>
        /// 原命令在场景 IR 中的零基索引，用于定位和同 Tick 排序。
        /// </summary>
        public int SourceIndex { get; }

        /// <summary>
        /// 预检阶段已绑定的宿主动作数据；计划不负责复制该值或驱动它。
        /// </summary>
        public TAction Action { get; }
    }

    /// <summary>
    /// 可选的命令计划编译器；不接管场景执行器、时钟推进或业务状态。
    /// </summary>
    public static class FixedTickCommandPlan
    {
        /// <summary>
        /// 同步预检整个命令列表，按 Tick、原声明索引返回只读计划。毫秒必须精确对齐固定 Tick，不做取整。
        /// 此方法只计划 <see cref="TestScenario.Commands"/>，不执行其他语义区或收尾阶段。
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// 场景结构、时间范围、Tick 对齐或动作绑定不合法。一次报告所有校验失败，不返回部分计划。
        /// </exception>
        public static IReadOnlyList<FixedTickCommand<TAction>> Compile<TAction>(
            TestScenario scenario, TryBindFixedTickCommand<TAction> tryBind)
        {
            if (tryBind == null) throw new ArgumentNullException(nameof(tryBind));

            var errors = new List<string>(TestScenarioValidator.Validate(scenario));
            var tickRate = scenario.ResolveExecution().TickRate;
            var validTickRate = tickRate is >= 1 and <= 240;
            var steps = new List<FixedTickCommand<TAction>>(scenario.Commands.Count);
            for (var index = 0; index < scenario.Commands.Count; index++)
            {
                var command = scenario.Commands[index];
                var tickUnits = (long)command.AtMs * tickRate;
                var validClock = validTickRate && command.AtMs >= 0;
                var aligned = validClock && tickUnits % 1000 == 0;
                var location = $"commands[{index}] '{command.Name}' @ {command.AtMs}ms";
                if (validClock && !aligned)
                    errors.Add($"{location} 未对齐固定 Tick（{tickRate}Hz）");

                if (string.IsNullOrWhiteSpace(command.Name))
                    continue;
                if (!tryBind(command, out var action))
                {
                    errors.Add($"{location} 未绑定或参数不受支持");
                    continue;
                }

                if (aligned)
                    steps.Add(new FixedTickCommand<TAction>(command, checked((int)(tickUnits / 1000)), index, action));
            }

            if (errors.Count != 0)
                throw new InvalidOperationException($"场景 '{scenario.CaseId}' 命令计划预检失败：{string.Join("; ", errors)}");

            steps.Sort((left, right) => left.Tick != right.Tick
                ? left.Tick.CompareTo(right.Tick)
                : left.SourceIndex.CompareTo(right.SourceIndex));
            return steps.AsReadOnly();
        }
    }
}
