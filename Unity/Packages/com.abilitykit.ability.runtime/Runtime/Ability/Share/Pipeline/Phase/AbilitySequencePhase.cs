namespace AbilityKit.Ability
{
    /// <summary>
    /// 顺序执行阶段
    /// 按顺序依次执行子阶段
    /// </summary>
    public class AbilitySequencePhase : AbilityCompositePhase
    {
        public AbilitySequencePhase() : base() { }
        
        public AbilitySequencePhase(AbilityPipelinePhaseId phaseId) : base(phaseId) { }

        // 继承基类的顺序执行逻辑
    }
}