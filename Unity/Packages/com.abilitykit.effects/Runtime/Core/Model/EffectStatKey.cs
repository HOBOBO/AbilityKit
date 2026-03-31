namespace AbilityKit.Effects.Core.Model
{
    /// <summary>
    /// 效果属性键枚举
    /// 注意：这是核心层的基础枚举，游戏特定实现应继承或扩展此枚举
    /// </summary>
    public enum EffectStatKey : byte
    {
        // ============ 发射器 (Launcher) 相关属性 ============

        /// <summary>发射间隔基础附加帧数（加法）</summary>
        LauncherIntervalBaseAddFrames = 0,

        /// <summary>发射间隔后置乘数（乘法）</summary>
        LauncherIntervalPostMul = 1,

        /// <summary>每次射击额外发射数量（加法）</summary>
        LauncherExtraProjectilesPerShot = 2,

        /// <summary>额外总发射次数（加法）</summary>
        LauncherExtraTotalCount = 3,

        // ============ 弹丸 (Projectile) 相关属性 ============

        /// <summary>伤害乘数（乘法）</summary>
        ProjectileDamageMul = 10,

        /// <summary>速度乘数（乘法）</summary>
        ProjectileSpeedMul = 11,

        /// <summary>穿透数量（加法）</summary>
        ProjectilePierce = 12,
    }

    /// <summary>
    /// EffectStatKey 枚举的扩展方法
    /// </summary>
    public static class EffectStatKeyExtensions
    {
        /// <summary>
        /// 是否为发射器相关属性
        /// </summary>
        public static bool IsLauncherStat(this EffectStatKey key) => key < EffectStatKey.ProjectileDamageMul;

        /// <summary>
        /// 是否为弹丸相关属性
        /// </summary>
        public static bool IsProjectileStat(this EffectStatKey key) => key >= EffectStatKey.ProjectileDamageMul;

        /// <summary>
        /// 是否为乘法操作属性
        /// </summary>
        public static bool IsMultiplier(this EffectStatKey key) =>
            key is EffectStatKey.LauncherIntervalPostMul
                or EffectStatKey.ProjectileDamageMul
                or EffectStatKey.ProjectileSpeedMul;

        /// <summary>
        /// 是否为加法操作属性
        /// </summary>
        public static bool IsAdditive(this EffectStatKey key) =>
            key is EffectStatKey.LauncherIntervalBaseAddFrames
                or EffectStatKey.LauncherExtraProjectilesPerShot
                or EffectStatKey.LauncherExtraTotalCount
                or EffectStatKey.ProjectilePierce;
    }
}
