#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Pipeline;
using AbilityKit.Samples.Abstractions;
using AbilityKit.Samples.Logic.Infrastructure.Config;
using AbilityKit.Samples.Logic.Samples.Config;

namespace AbilityKit.Samples.Logic.Samples.Pipeline
{
    /// <summary>
    /// PipelineBasics - 管线基础示例
    /// 展示配置 + 执行器 + 对象池的完整模式
    /// </summary>
    [Sample]
    public sealed class PipelineBasics : SampleBase
    {
        public override string Title => "Pipeline Basics";
        public override string Description => "演示配置 + 执行器 + 对象池模式";
        public override SampleCategory Category => SampleCategory.Pipeline;

        protected override void OnRun()
        {
            Log("管线(Pipeline) 基础示例");
            Divider();

            // 示例1：代码组合构建管线
            ShowCodeBasedPipeline();

            Divider();

            // 示例2：从JSON加载配置并执行
            ShowJsonBasedPipeline();

            Log("\n示例运行完成");
        }

        private void ShowCodeBasedPipeline()
        {
            Section("示例1：代码组合构建即时管线");

            var context = new SamplePipelineContext();
            context.SetData("targetValid", true);
            context.SetData("manaCost", 30f);
            context.SetData("currentMana", 50f);
            KeyValue("CodePipeline.InitialTargetValid", context.GetData<bool>("targetValid").ToString());
            KeyValue("CodePipeline.InitialMana", context.GetData<float>("currentMana").ToString());
            KeyValue("CodePipeline.ManaCost", context.GetData<float>("manaCost").ToString());

            var pipeline = new InstantAbilityPipeline<SamplePipelineContext>();

            pipeline.AddPhase(new SampleInstantPhase("PreCheck", (ctx) =>
            {
                Log("  [PreCheck] 检查目标是否有效...");
                bool valid = ctx.GetData<bool>("targetValid");
                if (!valid)
                {
                    ctx.IsAborted = true;
                    Log("    -> 目标无效，中止管线");
                }
                else
                {
                    Log("    -> 目标有效，继续执行");
                }
            }));

            pipeline.AddPhase(new SampleInstantPhase("Validation", (ctx) =>
            {
                Log("  [Validation] 验证资源是否足够...");
                float manaCost = ctx.GetData<float>("manaCost");
                float currentMana = ctx.GetData<float>("currentMana");
                if (currentMana < manaCost)
                {
                    ctx.IsAborted = true;
                    Log($"    -> 魔法值不足 ({currentMana}/{manaCost})，中止管线");
                }
                else
                {
                    Log($"    -> 资源足够，消耗 {manaCost} 魔法值");
                    ctx.SetData("currentMana", currentMana - manaCost);
                }
            }));

            pipeline.AddPhase(new SampleInstantPhase("Execute", (ctx) =>
            {
                Log("  [Execute] 执行技能效果!");
                Log("    -> 造成 100 点伤害");
            }));

            Log("开始执行技能管线:");
            var result = pipeline.RunToCompletion(new SamplePipelineConfig(), context);
            Log($"管线结束，状态: {result.State}");
            KeyValue("CodePipeline.ResultState", result.State.ToString());
            KeyValue("CodePipeline.LastPhase", result.LastPhaseId.ToString());
            KeyValue("CodePipeline.FinalMana", context.GetData<float>("currentMana").ToString());
        }

        /// <summary>
        /// 从PipelineConfig.json加载配置并执行
        /// 展示：JSON配置 + Attribute类型映射 + 对象池执行器
        /// </summary>
        private void ShowJsonBasedPipeline()
        {
            Section("示例2：从PipelineConfig.json加载配置执行管线");

            // 1. 加载配置
            var pipelineConfigs = SampleConfig.LoadPipelines<JsonPipelineDef>();

            Log($"已加载配置文件，包含 {pipelineConfigs.Count} 个管线");

            // 选择第一个管线作为演示
            var pipelineDef = pipelineConfigs[0];
            Log($"\n选择管线: [{pipelineDef.Id}] {pipelineDef.Name}");
            Log($"描述: {pipelineDef.Description}");
            Log($"阶段数量: {pipelineDef.Phases.Count}");
            KeyValue("ConfigPipeline.Id", pipelineDef.Id.ToString());
            KeyValue("ConfigPipeline.PhaseCount", pipelineDef.Phases.Count.ToString());

            foreach (var phase in pipelineDef.Phases)
            {
                Log($"  - {phase.Name} ({phase.Type}, duration={phase.Duration})");
            }

            // 2. 创建上下文和管线
            var context = new SamplePipelineContext();
            context.SetData("targetValid", true);
            context.SetData("currentRange", 15f);
            context.SetData("currentMana", 100f);
            context.SetData("isSilenced", false);

            var pipeline = new InstantAbilityPipeline<SamplePipelineContext>();

            Log("\n构建并执行管线:");

            // 3. 通过阶段名称从Registry获取类型并创建实例
            foreach (var phaseDef in pipelineDef.Phases)
            {
                // 从 Registry 根据阶段名称获取类型
                if (!PipelinePhaseRegistry.Instance.TryGet(phaseDef.Name, out var phaseType))
                {
                    Log($"  [跳过] 未知阶段: {phaseDef.Name}");
                    continue;
                }

                // 创建阶段实例
                var phaseInstance = PipelinePhaseRegistry.Instance.CreatePhase(phaseDef.Name);
                Log($"  [{phaseDef.Name}] -> {phaseType.Name}");

                // 从池获取执行器（对象池是默认约定，隐式使用）
                using var pooled = PhaseExecutorRegistry.Instance.RentExecutor(phaseInstance);
                if (pooled.Executor == null)
                {
                    Log($"    [WARN] 未找到执行器");
                    continue;
                }

                var execContext = PhaseExecutorContext.Create(context, Log);

                pipeline.AddPhase(new SampleInstantPhase(phaseDef.Name, (ctx) =>
                {
                    pooled.Executor.Execute(phaseInstance, execContext);
                }));

                Log($"    -> {pooled.Executor.GetType().Name}");
            }

            Log("\n执行管线:");
            var result = pipeline.RunToCompletion(new SamplePipelineConfig(), context);
            Log($"\n结果: {result.State}");
            KeyValue("ConfigPipeline.ResultState", result.State.ToString());
            KeyValue("ConfigPipeline.LastPhase", result.LastPhaseId.ToString());

            Log("\n执行后:");
            Log($"  魔法值: {context.GetData<float>("currentMana")}");
            Log($"  冷却中: {context.GetData<bool>("isOnCooldown")}");
            KeyValue("ConfigPipeline.FinalMana", context.GetData<float>("currentMana").ToString());
            KeyValue("ConfigPipeline.IsOnCooldown", context.GetData<bool>("isOnCooldown").ToString());
        }
    }

    #region 管线组件实现

    public class SamplePipelineContext : IAbilityPipelineContext
    {
        public object AbilityInstance { get; set; }
        public AbilityPipelinePhaseId CurrentPhaseId { get; set; }
        public EAbilityPipelineState PipelineState { get; set; }
        public bool IsAborted { get; set; }
        public bool IsPaused { get; set; }
        public float StartTime { get; set; }
        public float ElapsedTime { get; set; }
        private readonly Dictionary<string, object> _sharedData = new();

        public Dictionary<string, object> SharedData => _sharedData;

        public T GetData<T>(string key, T defaultValue = default)
        {
            if (_sharedData.TryGetValue(key, out var value) && value is T typedValue)
                return typedValue;
            return defaultValue;
        }

        public void SetData<T>(string key, T value)
        {
            _sharedData[key] = value;
        }

        public bool TryGetData<T>(string key, out T value)
        {
            if (_sharedData.TryGetValue(key, out var obj) && obj is T typedValue)
            {
                value = typedValue;
                return true;
            }
            value = default;
            return false;
        }

        public bool RemoveData(string key) => _sharedData.Remove(key);
        public void ClearData() => _sharedData.Clear();

        public void Reset()
        {
            AbilityInstance = null;
            CurrentPhaseId = default;
            PipelineState = EAbilityPipelineState.Ready;
            IsAborted = false;
            IsPaused = false;
            StartTime = 0;
            ElapsedTime = 0;
            _sharedData.Clear();
        }

        public PhaseExecutorContext CreateExecutorContext() => PhaseExecutorContext.Create(this, null);
    }

    public class SamplePipelineConfig : IAbilityPipelineConfig
    {
        public int ConfigId => 0;
        public string ConfigName => "SampleConfig";
        public IReadOnlyList<IAbilityPhaseConfig> PhaseConfigs => Array.Empty<IAbilityPhaseConfig>();
        public bool AllowInterrupt => true;
        public bool AllowPause => true;
    }

    public class SampleInstantPhase : AbilityInstantPhaseBase<SamplePipelineContext>
    {
        private readonly Action<SamplePipelineContext> _action;

        public SampleInstantPhase(string name, Action<SamplePipelineContext> action) : base(name)
        {
            _action = action;
        }

        protected override void OnInstantExecute(SamplePipelineContext context)
        {
            _action?.Invoke(context);
        }
    }

    public class SampleDelayPhase : AbilityDurationalPhaseBase<SamplePipelineContext>
    {
        private readonly Action<SamplePipelineContext> _onTick;

        public SampleDelayPhase(string name, float duration, Action<SamplePipelineContext> onTick) : base(name)
        {
            Duration = duration;
            _onTick = onTick;
        }

        protected override void OnExecute(SamplePipelineContext context) { }

        protected override void OnTick(SamplePipelineContext context, float deltaTime)
        {
            _onTick?.Invoke(context);
        }
    }

    public class SampleSkillPipeline : AbilityPipeline<SamplePipelineContext>
    {
        protected override void ReleaseContext(SamplePipelineContext context) { }
    }

    #endregion

    #region JSON配置模型

    /// <summary>
    /// JSON管线定义
    /// </summary>
    public sealed class JsonPipelineDef
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<JsonPhaseDef> Phases { get; set; } = new();
    }

    /// <summary>
    /// JSON阶段定义
    /// </summary>
    public sealed class JsonPhaseDef
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public float Duration { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    #endregion
}
