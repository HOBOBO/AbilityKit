#nullable enable

using System;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Flow
{
    /// <summary>
    /// TimedFlow - 带时间的流程
    /// </summary>
    [Sample]
    public sealed class TimedFlow : SampleBase
    {
        public override string Title => "Timed Flow";
        public override string Description => "演示带时间推进的流程模拟";
        public override SampleCategory Category => SampleCategory.Flow;

        private string _state = "Idle";
        private int _tickCount = 0;

        protected override void OnRun()
        {
            Log("带时间推进的流程模拟");
            Output.Divider();

            // 订阅 Tick 事件
            Environment.OnTick += OnTick;

            Log($"初始状态: {_state}");
            Output.Divider();

            // 模拟状态机: Idle -> Charging -> Firing -> Cooldown -> Idle
            SimulateStateMachine();

            Environment.OnTick -= OnTick;
        }

        private void OnTick(float delta)
        {
            _tickCount++;
        }

        private void SimulateStateMachine()
        {
            Log("=== 状态机模拟 ===");

            // Idle 阶段(0-1秒)
            Log("进入 Idle 阶段...");
            _state = "Idle";
            AdvanceTime(1.0f);
            Log($"  当前时间: {Time:F1}s, 状态: {_state}");

            // Charging 阶段(1-2秒)
            Log("进入 Charging 阶段...");
            _state = "Charging";
            AdvanceTime(1.0f);
            Log($"  当前时间: {Time:F1}s, 状态: {_state}");

            // Firing 阶段(2-3秒)
            Log("进入 Firing 阶段...");
            _state = "Firing";
            AdvanceTime(1.0f);
            Log($"  当前时间: {Time:F1}s, 状态: {_state}");

            // Cooldown 阶段(3-4秒)
            Log("进入 Cooldown 阶段...");
            _state = "Cooldown";
            AdvanceTime(1.0f);
            Log($"  当前时间: {Time:F1}s, 状态: {_state}");

            // 返回 Idle
            Log("返回 Idle 阶段...");
            _state = "Idle";
            Log($"  当前时间: {Time:F1}s, 状态: {_state}");

            Output.Divider();
            Log($"总 Tick 数: {_tickCount}");
            Log($"总耗时: {Time:F1}秒");
        }
    }
}
