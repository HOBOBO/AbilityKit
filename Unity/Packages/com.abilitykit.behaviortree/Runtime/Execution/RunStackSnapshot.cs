using System.Collections.Generic;

namespace AbilityKit.BehaviorTree.Execution
{
    /// <summary>运行栈（自底向顶的扁平索引序列）</summary>
    public sealed class RunStackSnapshot
    {
        public List<int> NodeIndexes { get; set; } = new();
    }
}
