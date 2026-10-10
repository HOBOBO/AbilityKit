using System;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Deterministic;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// 修改器 Sim 数值出口的精度策略。进入确定性模拟域的参数（子弹速度、伤害基础值等）
    /// 在解析出口经此策略处理；表现/视图数值不经策略，始终直接返回 float。
    /// 修改器组合运算本身只含 IEEE 位一致的加减乘除与比较，策略不参与组合过程；
    /// 需要定点组合内核时应使用框架的 IComposerStrategy 替换缝，而非本策略。
    /// </summary>
    public interface IMobaModifierPrecisionPolicy
    {
        /// <summary>
        /// Sim 出口的 float 视图。直通实现原样返回（零换算）；
        /// 定点实现对齐 Q32.32 权威网格后回出，|v| ≥ 2^31 时 fail-loud。
        /// </summary>
        float QuantizeSimFloat(float value);
    }

    /// <summary>
    /// 直通精度策略（空实现）：未注册策略的世界与非帧同步复用场景的默认值。
    /// Sim 出口与普通 float 出口位等价，不引入任何换算成本。
    /// </summary>
    public sealed class PassThroughModifierPrecisionPolicy : IMobaModifierPrecisionPolicy
    {
        public static readonly PassThroughModifierPrecisionPolicy Instance = new PassThroughModifierPrecisionPolicy();

        private PassThroughModifierPrecisionPolicy() { }

        public float QuantizeSimFloat(float value) => value;
    }

    /// <summary>
    /// 帧同步定点精度策略：Sim 出口经 Fixed64 单次精确换算对齐 Q32.32 权威网格。
    /// 常用数值域（2^-32 至 2^31）内换算与原值位等价；小于 2^-32 的退化值截断到零；
    /// |v| ≥ 2^31 溢出按 checked 语义 fail-loud，保证写入模拟域的数值跨端一致。
    /// </summary>
    [WorldService(typeof(IMobaModifierPrecisionPolicy))]
    public sealed class Fixed64ModifierPrecisionPolicy : IMobaModifierPrecisionPolicy, IService
    {
        public float QuantizeSimFloat(float value) => Fixed64.FromSingle(value).ToSingle();

        public void Dispose() { }
    }
}
