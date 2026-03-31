namespace AbilityKit.Effects.Core.Model
{
    public sealed class EffectStatItem
    {
        public readonly int KeyId;
        public readonly EffectOp Op;
        public readonly EffectValue Value;

        public EffectStatItem(int keyId, EffectOp op, in EffectValue value)
        {
            KeyId = keyId;
            Op = op;
            Value = value;
        }

        /// <summary>
        /// 创建加法操作的整数属性项
        /// </summary>
        public static EffectStatItem CreateAddInt(int keyId, int value) =>
            new EffectStatItem(keyId, EffectOp.Add, new EffectValue(value));

        /// <summary>
        /// 创建乘法操作的浮点属性项
        /// </summary>
        public static EffectStatItem CreateMulFloat(int keyId, float value) =>
            new EffectStatItem(keyId, EffectOp.Mul, new EffectValue(value));

        /// <summary>
        /// 是否为加法操作
        /// </summary>
        public bool IsAdd => Op == EffectOp.Add;

        /// <summary>
        /// 是否为乘法操作
        /// </summary>
        public bool IsMul => Op == EffectOp.Mul;

        /// <summary>
        /// 是否为整数类型值
        /// </summary>
        public bool IsIntegerValue => Value.Kind == EffectValueKind.Int;

        /// <summary>
        /// 是否为浮点类型值
        /// </summary>
        public bool IsFloatValue => Value.Kind == EffectValueKind.Float;

        /// <summary>
        /// 获取整数形式的值（如果不是整数则返回默认值）
        /// </summary>
        public int GetIntValue(int defaultValue = 0) => Value.AsInt(defaultValue);

        /// <summary>
        /// 获取浮点形式的值（如果不是浮点则返回默认值）
        /// </summary>
        public float GetFloatValue(float defaultValue = 0f) => Value.AsFloat(defaultValue);

        /// <summary>
        /// 将当前属性项应用到累加器（用于加法操作）
        /// </summary>
        public void ApplyToAccumulator(ref int intAccumulator, ref float floatAccumulator)
        {
            if (Op == EffectOp.Add)
            {
                if (IsIntegerValue)
                    intAccumulator += Value.I;
                else
                    floatAccumulator += Value.F;
            }
        }

        /// <summary>
        /// 将当前属性项应用到乘数（用于乘法操作）
        /// </summary>
        public void ApplyToMultiplier(ref float intMultiplier, ref float floatMultiplier)
        {
            if (Op == EffectOp.Mul)
            {
                if (IsIntegerValue)
                    intMultiplier *= Value.I;
                else
                    floatMultiplier *= Value.F;
            }
        }

        public override string ToString() => $"StatItem(Key={KeyId}, Op={Op}, Value={Value})";
    }
}
