using System;

namespace AbilityKit.Effects.Core.Snapshots
{
    /// <summary>
    /// 发射器效果快照
    /// 字段按访问频率和类型分组排列以优化缓存
    /// </summary>
    [Serializable]
    public struct LauncherEffectSnapshot
    {
        // 按类型分组：整数字段（高频率访问）
        public int IntervalBaseAddFrames;
        public int ExtraProjectilesPerShot;
        public int ExtraTotalCount;

        // 浮点字段
        public float IntervalPostMul;

        /// <summary>
        /// 默认快照（乘数为1，加数为0）
        /// </summary>
        public static LauncherEffectSnapshot Default => new()
        {
            IntervalBaseAddFrames = 0,
            ExtraProjectilesPerShot = 0,
            ExtraTotalCount = 0,
            IntervalPostMul = 1f,
        };

        /// <summary>
        /// 重置为默认值
        /// </summary>
        public void Reset()
        {
            this = Default;
        }

        /// <summary>
        /// 计算实际发射间隔（基础帧数 + 附加帧数）* 乘数
        /// </summary>
        public float CalculateActualInterval(float baseIntervalFrames)
        {
            return (baseIntervalFrames + IntervalBaseAddFrames) * IntervalPostMul;
        }

        /// <summary>
        /// 计算实际每发射击数量（基础数量 + 额外数量）
        /// </summary>
        public int CalculateActualProjectilesPerShot(int baseCount)
        {
            return baseCount + ExtraProjectilesPerShot;
        }

        /// <summary>
        /// 计算实际总发射次数（基础次数 + 额外次数）
        /// </summary>
        public int CalculateActualTotalCount(int baseCount)
        {
            return baseCount + ExtraTotalCount;
        }

        /// <summary>
        /// 合并另一个快照（加法操作）
        /// </summary>
        public void MergeAdd(in LauncherEffectSnapshot other)
        {
            IntervalBaseAddFrames += other.IntervalBaseAddFrames;
            ExtraProjectilesPerShot += other.ExtraProjectilesPerShot;
            ExtraTotalCount += other.ExtraTotalCount;
        }

        /// <summary>
        /// 合并另一个快照（乘法操作）
        /// </summary>
        public void MergeMul(in LauncherEffectSnapshot other)
        {
            IntervalPostMul *= other.IntervalPostMul;
        }

        /// <summary>
        /// 创建合并后的新快照
        /// </summary>
        public static LauncherEffectSnapshot Merge(in LauncherEffectSnapshot a, in LauncherEffectSnapshot b)
        {
            return new LauncherEffectSnapshot
            {
                IntervalBaseAddFrames = a.IntervalBaseAddFrames + b.IntervalBaseAddFrames,
                ExtraProjectilesPerShot = a.ExtraProjectilesPerShot + b.ExtraProjectilesPerShot,
                ExtraTotalCount = a.ExtraTotalCount + b.ExtraTotalCount,
                IntervalPostMul = a.IntervalPostMul * b.IntervalPostMul,
            };
        }

        /// <summary>
        /// 是否为默认值（无任何效果修改）
        /// </summary>
        public bool IsDefault =>
            IntervalBaseAddFrames == 0 &&
            ExtraProjectilesPerShot == 0 &&
            ExtraTotalCount == 0 &&
            Math.Abs(IntervalPostMul - 1f) < 0.0001f;

        public override string ToString() =>
            $"Launcher[IntervalAdd={IntervalBaseAddFrames}, IntervalMul={IntervalPostMul:F2}, ExtraPerShot={ExtraProjectilesPerShot}, ExtraTotal={ExtraTotalCount}]";
    }
}
