#nullable enable

using System;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Foundation
{
    /// <summary>
    /// 展示时间由宿主环境推进，而不是 sample 自己阻塞或拥有主循环。
    /// </summary>
    [Sample(12, "foundation", "environment", "tick", "web", "deterministic")]
    public sealed class EnvironmentTick : SampleBase
    {
        public override string Title => "Environment Tick";
        public override string Description => "演示 ISampleEnvironment 如何由宿主按帧推进，并把 delta/time 传给纯逻辑示例";
        public override SampleCategory Category => SampleCategory.Foundation;

        protected override void OnRun()
        {
            var tickCount = 0;
            var accumulated = 0f;

            Environment.OnTick += OnTick;

            Section("宿主负责推进时间");
            KeyValue("InitialTime", Time.ToString("F3"));
            KeyValue("InitialDelta", Environment.DeltaTime.ToString("F3"));
            KeyValue("EnvironmentTick.InitialTime", Time.ToString("F3"));
            KeyValue("EnvironmentTick.InitialDelta", Environment.DeltaTime.ToString("F3"));
            Bullet("Sample 不创建线程、不 sleep，也不直接绑定 Unity Update 或浏览器 requestAnimationFrame。");
            Bullet("宿主只需要调用 Environment.Advance(delta)，逻辑层就能收到 OnTick。");

            Divider();
            Section("模拟 5 个固定帧");
            SimulateFrames(5, 0.02f);
            KeyValue("EnvironmentTick.TickCount", tickCount.ToString());
            KeyValue("EnvironmentTick.AccumulatedDelta", accumulated.ToString("F3"));

            Divider();
            Section("暂停不会推进");
            Pause();
            AdvanceTime(0.02f);
            KeyValue("Paused", Environment.IsPaused.ToString());
            KeyValue("TimeWhilePaused", Time.ToString("F3"));
            KeyValue("EnvironmentTick.Paused", Environment.IsPaused.ToString());
            KeyValue("EnvironmentTick.TimeWhilePaused", Time.ToString("F3"));

            Resume();
            AdvanceTime(0.04f);
            KeyValue("PausedAfterResume", Environment.IsPaused.ToString());
            KeyValue("FinalTime", Time.ToString("F3"));
            KeyValue("TickCount", tickCount.ToString());
            KeyValue("AccumulatedDelta", accumulated.ToString("F3"));
            KeyValue("EnvironmentTick.PausedAfterResume", Environment.IsPaused.ToString());
            KeyValue("EnvironmentTick.FinalTime", Time.ToString("F3"));
            KeyValue("EnvironmentTick.TickCount", tickCount.ToString());
            KeyValue("EnvironmentTick.AccumulatedDelta", accumulated.ToString("F3"));

            Environment.OnTick -= OnTick;

            void OnTick(float delta)
            {
                tickCount++;
                accumulated += delta;
                KeyValue($"Frame[{tickCount}]", $"delta={delta:F3}, time={Time:F3}");
                KeyValue($"EnvironmentTick.Frame[{tickCount}]", $"delta={delta:F3}, time={Time:F3}");
            }
        }
    }
}
