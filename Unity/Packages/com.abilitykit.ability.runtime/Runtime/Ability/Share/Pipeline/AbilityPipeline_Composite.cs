namespace AbilityKit.Ability
{
    /// <summary>
    /// AbilityPipeline - 复合阶段处理
    /// </summary>
    public abstract partial class AbilityPipeline
    {
        protected AbilityParallelPhase _currentParallelPhase;

        /// <summary>
        /// 处理复合阶段
        /// </summary>
        protected virtual void HandleCompositePhase(AbilityCompositePhase phase, IAbilityPipelineContext context)
        {
            phase.Execute(context);
        
            // 如果是并行阶段，记录下来用于特殊更新
            if (phase is AbilityParallelPhase parallelPhase)
            {
                _currentParallelPhase = parallelPhase;
            }
        }

        /// <summary>
        /// 更新复合阶段（主要是并行阶段的更新）
        /// </summary>
        protected void OnCompositeUpdate(IAbilityPipelineContext context, float deltaTime)
        {
            // 并行阶段需要额外更新其所有子阶段
            if (_currentParallelPhase != null)
            {
                _currentParallelPhase.OnUpdate(context, deltaTime);
                
                if (_currentParallelPhase.IsComplete)
                {
                    _currentParallelPhase = null;
                }
            }
        }
    }
}