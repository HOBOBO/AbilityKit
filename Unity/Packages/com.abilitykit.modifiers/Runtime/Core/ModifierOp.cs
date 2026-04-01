namespace AbilityKit.Modifiers
{
    /// <summary>
    /// 修改器操作类型。
    /// 决定如何将修改器的值应用到基础值上。
    ///
    /// 内置操作（0-99）：
    /// - 0-9: 基础算术操作
    /// - 10-19: 百分比/比例操作
    /// - 20-29: 覆盖/替换操作
    /// - 100+: 业务层自定义操作
    /// </summary>
    public enum ModifierOp : byte
    {
        /// <summary>加法：Base + Value</summary>
        Add = 0,

        /// <summary>乘法：Base × Value</summary>
        Mul = 1,

        /// <summary>覆盖：直接替换为 Value</summary>
        Override = 2,

        /// <summary>百分比加成：Base × (1 + Value)。Value=0.2 表示 +20%</summary>
        PercentAdd = 3,

        /// <summary>自定义操作开始标识（业务层可扩展）</summary>
        Custom = 100,
    }

    /// <summary>
    /// ModifierOp 扩展方法
    /// </summary>
    public static class ModifierOpExtensions
    {
        /// <summary>是否为加法类操作</summary>
        public static bool IsAdditive(this ModifierOp op)
            => op == ModifierOp.Add || op == ModifierOp.PercentAdd;

        /// <summary>是否为乘法类操作</summary>
        public static bool IsMultiplicative(this ModifierOp op)
            => op == ModifierOp.Mul;

        /// <summary>是否为覆盖操作</summary>
        public static bool IsOverride(this ModifierOp op)
            => op == ModifierOp.Override;

        /// <summary>是否为内置操作</summary>
        public static bool IsBuiltin(this ModifierOp op)
            => op < ModifierOp.Custom;

        /// <summary>获取操作符号</summary>
        public static string GetSymbol(this ModifierOp op) => op switch
        {
            ModifierOp.Add => "+",
            ModifierOp.Mul => "×",
            ModifierOp.Override => "=",
            ModifierOp.PercentAdd => "+%",
            _ => "?"
        };
    }
}
