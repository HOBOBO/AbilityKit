using System;

namespace AbilityKit.Effects.Core.Model
{
    /// <summary>
    /// 效果定义（只读）
    /// </summary>
    public sealed class EffectDefinition : IEquatable<EffectDefinition>
    {
        public readonly string EffectId;
        public readonly EffectScopeKey DefaultScope;
        public readonly EffectStatItem[] Stats;

        /// <summary>
        /// 缓存的统计数据（避免重复计算）
        /// </summary>
        private readonly int _statsHashCode;
        private readonly int _intStatCount;
        private readonly int _floatStatCount;

        public EffectDefinition(string effectId, in EffectScopeKey defaultScope, EffectStatItem[] stats)
        {
            EffectId = effectId ?? string.Empty;
            DefaultScope = defaultScope;
            Stats = stats ?? Array.Empty<EffectStatItem>();

            // 计算缓存数据
            var hash = new HashCode();
            hash.Add(EffectId);
            hash.Add(DefaultScope);
            _statsHashCode = hash.ToHashCode();

            int intCount = 0, floatCount = 0;
            for (int i = 0; i < Stats.Length; i++)
            {
                if (Stats[i].IsIntegerValue) intCount++;
                else floatCount++;
            }
            _intStatCount = intCount;
            _floatStatCount = floatCount;
        }

        /// <summary>
        /// 统计数据数量
        /// </summary>
        public int StatsCount => Stats.Length;

        /// <summary>
        /// 整数属性项数量
        /// </summary>
        public int IntStatCount => _intStatCount;

        /// <summary>
        /// 浮点属性项数量
        /// </summary>
        public int FloatStatCount => _floatStatCount;

        /// <summary>
        /// 是否为空定义
        /// </summary>
        public bool IsEmpty => Stats.Length == 0;

        /// <summary>
        /// 哈希码（基于定义标识，不基于内容）
        /// </summary>
        public int DefinitionHashCode => _statsHashCode;

        /// <summary>
        /// 根据 KeyId 查找属性项
        /// </summary>
        public EffectStatItem FindStatByKey(int keyId)
        {
            for (int i = 0; i < Stats.Length; i++)
            {
                if (Stats[i].KeyId == keyId)
                    return Stats[i];
            }
            return null;
        }

        /// <summary>
        /// 根据 KeyId 查找所有匹配的属性项
        /// </summary>
        public int FindStatsByKey(int keyId, EffectStatItem[] results)
        {
            int count = 0;
            for (int i = 0; i < Stats.Length && count < results.Length; i++)
            {
                if (Stats[i].KeyId == keyId)
                    results[count++] = Stats[i];
            }
            return count;
        }

        /// <summary>
        /// 获取所有指定操作类型的属性项
        /// </summary>
        public int GetStatsByOp(EffectOp op, EffectStatItem[] results)
        {
            int count = 0;
            for (int i = 0; i < Stats.Length && count < results.Length; i++)
            {
                if (Stats[i].Op == op)
                    results[count++] = Stats[i];
            }
            return count;
        }

        /// <summary>
        /// 是否包含指定 KeyId 的属性项
        /// </summary>
        public bool HasKey(int keyId)
        {
            for (int i = 0; i < Stats.Length; i++)
            {
                if (Stats[i].KeyId == keyId)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 批量创建 EffectDefinition 的工厂方法
        /// </summary>
        public static EffectDefinition Create(string effectId, EffectStatItem[] stats) =>
            new EffectDefinition(effectId, default, stats);

        public bool Equals(EffectDefinition other)
        {
            if (other is null) return false;
            return ReferenceEquals(this, other) || _statsHashCode == other._statsHashCode && EffectId == other.EffectId;
        }

        public override bool Equals(object obj) => obj is EffectDefinition other && Equals(other);

        public override int GetHashCode() => _statsHashCode;

        public override string ToString() => $"EffectDef({EffectId}, Stats={Stats.Length})";
    }
}
