
using System;

namespace AbilityKit.Ability
{
    /// <summary>
    /// 能力管线阶段执行结果
    /// </summary>
    public class AbilityPipelinePhaseResult : GenericEnumId<int>
    {
        public AbilityPipelinePhaseResult(int id) : base(id)
        {
        }
        /// <summary>
        /// 成功
        /// </summary>
        public const string SuccessName = "Success";
        
        public static AbilityPipelinePhaseResult Success =
            AbilityPipelinePhaseResultManager.Instance.Register(SuccessName);
        
    }
    
    public class AbilityPipelinePhaseResultManager : GenericIdManager<string, AbilityPipelinePhaseResult>
    {
        public static AbilityPipelinePhaseResultManager Instance = new AbilityPipelinePhaseResultManager(null);

        public AbilityPipelinePhaseResultManager(Func<AbilityPipelinePhaseResult, AbilityPipelinePhaseResult> incrementFunc) : base(incrementFunc)
        {
        }
    }
}