using System.Collections.Generic;

namespace AbilityKit.BehaviorTree.Definition
{
    /// <summary>子树黑板键与父树黑板键之间的双向输入/输出绑定。</summary>
    public sealed class SubtreeBlackboardBinding
    {
        public string SubtreeKey { get; set; } = "";
        public string ParentKey { get; set; } = "";
    }

    /// <summary>单个子树引用节点的黑板作用域配置。</summary>
    public sealed class SubtreeBlackboardConfiguration
    {
        /// <summary>未显式绑定的子树键是否使用该子树实例的局部命名空间。</summary>
        public bool IsolateUnmappedKeys { get; set; }

        public List<SubtreeBlackboardBinding> Bindings { get; set; } = new();
    }
}
