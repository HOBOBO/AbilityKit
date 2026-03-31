namespace AbilityKit.Effects.Core.Model
{
    /// <summary>
    /// 效果操作类型
    /// </summary>
    public enum EffectOp : byte
    {
        /// <summary>加法操作（叠加数值）</summary>
        Add = 0,

        /// <summary>乘法操作（乘以系数）</summary>
        Mul = 1,
    }

    /// <summary>
    /// EffectOp 的扩展方法
    /// </summary>
    public static class EffectOpExtensions
    {
        /// <summary>
        /// 获取操作的中文名称
        /// </summary>
        public static string GetChineseName(this EffectOp op) =>
            op switch
            {
                EffectOp.Add => "加法",
                EffectOp.Mul => "乘法",
                _ => "未知"
            };

        /// <summary>
        /// 获取操作的符号表示
        /// </summary>
        public static string GetSymbol(this EffectOp op) =>
            op switch
            {
                EffectOp.Add => "+",
                EffectOp.Mul => "*",
                _ => "?"
            };
    }
}
