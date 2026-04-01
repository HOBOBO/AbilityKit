using System;

namespace AbilityKit.Modifiers
{
    /// <summary>
    /// 修改器键。
    /// 用于唯一标识一个可修改的目标（属性、技能参数、弹道配置等）。
    ///
    /// 设计：32位压缩存储 = [Reserved:8][Custom:8][SubCategory:8][Category:8]
    ///
    /// 对标 GAS 的 GameplayTag。
    /// </summary>
    public readonly struct ModifierKey : IEquatable<ModifierKey>
    {
        /// <summary>完整的打包值</summary>
        public readonly uint Packed;

        #region 工厂方法

        /// <summary>空键</summary>
        public static readonly ModifierKey None = new(0);

        /// <summary>创建键</summary>
        public static ModifierKey Create(int category, int subCategory = 0, int custom = 0)
            => new(((uint)category & 0xFF) | (((uint)subCategory & 0xFF) << 8) | (((uint)custom & 0xFF) << 16));

        /// <summary>从打包值创建</summary>
        public static ModifierKey FromPacked(uint packed) => new(packed);

        #endregion

        #region 属性

        private ModifierKey(uint packed) => Packed = packed;

        /// <summary>分类 ID（8bit）</summary>
        public int Category => (int)(Packed & 0xFF);

        /// <summary>子分类 ID（8bit）</summary>
        public int SubCategory => (int)((Packed >> 8) & 0xFF);

        /// <summary>自定义 ID（8bit）</summary>
        public int Custom => (int)((Packed >> 16) & 0xFF);

        /// <summary>是否为空</summary>
        public bool IsEmpty => Packed == 0;

        /// <summary>匹配分类</summary>
        public bool MatchesCategory(int category) => (Packed & 0xFF) == (category & 0xFF);

        #endregion

        #region 预定义分类（业务层可自行扩展）

        /// <summary>
        /// 预定义分类常量。
        /// 业务层可按需使用或自行定义。
        /// </summary>
        public static class Categories
        {
            // 属性类
            public const int Health = 1;
            public const int Shield = 2;
            public const int Mana = 3;
            public const int Speed = 4;

            // 战斗类
            public const int Damage = 10;
            public const int Defense = 11;
            public const int AttackSpeed = 12;
            public const int Cooldown = 13;
            public const int Range = 14;

            // 效果类
            public const int DOT = 20;
            public const int HOT = 21;
            public const int Lifesteal = 22;

            // 弹道类
            public const int Projectile = 30;
            public const int AOE = 31;

            // 技能类
            public const int Skill = 40;
            public const int Cost = 41;
        }

        /// <summary>
        /// 预定义子分类
        /// </summary>
        public static class SubCategories
        {
            public const int None = 0;
            public const int Max = 1;
            public const int Current = 2;
            public const int Regen = 3;
            public const int Incoming = 4;
            public const int Outgoing = 5;
            public const int Mul = 6;
        }

        #endregion

        #region 便捷工厂

        /// <summary>护盾最大值</summary>
        public static ModifierKey ShieldMax => Create(Categories.Shield, SubCategories.Max);

        /// <summary>移动速度</summary>
        public static ModifierKey MoveSpeed => Create(Categories.Speed, SubCategories.Max);

        /// <summary>护盾回复速率</summary>
        public static ModifierKey ShieldRegen => Create(Categories.Shield, SubCategories.Regen);

        /// <summary>DOT 伤害</summary>
        public static ModifierKey DOTDamage => Create(Categories.DOT, SubCategories.Outgoing);

        /// <summary>HOT 治疗</summary>
        public static ModifierKey HOTHeal => Create(Categories.HOT, SubCategories.Outgoing);

        /// <summary>受到的治疗加成</summary>
        public static ModifierKey HealTaken => Create(Categories.HOT, SubCategories.Incoming);

        #endregion

        #region Object

        public static bool operator ==(ModifierKey a, ModifierKey b) => a.Packed == b.Packed;
        public static bool operator !=(ModifierKey a, ModifierKey b) => a.Packed != b.Packed;
        public bool Equals(ModifierKey other) => Packed == other.Packed;
        public override bool Equals(object obj) => obj is ModifierKey other && Equals(other);
        public override int GetHashCode() => (int)Packed;

        public override string ToString()
        {
            if (IsEmpty) return "None";
            if (Custom != 0) return $"[{Category}:{SubCategory}:{Custom}]";
            if (SubCategory != 0) return $"[{Category}:{SubCategory}]";
            return $"[{Category}]";
        }

        #endregion
    }
}
