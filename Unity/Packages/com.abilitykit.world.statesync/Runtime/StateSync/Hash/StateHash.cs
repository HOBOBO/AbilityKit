namespace AbilityKit.Ability.StateSync
{
    /// <summary>
    /// 状态哈希的无符号 64 位值包装；零为无效哨兵，但数值相等不检查有效性。
    /// </summary>
    public readonly struct StateHash : System.IEquatable<StateHash>
    {
        /// <summary>
        /// 构造时保存的原始数值。
        /// </summary>
        public ulong Value { get; }
        /// <summary>
        /// 仅标识数值是否非零，不证明状态覆盖范围或可信度。
        /// </summary>
        public bool IsValid => Value != 0;

        /// <summary>
        /// 包装给定值；零值可表示缺失，不抛异常。
        /// </summary>
        public StateHash(ulong value)
        {
            Value = value;
        }

        /// <summary>
        /// 数值为零的无效哨兵，与默认值相等。
        /// </summary>
        public static readonly StateHash Invalid = new StateHash(0);

        /// <summary>
        /// 比较数值，两个无效值也相等。
        /// </summary>
        public bool Equals(StateHash other) => Value == other.Value;
        /// <summary>
        /// 仅当对象为同类型且数值相同才相等。
        /// </summary>
        public override bool Equals(object obj) => obj is StateHash other && Equals(other);
        /// <summary>
        /// 返回原始数值的集合哈希，用于查表，不作为新的状态哈希。
        /// </summary>
        public override int GetHashCode() => Value.GetHashCode();
        /// <summary>
        /// 返回 16 位大写十六进制字符串，供诊断使用。
        /// </summary>
        public override string ToString() => Value.ToString("X16");

        /// <summary>
        /// 比较数值是否相同，不检查有效性。
        /// </summary>
        public static bool operator ==(StateHash left, StateHash right) => left.Value == right.Value;
        /// <summary>
        /// 比较数值是否不同，不检查有效性。
        /// </summary>
        public static bool operator !=(StateHash left, StateHash right) => left.Value != right.Value;
    }
}
