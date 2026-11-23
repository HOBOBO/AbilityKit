using System;
using System.Collections.Generic;

namespace AbilityKit.Pipeline.Examples
{
    /// <summary>
    /// 示例：管线配置实现
    /// </summary>
    public class DefaultAbilityPipelineConfig : IAbilityPipelineConfig
    {
        public int ConfigId => 0;
        public string ConfigName => "Default";
        public IReadOnlyList<IAbilityPhaseConfig> PhaseConfigs => _phaseConfigs;
        public bool AllowInterrupt => true;
        public bool AllowPause => true;

        private readonly List<IAbilityPhaseConfig> _phaseConfigs = new List<IAbilityPhaseConfig>();

        public DefaultAbilityPipelineConfig() { }
    }

    /// <summary>
    /// 示例：使用模拟时间提供者（非 Unity 环境）
    /// 演示如何在单元测试或服务器端使用管线系统
    /// </summary>
    public class SimulatedTimeProvider : ITimeProvider
    {
        private float _currentTime;

        public SimulatedTimeProvider(float initialTime = 0f)
        {
            _currentTime = initialTime;
        }

        /// <summary>
        /// 获取自系统启动以来的实时时间（秒）
        /// </summary>
        public float RealtimeSinceStartup => _currentTime;

        /// <summary>
        /// 前进时间
        /// </summary>
        public void Advance(float deltaTime)
        {
            _currentTime += deltaTime;
        }

        /// <summary>
        /// 前进到指定时间
        /// </summary>
        public void SeekTo(float time)
        {
            _currentTime = time;
        }

        /// <summary>
        /// 重置时间
        /// </summary>
        public void Reset()
        {
            _currentTime = 0f;
        }
    }

    /// <summary>
    /// 示例：管线模拟运行器
    /// 用于测试环境中运行管线
    /// </summary>
    public class PipelineSimulator
    {
        private readonly SimulatedTimeProvider _timeProvider;

        public PipelineSimulator(SimulatedTimeProvider timeProvider)
        {
            _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        }

        /// <summary>
        /// 前进时间并Tick
        /// </summary>
        public void AdvanceAndTick(IAbilityPipelineRun<ExampleAbilityPipelineContext> run, float deltaTime)
        {
            _timeProvider.Advance(deltaTime);
            run.Tick(deltaTime);
        }
    }

    /// <summary>
    /// 示例：管线单元测试
    /// </summary>
    public static class PipelineUnitTestExample
    {
        public static void TestDelayPhase()
        {
            var timeProvider = new SimulatedTimeProvider();
            var context = new ExampleAbilityPipelineContext();
            var pipeline = new ExampleAbilityPipeline();
            var delayPhase = new AbilityDelayPhase<ExampleAbilityPipelineContext>(1.0f);

            pipeline.AddPhase(delayPhase);
            var run = pipeline.Start(new DefaultAbilityPipelineConfig(), context);

            var simulator = new PipelineSimulator(timeProvider);

            try
            {
                simulator.AdvanceAndTick(run, 0f);
                simulator.AdvanceAndTick(run, 0.5f);
                if (run.State != EAbilityPipelineState.Executing)
                    throw new InvalidOperationException("延迟阶段不应在半秒时完成。");

                simulator.AdvanceAndTick(run, 0.5f);
                if (run.State != EAbilityPipelineState.Completed)
                    throw new InvalidOperationException("延迟阶段应在累计一秒时完成。");
            }
            finally
            {
                run.Release();
            }
        }

        public static void TestInterruptBehavior()
        {
            var context = new ExampleAbilityPipelineContext();
            var pipeline = new ExampleAbilityPipeline();
            pipeline.AddPhase(new AbilityDelayPhase<ExampleAbilityPipelineContext>(1f));

            var run = pipeline.Start(new DefaultAbilityPipelineConfig(), context);
            try
            {
                run.Tick(0f);
                run.Interrupt();
                if (!context.IsAborted || run.State != EAbilityPipelineState.Failed)
                    throw new InvalidOperationException("中断后应标记上下文，并结束当前运行。");
            }
            finally
            {
                run.Release();
            }
        }
    }
}
