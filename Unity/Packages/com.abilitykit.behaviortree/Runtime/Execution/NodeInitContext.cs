using AbilityKit.Deterministic;

using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Registry;
namespace AbilityKit.BehaviorTree.Execution
{
    /// <summary>
    /// 节点初始化上下文：定义、类型化属性、子节点数、注册中心、专属随机流
    /// 注意：不要用 init 访问器——Unity 编译环境（netstandard2.1）缺IsExternalInit
    /// </summary>
    public struct NodeInitContext
    {
        public TreeDefinition Tree { get; set; }
        public NodeDefinition Definition { get; set; }
        public PropertyReader Properties { get; set; }
        public int ChildCount { get; set; }
        public NodeRegistry Registry { get; set; }
        /// <summary>从树种子与节id 派生的独立随机流；快照会捕获其完整状�?/summary>
        public DeterministicRandom Random { get; set; }
        public ExecutionContext Context { get; set; }
    }
}
