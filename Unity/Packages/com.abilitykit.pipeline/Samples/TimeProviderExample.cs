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
        private readonly ITimeProvider _timeProvider;
        private float _lastTickTime;

        public PipelineSimulator(ITimeProvider timeProvider)
        {
            _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            _lastTickTime = _timeProvider.RealtimeSinceStartup;
        }

        /// <summary>
        /// 运行管线直到完成
        /// </summary>
        public void RunToCompletion(IAbilityPipelineRun<ExampleAbilityPipelineContext> run, float maxTime = 100f)
        {
            while (run.State == EAbilityPipelineState.Executing)
            {
                var currentTime = _timeProvider.RealtimeSinceStartup;
                var deltaTime = currentTime - _lastTickTime;
                _lastTickTime = currentTime;

                run.Tick(deltaTime);

                if (currentTime > maxTime)
                {
                    run.Interrupt();
                    break;
                }
            }
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
            var pipeline = new AbilityPipeline<ExampleAbilityPipelineContext>();
            var delayPhase = new AbilityDelayPhase<ExampleAbilityPipelineContext>(1.0f);

            pipeline.AddPhase(delayPhase);
            var run = pipeline.Start(new DefaultAbilityPipelineConfig(), context);

            var simulator = new PipelineSimulator(timeProvider);

            // 验证 0.5s 时阶段未完成
            timeProvider.Advance(0.5f);
            simulator.RunToCompletion(run);
            Console.WriteLine($"After 0.5s: State = {run.State}");

            // 重置并测试完整运行
            timeProvider.Reset();
            context.Reset();
            run = pipeline.Start(new DefaultAbilityPipelineConfig(), context);

            timeProvider.Advance(1.0f);
            simulator.RunToCompletion(run);
            Console.WriteLine($"After 1.0s: State = {run.State}");
        }

        public static void TestInterruptBehavior()
        {
            var context = new ExampleAbilityPipelineContext();
            var pipeline = new AbilityPipeline<ExampleAbilityPipelineContext>();

            // 添加一个会被中断的条件阶段
            pipeline.AddPhase(new AbilityConditionalPhase<ExampleAbilityPipelineContext>(
                ctx => ctx.IsAborted,
                AbilityPipelinePhaseId.Interrupt));

            var run = pipeline.Start(new DefaultAbilityPipelineConfig(), context);
            context.IsAborted = true;

            run.Tick(0f);
            Console.WriteLine($"After interrupt: State = {run.State}");
        }
    }
}
