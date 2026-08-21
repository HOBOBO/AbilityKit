namespace AbilityKit.Pipeline
{
    /// <summary>
    /// 条件门控阶段：进入时判断条件，满足则立即完成，不满足则中止上下文。
    /// </summary>
    /// <typeparam name="TCtx">管线上下文类型。</typeparam>
    public sealed class AbilityGatePhase<TCtx> : AbilityInstantPhaseBase<TCtx>, IAbilityPipelinePhaseInstanceFactory<TCtx>
        where TCtx : IAbilityPipelineContext
    {
        private readonly IAbilityConditionNode _condition;

        public IAbilityConditionNode Condition => _condition;

        /// <summary>
        /// 创建条件门控阶段。
        /// </summary>
        public AbilityGatePhase(IAbilityConditionNode condition) : this(new AbilityPipelinePhaseId("Gate"), condition)
        {
        }

        /// <summary>
        /// 使用指定阶段 ID 创建条件门控阶段。
        /// </summary>
        public AbilityGatePhase(AbilityPipelinePhaseId phaseId, IAbilityConditionNode condition) : base(phaseId)
        {
            _condition = condition ?? throw new System.ArgumentNullException(nameof(condition));
        }

        /// <inheritdoc />
        protected override void OnInstantExecute(TCtx context)
        {
            if (!_condition.Evaluate(context))
            {
                context.IsAborted = true;
            }
        }

        /// <inheritdoc />
        public IAbilityPipelinePhase<TCtx> CreateRunPhase()
        {
            return new AbilityGatePhase<TCtx>(PhaseId, _condition);
        }

        /// <summary>
        /// 创建条件门控阶段。
        /// </summary>
        public static AbilityGatePhase<TCtx> Create(IAbilityConditionNode condition)
        {
            return new AbilityGatePhase<TCtx>(condition);
        }
    }
}
