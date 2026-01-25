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
        
        public static AbilityPipelinePhaseResult Success =>
            AbilityPipelinePhaseResultManager.Instance.Register(SuccessName);
        
    }
    
    public class AbilityPipelinePhaseResultManager
    {
        private static readonly Lazy<AbilityPipelinePhaseResultManager> _instance = new Lazy<AbilityPipelinePhaseResultManager>(() => new AbilityPipelinePhaseResultManager());

        public static AbilityPipelinePhaseResultManager Instance => _instance.Value;

        private readonly StableStringEnumIdManager<AbilityPipelinePhaseResult> _impl =
            new StableStringEnumIdManager<AbilityPipelinePhaseResult>(raw => new AbilityPipelinePhaseResult(raw));

        public AbilityPipelinePhaseResult Register(string name) => _impl.Register(name);
    }
}