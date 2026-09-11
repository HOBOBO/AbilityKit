#nullable enable

using AbilityKit.Samples.Logic.Infrastructure.Config.Attributes;

namespace AbilityKit.Samples.Logic.Samples.Config
{
    /// <summary>
    /// 閫夋嫨鍣ㄨ妭鐐?- 渚濇鎵ц瀛愯妭鐐癸紝杩斿洖绗竴涓垚鍔熺殑
    /// </summary>
    [BTNodeTypeId("Selector")]
    public sealed class SelectorBTNode { }

    /// <summary>
    /// 序列节点 - 依次执行子节点，返回第一个失败的
    /// </summary>
    [BTNodeTypeId("Sequence")]
    public sealed class SequenceBTNode { }

    /// <summary>
    /// 鏉′欢鑺傜偣 - 鎵ц鏉′欢妫€鏌?
    /// </summary>
    [BTNodeTypeId("Condition")]
    public sealed class ConditionBTNode { }

    /// <summary>
    /// 动作节点 - 执行具体动作
    /// </summary>
    [BTNodeTypeId("Action")]
    public sealed class ActionBTNode { }

    /// <summary>
    /// 并行节点 - 同时执行所有子节点
    /// </summary>
    [BTNodeTypeId("Parallel")]
    public sealed class ParallelBTNode { }

    /// <summary>
    /// 寰幆鑺傜偣 - 閲嶅鎵ц瀛愯妭鐐?
    /// </summary>
    [BTNodeTypeId("Loop")]
    public sealed class LoopBTNode { }
}
