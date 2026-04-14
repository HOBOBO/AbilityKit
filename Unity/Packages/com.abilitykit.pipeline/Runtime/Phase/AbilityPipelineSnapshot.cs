using System.Collections.Generic;

namespace AbilityKit.Pipeline
{
    /// <summary>
    /// 技能流水线阶段快照
    /// </summary>
    public class AbilityPipelineSnapshot
    {
        public Dictionary<string, object> PhaseStates { get; set; }
        public IAbilityPipelineContext Context { get; set; }
    }
}
