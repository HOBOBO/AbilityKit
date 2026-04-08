using System;

namespace AbilityKit.Modifiers
{
    // ============================================================================
    // 修改器键
    // ============================================================================

    /// <summary>
    /// 修改器键。
    /// 用于标识修改器的作用目标（属性、技能参数、状态等）。
    ///
    /// 32 位压缩存储：
    /// [Reserved:8][Custom:8][SubCategory:8][Category:8]
    ///
    /// 使用示例：
    /// ```csharp
    /// // 预定义键
    /// ModifierKey.AttackPower
    /// ModifierKey.MaxHealth
    ///
    /// // 自定义键
    /// var key = ModifierKey.Create(categoryId: 10, subCategoryId: 2, customId: 0);
    ///
    /// // 业务层扩展分类
    /// ModifierKey.Categories.Projectile = 30;
    /// ```
    /// </summary>
    public struct ModifierKey : IEquatable<ModifierKey>
    {
        /// <summary>压缩后的键值</summary>
        public uint Packed;

        /// <summary>
        /// 创建自定义键
        /// </summary>
        /// <param name="categoryId">分类 ID (0-255)</param>
        /// <param name="subCategoryId">子分类 ID (0-255)</param>
        /// <param name="customId">自定义 ID (0-255)</param>
        public static ModifierKey Create(byte categoryId = 0, byte subCategoryId = 0, byte customId = 0)
        {
            return new ModifierKey
            {
                Packed = (uint)((categoryId << 16) | (subCategoryId << 8) | customId)
            };
        }

        /// <summary>
        /// 从压缩值创建
        /// </summary>
        public static ModifierKey FromPacked(uint packed) => new ModifierKey { Packed = packed };

        /// <summary>
        /// 无效键
        /// </summary>
        public static ModifierKey None => default;

        /// <summary>是否为有效键</summary>
        public bool IsValid => Packed != 0;

        /// <summary>是否为空键</summary>
        public bool IsEmpty => Packed == 0;

        #region 预定义键

        // 属性相关
        public static ModifierKey AttackPower => Create(1, 0);
        public static ModifierKey MaxHealth => Create(1, 1);
        public static ModifierKey MoveSpeed => Create(1, 2);
        public static ModifierKey Defense => Create(1, 3);

        // 护盾相关
        public static ModifierKey ShieldMax => Create(2, 0);
        public static ModifierKey ShieldRegen => Create(2, 1);

        // 伤害相关
        public static ModifierKey DamageBonus => Create(3, 0);
        public static ModifierKey CriticalRate => Create(3, 1);
        public static ModifierKey CriticalDamage => Create(3, 2);

        // 状态相关
        public static ModifierKey StateInvincible => Create(100, 0);
        public static ModifierKey StateSilence => Create(100, 1);
        public static ModifierKey StateImmune => Create(100, 2);

        #endregion

        #region 分类访问器

        public byte Category => (byte)(Packed >> 16);
        public byte SubCategory => (byte)(Packed >> 8);
        public byte CustomId => (byte)Packed;

        #endregion

        #region 分类扩展

        /// <summary>
        /// 分类扩展点
        /// </summary>
        public static class Categories
        {
            public const byte Attribute = 1;
            public const byte Shield = 2;
            public const byte Damage = 3;
            public const byte State = 100;
            public const byte Projectile = 30;
            public const byte AOE = 31;
            public const byte Skill = 40;
            public const byte Custom = 200;
        }

        #endregion

        #region IEquatable

        public bool Equals(ModifierKey other) => Packed == other.Packed;
        public override bool Equals(object obj) => obj is ModifierKey other && Equals(other);
        public override int GetHashCode() => (int)Packed;
        public static bool operator ==(ModifierKey a, ModifierKey b) => a.Packed == b.Packed;
        public static bool operator !=(ModifierKey a, ModifierKey b) => a.Packed != b.Packed;
        public override string ToString() => $"ModifierKey({Category}.{SubCategory}.{CustomId})";

        #endregion
    }
}
