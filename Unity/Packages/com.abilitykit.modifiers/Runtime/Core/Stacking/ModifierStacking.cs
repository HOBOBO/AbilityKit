using System;

namespace AbilityKit.Modifiers
{
    /// <summary>
    /// 叠加类型。
    /// </summary>
    public enum StackingType : byte
    {
        /// <summary>
        /// 独占：同一来源只能有一个实例在该堆叠组中。
        /// 新来的相同源会替换旧的。
        /// </summary>
        Exclusive = 0,

        /// <summary>
        /// 聚合：同一来源可以有多个实例在该堆叠组中。
        /// 新来的相同源会叠加层数。
        /// </summary>
        Aggregate = 1,
    }

    /// <summary>
    /// 叠加组。
    /// 用于管理同类修改器的叠加逻辑。
    ///
    /// 对标 GAS 的 FGameplayEffectStackingModule。
    ///
    /// 设计原则：
    /// - 独立于 ModifierData，叠加逻辑由业务层决定
    /// - 这里只提供计算逻辑：如何合并多个叠加条目
    /// - 存储由业务层负责（可以用 List、Dictionary 或 ECS Component）
    /// </summary>
    public struct ModifierStacking
    {
        /// <summary>
        /// 叠加组配置
        /// </summary>
        public StackingConfig Config;

        /// <summary>
        /// 当前叠加层数
        /// </summary>
        public int StackCount;

        /// <summary>
        /// 单个叠加条目的数据
        /// </summary>
        public ModifierData Entry;

        #region 工厂方法

        /// <summary>
        /// 创建聚合叠加组
        /// </summary>
        public static ModifierStacking CreateAggregate(
            ModifierKey stackKey,
            int maxStack = 1,
            int initialCount = 1,
            ModifierData? entry = null)
        {
            return new ModifierStacking
            {
                Config = new StackingConfig
                {
                    Type = StackingType.Aggregate,
                    StackKey = stackKey,
                    MaxStackCount = maxStack
                },
                StackCount = initialCount,
                Entry = entry ?? default
            };
        }

        /// <summary>
        /// 创建独占叠加组
        /// </summary>
        public static ModifierStacking CreateExclusive(
            ModifierKey stackKey,
            ModifierData? entry = null)
        {
            return new ModifierStacking
            {
                Config = new StackingConfig
                {
                    Type = StackingType.Exclusive,
                    StackKey = stackKey,
                    MaxStackCount = 1
                },
                StackCount = 1,
                Entry = entry ?? default
            };
        }

        #endregion

        #region 叠加操作

        /// <summary>
        /// 尝试添加一层叠加
        /// </summary>
        /// <param name="newEntry">新的叠加条目</param>
        /// <returns>是否成功添加</returns>
        public bool TryPush(in ModifierData newEntry)
        {
            if (StackCount >= Config.MaxStackCount)
                return false;

            if (Config.Type == StackingType.Exclusive)
            {
                // 独占模式：直接替换
                Entry = newEntry;
                StackCount = 1;
                return true;
            }
            else
            {
                // 聚合模式：叠加层数
                StackCount++;
                return true;
            }
        }

        /// <summary>
        /// 移除一层叠加
        /// </summary>
        /// <returns>是否还有剩余</returns>
        public bool TryPop()
        {
            if (StackCount <= 0)
                return false;

            StackCount--;

            if (Config.Type == StackingType.Exclusive)
                Entry = default;

            return StackCount > 0;
        }

        /// <summary>
        /// 清空所有叠加
        /// </summary>
        public void Clear()
        {
            StackCount = 0;
            Entry = default;
        }

        #endregion

        #region 计算

        /// <summary>
        /// 展开为修改器列表（用于 ModifierCalculator 计算）
        /// </summary>
        /// <param name="results">预分配的数组</param>
        /// <returns>实际使用的条目数</returns>
        public int ExpandTo(Span<ModifierData> results)
        {
            if (StackCount == 0)
                return 0;

            if (Config.Type == StackingType.Exclusive)
            {
                results[0] = Entry;
                return 1;
            }

            // Aggregate: 重复 StackCount 次
            for (int i = 0; i < StackCount && i < results.Length; i++)
            {
                results[i] = Entry;
            }

            return StackCount;
        }

        /// <summary>
        /// 快速计算叠加后的数值（不分配）
        /// </summary>
        public float CalculateStackedValue(float baseValue)
        {
            if (StackCount == 0 || Entry.Op == ModifierOp.Override)
                return StackCount > 0 ? Entry.GetMagnitude() : baseValue;

            // 叠加计算：层数 × 单层值
            float stackedValue = Entry.GetMagnitude() * StackCount;

            return Entry.Op switch
            {
                ModifierOp.Add => baseValue + stackedValue,
                ModifierOp.Mul => baseValue * stackedValue,
                ModifierOp.PercentAdd => baseValue * (1f + stackedValue),
                _ => baseValue
            };
        }

        #endregion
    }

    /// <summary>
    /// 叠加配置
    /// </summary>
    [Serializable]
    public struct StackingConfig
    {
        /// <summary>叠加类型</summary>
        public StackingType Type;

        /// <summary>叠加标识键（用于区分不同的叠加组）</summary>
        public ModifierKey StackKey;

        /// <summary>最大叠加层数</summary>
        public int MaxStackCount;

        /// <summary>
        /// 衰减配置（可选）
        /// </summary>
        public DecayConfig? Decay;

        public override string ToString()
            => $"{Type}[{StackKey}] x{MaxStackCount}";
    }

    /// <summary>
    /// 衰减配置
    /// </summary>
    [Serializable]
    public struct DecayConfig
    {
        /// <summary>是否在持续时间结束后自动衰减</summary>
        public bool AutoDecay;

        /// <summary>每层衰减时间（秒）。0 表示不自动衰减。</summary>
        public float DecayPerStackDuration;

        /// <summary>每层衰减时减少的叠加数</summary>
        public int DecayPerStackCount;

        /// <summary>是否完全移除（true）或保留 1 层（false）</summary>
        public bool RemoveOnFullDecay;
    }
}
