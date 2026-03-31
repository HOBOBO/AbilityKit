namespace AbilityKit.Effects.Core.Model
{
    public readonly struct EffectValue
    {
        public readonly int I;
        public readonly float F;

        /// <summary>
        /// 值类型枚举，用于标识当前存储的是整数还是浮点数
        /// </summary>
        public readonly EffectValueKind Kind;

        /// <summary>
        /// 空值（未初始化状态）
        /// </summary>
        public static EffectValue None => default;

        /// <summary>
        /// 整数零值
        /// </summary>
        public static EffectValue ZeroInt => new EffectValue(0);

        /// <summary>
        /// 浮点数零值
        /// </summary>
        public static EffectValue ZeroFloat => new EffectValue(0f);

        public EffectValue(int i)
        {
            I = i;
            F = 0f;
            Kind = EffectValueKind.Int;
        }

        public EffectValue(float f)
        {
            I = 0;
            F = f;
            Kind = EffectValueKind.Float;
        }

        private EffectValue(int i, float f, EffectValueKind kind)
        {
            I = i;
            F = f;
            Kind = kind;
        }

        /// <summary>
        /// 从整数创建 EffectValue
        /// </summary>
        public static EffectValue FromInt(int value) => new EffectValue(value);

        /// <summary>
        /// 从浮点数创建 EffectValue
        /// </summary>
        public static EffectValue FromFloat(float value) => new EffectValue(value);

        /// <summary>
        /// 获取整数值（如果不是整数类型则返回默认值）
        /// </summary>
        public int AsInt(int defaultValue = 0) => Kind == EffectValueKind.Int ? I : defaultValue;

        /// <summary>
        /// 获取浮点数值（如果不是浮点类型则返回默认值）
        /// </summary>
        public float AsFloat(float defaultValue = 0f) => Kind == EffectValueKind.Float ? F : defaultValue;

        /// <summary>
        /// 获取整数值，强制转换浮点数（如果需要）
        /// </summary>
        public int AsIntForce()
        {
            return Kind switch
            {
                EffectValueKind.Int => I,
                EffectValueKind.Float => (int)F,
                _ => 0
            };
        }

        /// <summary>
        /// 获取浮点数值，强制转换整数（如果需要）
        /// </summary>
        public float AsFloatForce()
        {
            return Kind switch
            {
                EffectValueKind.Int => I,
                EffectValueKind.Float => F,
                _ => 0f
            };
        }

        public bool IsInteger => Kind == EffectValueKind.Int;
        public bool IsFloat => Kind == EffectValueKind.Float;

        public override string ToString() => Kind == EffectValueKind.Int ? $"Int({I})" : $"Float({F})";
    }

    /// <summary>
    /// EffectValue 的值类型
    /// </summary>
    public enum EffectValueKind : byte
    {
        /// <summary>
        /// 未初始化或无效值
        /// </summary>
        None = 0,

        /// <summary>
        /// 整数值
        /// </summary>
        Int = 1,

        /// <summary>
        /// 浮点数值
        /// </summary>
        Float = 2,
    }
}
