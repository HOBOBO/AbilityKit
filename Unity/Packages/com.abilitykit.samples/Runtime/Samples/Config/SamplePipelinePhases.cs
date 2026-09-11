#nullable enable

using AbilityKit.Samples.Logic.Infrastructure.Config;
using AbilityKit.Samples.Logic.Infrastructure.Config.Attributes;
using AbilityKit.Samples.Logic.Samples.Pipeline;

namespace AbilityKit.Samples.Logic.Samples.Config
{
    /// <summary>
    /// 棰勬鏌ラ樁娈?- 楠岃瘉鐩爣鏈夋晥鎬?    /// </summary>
    [PipelinePhaseTypeId("PreCheck", isTimed: false)]
    [ExecutorFor(typeof(PreCheckExecutor))]
    public sealed class PreCheckPhase
    {
        public bool RequireTarget { get; set; }
        public float MinRange { get; set; }
        public float MaxRange { get; set; }

        public PreCheckPhase()
        {
            RequireTarget = true;
            MinRange = 0f;
            MaxRange = float.MaxValue;
        }
    }

    /// <summary>
    /// 楠岃瘉闃舵 - 妫€鏌ヨ祫婧愭槸鍚﹁冻澶?    /// </summary>
    [PipelinePhaseTypeId("Validation", isTimed: false)]
    [ExecutorFor(typeof(ValidationExecutor))]
    public sealed class ValidationPhase
    {
        public float RequiredMana { get; set; }
        public bool CheckSilence { get; set; } = true;
    }

    /// <summary>
    /// 施法引导阶段 - 需要时间完成的阶段
    /// </summary>
    [PipelinePhaseTypeId("Casting", isTimed: true)]
    [ExecutorFor(typeof(CastingExecutor))]
    public sealed class CastingPhase
    {
        public float CastDuration { get; set; } = 1.5f;
        public bool CanMove { get; set; } = false;
        public bool CanRotate { get; set; } = true;
        public string CastAnimation { get; set; }
    }

    /// <summary>
    /// 执行阶段 - 产生效果
    /// </summary>
    [PipelinePhaseTypeId("Execute", isTimed: false)]
    [ExecutorFor(typeof(ExecuteExecutor))]
    public sealed class ExecutePhase
    {
        public float Damage { get; set; }
        public float EffectRadius { get; set; }
        public string EffectType { get; set; } = "Physical";
    }

    /// <summary>
    /// 冷却阶段 - 进入冷却时间
    /// </summary>
    [PipelinePhaseTypeId("Cooldown", isTimed: true)]
    [ExecutorFor(typeof(CooldownExecutor))]
    public sealed class CooldownPhase
    {
        public float CooldownDuration { get; set; } = 5f;
    }

    /// <summary>
    /// 鎸佺画鏃堕棿闃舵 - 淇濇寔鏌愮鐘舵€?    /// </summary>
    [PipelinePhaseTypeId("Duration", isTimed: true)]
    public sealed class DurationPhase
    {
        public float Duration { get; set; } = 1f;
        public bool CanMove { get; set; } = true;
        public bool CanRotate { get; set; } = true;
    }
}
