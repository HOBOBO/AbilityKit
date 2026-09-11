#nullable enable

using System;
using AbilityKit.Core.Pooling;
using AbilityKit.Samples.Logic.Samples.Config;

namespace AbilityKit.Samples.Logic.Samples.Pipeline
{
    /// <summary>
    /// 阶段执行器上下文
    /// 包含执行阶段所需的所有信息
    /// </summary>
    public sealed class PhaseExecutorContext
    {
        public SamplePipelineContext PipelineContext { get; set; }
        public Action<string> Log { get; set; }
        public Action<string> Warn { get; set; }
        public Action<string> Error { get; set; }

        public static PhaseExecutorContext Create(SamplePipelineContext pipelineContext, Action<string> log)
        {
            return new PhaseExecutorContext
            {
                PipelineContext = pipelineContext,
                Log = log ?? (_ => { }),
                Warn = _ => { },
                Error = _ => { }
            };
        }
    }

    /// <summary>
    /// 阶段执行器接口
    /// 支持对象池复用
    /// </summary>
    public interface IPhaseExecutor : IPoolable
    {
        /// <summary>
        /// 执行阶段逻辑
        /// </summary>
        void Execute(object config, PhaseExecutorContext context);
    }

    /// <summary>
    /// 阶段执行器基类
    /// </summary>
    public abstract class PhaseExecutorBase<TConfig> : IPhaseExecutor
        where TConfig : class
    {
        public abstract void Execute(TConfig config, PhaseExecutorContext context);

        public void Execute(object config, PhaseExecutorContext context)
        {
            if (config is TConfig typedConfig)
            {
                Execute(typedConfig, context);
            }
            else
            {
                context.Error?.Invoke($"配置类型不匹配: 期望 {typeof(TConfig).Name}, 实际 {config.GetType().Name}");
            }
        }

        public virtual void OnPoolGet() { }
        public virtual void OnPoolRelease() { }
        public virtual void OnPoolDestroy() { }
    }

    /// <summary>
    /// PreCheck 阶段执行器
    /// </summary>
    public sealed class PreCheckExecutor : PhaseExecutorBase<PreCheckPhase>
    {
        public PreCheckExecutor() { }

        public override void Execute(PreCheckPhase config, PhaseExecutorContext context)
        {
            context.Log?.Invoke($"  [PreCheck] 预检查阶段");
            context.Log?.Invoke($"    -> 要求目标: {config.RequireTarget}");
            context.Log?.Invoke($"    -> 距离范围: [{config.MinRange}, {config.MaxRange}]");

            bool targetValid = context.PipelineContext.GetData("targetValid", false);
            if (config.RequireTarget && !targetValid)
            {
                context.PipelineContext.IsAborted = true;
                context.Log?.Invoke($"    -> [ABORT] 目标无效，中止管线");
                return;
            }

            float currentRange = context.PipelineContext.GetData("currentRange", 0f);
            if (currentRange < config.MinRange || currentRange > config.MaxRange)
            {
                context.PipelineContext.IsAborted = true;
                context.Log?.Invoke($"    -> [ABORT] 距离超出范围 ({currentRange}), 中止管线");
                return;
            }

            context.Log?.Invoke($"    -> 预检查通过");
        }
    }

    /// <summary>
    /// Validation 阶段执行器
    /// </summary>
    public sealed class ValidationExecutor : PhaseExecutorBase<ValidationPhase>
    {
        public ValidationExecutor() { }

        public override void Execute(ValidationPhase config, PhaseExecutorContext context)
        {
            context.Log?.Invoke($"  [Validation] 资源验证阶段");

            float currentMana = context.PipelineContext.GetData("currentMana", 0f);
            context.Log?.Invoke($"    -> 当前资源: {currentMana}");
            context.Log?.Invoke($"    -> 消耗需求: {config.RequiredMana}");

            if (config.CheckSilence)
            {
                bool isSilenced = context.PipelineContext.GetData("isSilenced", false);
                context.Log?.Invoke($"    -> 检查沉默状态: {(isSilenced ? "是" : "否")}");
                if (isSilenced)
                {
                    context.PipelineContext.IsAborted = true;
                    context.Log?.Invoke($"    -> [ABORT] 角色被沉默，中止管线");
                    return;
                }
            }

            if (currentMana < config.RequiredMana)
            {
                context.PipelineContext.IsAborted = true;
                context.Log?.Invoke($"    -> [ABORT] 资源不足，中止管线");
                return;
            }

            context.PipelineContext.SetData("currentMana", currentMana - config.RequiredMana);
            context.Log?.Invoke($"    -> 消耗 {config.RequiredMana} 资源，剩余 {context.PipelineContext.GetData<float>("currentMana")}");
            context.Log?.Invoke($"    -> 资源验证通过");
        }
    }

    /// <summary>
    /// Casting 阶段执行器
    /// </summary>
    public sealed class CastingExecutor : PhaseExecutorBase<CastingPhase>
    {
        public CastingExecutor() { }

        public override void Execute(CastingPhase config, PhaseExecutorContext context)
        {
            context.Log?.Invoke($"  [Casting] 施法引导阶段");
            context.Log?.Invoke($"    -> 施法时长: {config.CastDuration}s");
            context.Log?.Invoke($"    -> 动画: {config.CastAnimation ?? "无"}");
            context.Log?.Invoke($"    -> 可移动: {(config.CanMove ? "是" : "否")}");
            context.Log?.Invoke($"    -> 可转向: {(config.CanRotate ? "是" : "否")}");
        }
    }

    /// <summary>
    /// Execute 阶段执行器
    /// </summary>
    public sealed class ExecuteExecutor : PhaseExecutorBase<ExecutePhase>
    {
        public ExecuteExecutor() { }

        public override void Execute(ExecutePhase config, PhaseExecutorContext context)
        {
            context.Log?.Invoke($"  [Execute] 技能效果执行阶段");
            context.Log?.Invoke($"    -> 伤害: {config.Damage}");
            context.Log?.Invoke($"    -> 效果半径: {config.EffectRadius}");
            context.Log?.Invoke($"    -> 效果类型: {config.EffectType}");

            context.Log?.Invoke($"    -> 造成 {config.Damage} 点 {config.EffectType} 伤害");
            context.Log?.Invoke($"    -> 效果已应用到 {config.EffectRadius} 范围内的目标");
        }
    }

    /// <summary>
    /// Cooldown 阶段执行器
    /// </summary>
    public sealed class CooldownExecutor : PhaseExecutorBase<CooldownPhase>
    {
        public CooldownExecutor() { }

        public override void Execute(CooldownPhase config, PhaseExecutorContext context)
        {
            context.Log?.Invoke($"  [Cooldown] 冷却阶段");
            context.Log?.Invoke($"    -> 冷却时长: {config.CooldownDuration}s");

            context.PipelineContext.SetData("isOnCooldown", true);
            context.PipelineContext.SetData("cooldownRemaining", config.CooldownDuration);
            context.Log?.Invoke($"    -> 技能进入冷却");
        }
    }
}
