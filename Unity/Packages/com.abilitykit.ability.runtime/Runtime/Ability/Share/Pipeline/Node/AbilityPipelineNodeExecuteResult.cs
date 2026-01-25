using System.Collections.Generic;

namespace AbilityKit.Ability
{
    /// <summary>
    /// 默认节点执行结果
    /// </summary>
    public class AbilityPipelineNodeExecuteResult : IAbilityPipelineNodeExecuteResult
    {
        public bool IsCompleted { get; set; }
        public Dictionary<string, object> OutputData;
        public List<string> ActiveOutputPorts;
    }
}