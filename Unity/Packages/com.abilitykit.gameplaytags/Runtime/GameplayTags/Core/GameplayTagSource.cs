namespace AbilityKit.GameplayTags
{
    /// <summary>
    /// 标签来源标识，用于追踪标签的施加者。
    /// </summary>
    public readonly struct GameplayTagSource
    {
        /// <summary>
        /// 来源值（通常为实体 ID 或效果 ID）
        /// </summary>
        public readonly long Value;

        public GameplayTagSource(long value)
        {
            Value = value;
        }

        /// <summary>
        /// 是否有效
        /// </summary>
        public bool IsValid => Value != 0;

        /// <summary>
        /// 无效来源
        /// </summary>
        public static GameplayTagSource None => default;

        /// <summary>
        /// 系统来源（由系统自动添加/移除的标签）
        /// </summary>
        public static GameplayTagSource System => new GameplayTagSource(-1);

        public override string ToString() => Value.ToString();

        public static bool operator ==(GameplayTagSource a, GameplayTagSource b) => a.Value == b.Value;
        public static bool operator !=(GameplayTagSource a, GameplayTagSource b) => a.Value != b.Value;
        public override bool Equals(object obj) => obj is GameplayTagSource other && Value == other.Value;
        public override int GetHashCode() => Value.GetHashCode();
    }
}
