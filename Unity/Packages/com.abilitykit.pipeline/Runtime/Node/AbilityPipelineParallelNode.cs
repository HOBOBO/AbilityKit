using System;
using System.Collections.Generic;

namespace AbilityKit.Ability
{
    /// <summary>
    /// 并行节点
    /// </summary>
    public class AbilityPipelineParallelNode : AbilityPipelineNode
    {
        private List<AbilityPipelineNode> _children = new();
        private Dictionary<string, IAbilityPipelineNodeExecuteResult> _childResults = new();
    
        protected override IAbilityPipelineNodeExecuteResult OnExecute(IAbilityPipelineContext context)
        {
            bool allCompleted = true;
        
            foreach (var child in _children)
            {
                if (!_childResults.ContainsKey(child.Id))
                {
                    var result = child.Execute(context);
                    if (result.IsCompleted)
                    {
                        _childResults[child.Id] = result;
                    }
                    else
                    {
                        allCompleted = false;
                    }
                }
            }
        
            var _result = PooledAbilityPipelineNodeExecuteResult.Rent();
            _result.IsCompleted = allCompleted;
            if (allCompleted)
            {
                _result.EnsureActiveOutputPorts();
                _result.ActiveOutputPorts.Add("Complete");
            }
            return _result;
        }
    }
}