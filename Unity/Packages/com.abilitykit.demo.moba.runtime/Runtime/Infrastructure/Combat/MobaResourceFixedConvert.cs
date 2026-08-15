using AbilityKit.Core.Mathematics;
using AbilityKit.Deterministic;

namespace AbilityKit.Demo.Moba
{
    /// <summary>
    /// 伤害/资源数值管线的 float↔Fixed64 边界换算：
    /// <see cref="DeterministicMathBridge"/> 的命名空间便捷转发（算法唯一实现于 Core）。
    /// 规则：同一份 float 位模式换算结果位一致，因此只在配置加载、表现事件、IO 出口
    /// 做单次换算；模拟内部一律 Fixed64。ToFixed 对 NaN/Inf 归零（对齐旧 float 管线防护行为）。
    /// </summary>
    internal static class MobaResourceFixedConvert
    {
        internal static Fixed64 ToFixed(float value) => DeterministicMathBridge.ToFixed(value);

        internal static float ToSingle(Fixed64 value) => DeterministicMathBridge.ToSingle(value);
    }
}
