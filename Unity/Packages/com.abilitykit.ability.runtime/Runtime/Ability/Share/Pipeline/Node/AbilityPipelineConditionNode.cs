using System;
using System.Collections.Generic;

namespace AbilityKit.Ability
{
    public class AbilityPipelineConditionNode : AbilityPipelineNode
    {
        private Func<IAbilityPipelineContext, bool> _condition;
    
        protected override IAbilityPipelineNodeExecuteResult OnExecute(IAbilityPipelineContext context)
        {
            var result = PooledAbilityPipelineNodeExecuteResult.Rent();
            result.IsCompleted = true;
            result.EnsureActiveOutputPorts();
        
            // 根据条件激活不同的输出端口
            result.ActiveOutputPorts.Add(_condition(context) ? "True" : "False");
        
            return result;
        }
    }
}