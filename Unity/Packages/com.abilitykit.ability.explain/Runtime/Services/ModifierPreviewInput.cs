using System;

namespace AbilityKit.Ability.Explain
{
    /// <summary>
    /// 强类型修饰预览输入（"加成效果"通道）。业务层把用户选择的修饰写入
    /// <see cref="ExplainResolveContext.Modifiers"/>，resolver 用真实计算（如 ModifierCalculator）
    /// 算出基础值 → 终值，在解释森林中展示"加成之后"的效果。
    /// 说明：explain 包保持中性，不依赖任何具体修饰器/属性实现；Op 为约定字符串
    /// （add / mul / percent_add / override），由业务 resolver 自行映射到真实计算。
    /// </summary>
    [Serializable]
    public sealed class ModifierPreviewInput
    {
        /// <summary>操作类型：add / mul / percent_add / override。</summary>
        public string Op;

        /// <summary>修饰数值（对 mul 是倍率，对 percent_add 是百分比增量，如 0.2 = +20%）。</summary>
        public float Value;

        /// <summary>展示标签，如 "冷却 -20%"。可选。</summary>
        public string Label;
    }
}
