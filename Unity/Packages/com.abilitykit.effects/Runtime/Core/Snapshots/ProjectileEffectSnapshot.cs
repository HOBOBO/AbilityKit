using System;

namespace AbilityKit.Effects.Core.Snapshots
{
    /// <summary>
    /// 弹丸效果快照
    /// 字段按访问频率和类型分组排列以优化缓存
    /// </summary>
    [Serializable]
    public struct ProjectileEffectSnapshot
    {
        // 乘数字段（高频率访问）
        public float DamageMul;
        public float SpeedMul;

        // 整数字段
        public int Pierce;

        /// <summary>
        /// 默认快照（乘数为1，穿透为0）
        /// </summary>
        public static ProjectileEffectSnapshot Default => new()
        {
            DamageMul = 1f,
            SpeedMul = 1f,
            Pierce = 0,
        };

        /// <summary>
        /// 重置为默认值
        /// </summary>
        public void Reset()
        {
            this = Default;
        }

        /// <summary>
        /// 计算实际伤害
        /// </summary>
        public float CalculateActualDamage(float baseDamage)
        {
            return baseDamage * DamageMul;
        }

        /// <summary>
        /// 计算实际速度
        /// </summary>
        public float CalculateActualSpeed(float baseSpeed)
        {
            return baseSpeed * SpeedMul;
        }

        /// <summary>
        /// 合并另一个快照（乘法操作）
        /// </summary>
        public void MergeMul(in ProjectileEffectSnapshot other)
        {
            DamageMul *= other.DamageMul;
            SpeedMul *= other.SpeedMul;
        }

        /// <summary>
        /// 合并另一个快照（加法操作）
        /// </summary>
        public void MergeAdd(in ProjectileEffectSnapshot other)
        {
            Pierce += other.Pierce;
        }

        /// <summary>
        /// 创建合并后的新快照
        /// </summary>
        public static ProjectileEffectSnapshot Merge(in ProjectileEffectSnapshot a, in ProjectileEffectSnapshot b)
        {
            return new ProjectileEffectSnapshot
            {
                DamageMul = a.DamageMul * b.DamageMul,
                SpeedMul = a.SpeedMul * b.SpeedMul,
                Pierce = a.Pierce + b.Pierce,
            };
        }

        /// <summary>
        /// 是否为默认值（无任何效果修改）
        /// </summary>
        public bool IsDefault =>
            Math.Abs(DamageMul - 1f) < 0.0001f &&
            Math.Abs(SpeedMul - 1f) < 0.0001f &&
            Pierce == 0;

        /// <summary>
        /// 是否有任何非1的乘数效果
        /// </summary>
        public bool HasMultiplierEffect =>
            Math.Abs(DamageMul - 1f) >= 0.0001f ||
            Math.Abs(SpeedMul - 1f) >= 0.0001f;

        /// <summary>
        /// 是否有穿透效果
        /// </summary>
        public bool HasPierceEffect => Pierce > 0;

        public override string ToString() =>
            $"Projectile[DamageMul={DamageMul:F2}, SpeedMul={SpeedMul:F2}, Pierce={Pierce}]";
    }
}
