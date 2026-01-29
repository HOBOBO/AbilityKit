namespace AbilityKit.Ability
{
    /// <summary>
    /// 延迟阶段 - 等待指定时间后继续
    /// </summary>
    public class AbilityDelayPhase<TCtx> : AbilityDurationalPhaseBase<TCtx>
        where TCtx : IAbilityPipelineContext
    {
        /// <summary>
        /// 延迟时间（秒）
        /// </summary>
        public float DelayTime
        {
            get => Duration;
            set => Duration = value;
        }

        public AbilityDelayPhase(float delayTime) : base("Delay")
        {
            DelayTime = delayTime;
        }

        public AbilityDelayPhase(AbilityPipelinePhaseId phaseId, float delayTime) : base(phaseId)
        {
            DelayTime = delayTime;
        }

        protected override void OnExecute(TCtx context)
        {
            // 延迟阶段不需要执行任何逻辑，只是等待时间
        }

        /// <summary>
        /// 创建延迟阶段
        /// </summary>
        public static AbilityDelayPhase<TCtx> Create(float delayTime)
        {
            return new AbilityDelayPhase<TCtx>(delayTime);
        }
    }
}

