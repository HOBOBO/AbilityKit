using System.Collections.Generic;

namespace AbilityKit.BehaviorTree.Nodes
{
    /// <summary>
    /// 可选的条件依赖声明。运行时只会在依赖键变化后重评估该条件；
    /// 未实现此接口的条件保持逐 Tick 轮询语义。
    /// </summary>
    public interface ConditionDependencyProvider
    {
        IReadOnlyList<string> BlackboardDependencies { get; }
    }
}
