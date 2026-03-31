namespace AbilityKit.Ability.Impl.Moba.Effects.Model
{
    /// <summary>
    /// Moba游戏效果属性键枚举
    /// </summary>
    internal enum MobaEffectStatKey : int
    {
        // 发射器相关属性
        /// <summary>发射间隔基础附加帧数</summary>
        LauncherIntervalBaseAddFrames = 0,

        /// <summary>发射间隔后置乘数</summary>
        LauncherIntervalPostMul = 1,

        /// <summary>每次射击额外发射数量</summary>
        LauncherExtraProjectilesPerShot = 2,

        /// <summary>额外总发射次数</summary>
        LauncherExtraTotalCount = 3,

        // 弹丸相关属性
        /// <summary>伤害乘数</summary>
        ProjectileDamageMul = 10,

        /// <summary>速度乘数</summary>
        ProjectileSpeedMul = 11,

        /// <summary>穿透数量</summary>
        ProjectilePierce = 12,
    }

    /// <summary>
    /// MobaEffectStatKey 的扩展方法
    /// </summary>
    internal static class MobaEffectStatKeyExtensions
    {
        /// <summary>
        /// 是否为发射器属性
        /// </summary>
        public static bool IsLauncherStat(this MobaEffectStatKey key) => key < MobaEffectStatKey.ProjectileDamageMul;

        /// <summary>
        /// 是否为弹丸属性
        /// </summary>
        public static bool IsProjectileStat(this MobaEffectStatKey key) => key >= MobaEffectStatKey.ProjectileDamageMul;

        /// <summary>
        /// 是否为乘数属性
        /// </summary>
        public static bool IsMultiplier(this MobaEffectStatKey key) =>
            key is MobaEffectStatKey.LauncherIntervalPostMul
                or MobaEffectStatKey.ProjectileDamageMul
                or MobaEffectStatKey.ProjectileSpeedMul;

        /// <summary>
        /// 是否为加法属性
        /// </summary>
        public static bool IsAdditive(this MobaEffectStatKey key) =>
            key is MobaEffectStatKey.LauncherIntervalBaseAddFrames
                or MobaEffectStatKey.LauncherExtraProjectilesPerShot
                or MobaEffectStatKey.LauncherExtraTotalCount
                or MobaEffectStatKey.ProjectilePierce;
    }
}
