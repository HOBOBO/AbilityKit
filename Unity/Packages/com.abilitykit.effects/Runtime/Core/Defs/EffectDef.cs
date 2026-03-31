using System;
using System.Linq;
using AbilityKit.Effects.Core.Model;

namespace AbilityKit.Effects.Core.Defs
{
    /// <summary>
    /// 效果定义 DTO（用于数据导入/序列化）
    /// </summary>
    [Serializable]
    public sealed class EffectDef
    {
        public string EffectId;
        public EffectScopeDef DefaultScope;
        public EffectItemDef[] Items;

        /// <summary>
        /// 检查是否为有效定义
        /// </summary>
        public bool IsValid => !string.IsNullOrEmpty(EffectId);

        /// <summary>
        /// 获取属性项数量
        /// </summary>
        public int ItemCount => Items?.Length ?? 0;

        /// <summary>
        /// 获取或设置属性项（带空值检查）
        /// </summary>
        public EffectItemDef GetItem(int index) => Items != null && index >= 0 && index < Items.Length ? Items[index] : null;

        /// <summary>
        /// 尝试获取第一个匹配类型的属性项
        /// </summary>
        public bool TryGetFirstItem(string type, out EffectItemDef item)
        {
            item = null;
            if (Items == null) return false;

            for (int i = 0; i < Items.Length; i++)
            {
                var it = Items[i];
                if (it != null && string.Equals(it.Type, type, StringComparison.OrdinalIgnoreCase))
                {
                    item = it;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 获取所有指定类型的属性项
        /// </summary>
        public int GetItemsOfType(string type, out EffectItemDef[] results)
        {
            if (Items == null || Items.Length == 0)
            {
                results = Array.Empty<EffectItemDef>();
                return 0;
            }

            var list = Items.Where(it => it != null && string.Equals(it.Type, type, StringComparison.OrdinalIgnoreCase)).ToArray();
            results = list;
            return list.Length;
        }

        /// <summary>
        /// 清理空引用
        /// </summary>
        public void TrimNulls()
        {
            if (Items != null)
            {
                Items = Items.Where(it => it != null).ToArray();
            }
        }
    }

    /// <summary>
    /// 效果作用域定义 DTO
    /// </summary>
    [Serializable]
    public sealed class EffectScopeDef
    {
        public string Kind;
        public int Id;

        /// <summary>
        /// 检查是否为有效定义
        /// </summary>
        public bool IsValid => !string.IsNullOrEmpty(Kind);

        /// <summary>
        /// 获取调试字符串
        /// </summary>
        public string ToDebugString() => $"{Kind}:{Id}";
    }

    /// <summary>
    /// 效果属性项定义 DTO
    /// </summary>
    [Serializable]
    public sealed class EffectItemDef
    {
        public string Type;
        public string Key;
        public string Op;
        public EffectValueDef Value;
        public EffectScopeDef Scope;

        /// <summary>
        /// 检查是否为有效定义
        /// </summary>
        public bool IsValid => !string.IsNullOrEmpty(Type) && !string.IsNullOrEmpty(Key) && Value != null;

        /// <summary>
        /// 是否为Stat类型
        /// </summary>
        public bool IsStat => string.Equals(Type, "Stat", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// 获取调试字符串
        /// </summary>
        public string ToDebugString() =>
            Value != null ? $"{Type}.{Key} {Op} {Value.F}(F)/{Value.I}(I)" : $"{Type}.{Key} {Op} null";
    }

    /// <summary>
    /// 效果值定义 DTO
    /// </summary>
    [Serializable]
    public sealed class EffectValueDef
    {
        /// <summary>
        /// 值模式：Int/Float
        /// </summary>
        public string Mode;

        /// <summary>
        /// 浮点值
        /// </summary>
        public float F;

        /// <summary>
        /// 整数值
        /// </summary>
        public int I;

        /// <summary>
        /// 浮点最小值（用于范围）
        /// </summary>
        public float Min;

        /// <summary>
        /// 浮点最大值（用于范围）
        /// </summary>
        public float Max;

        /// <summary>
        /// 整数最小值（用于范围）
        /// </summary>
        public int MinInt;

        /// <summary>
        /// 整数最大值（用于范围）
        /// </summary>
        public int MaxInt;

        /// <summary>
        /// 是否使用范围值
        /// </summary>
        public bool HasRange => Mode == "Range";

        /// <summary>
        /// 是否为浮点模式
        /// </summary>
        public bool IsFloat => string.Equals(Mode, "Float", StringComparison.OrdinalIgnoreCase) || Mode == null;

        /// <summary>
        /// 是否为整数模式
        /// </summary>
        public bool IsInt => string.Equals(Mode, "Int", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// 获取整数值（考虑模式）
        /// </summary>
        public int GetIntValue()
        {
            return Mode switch
            {
                "Int" => I,
                "Range" => MinInt + (MaxInt > MinInt ? new Random().Next(MaxInt - MinInt + 1) : 0),
                _ => (int)F
            };
        }

        /// <summary>
        /// 获取浮点值（考虑模式）
        /// </summary>
        public float GetFloatValue()
        {
            return Mode switch
            {
                "Int" => I,
                "Range" => Min + (Max > Min ? (float)(new Random().NextDouble() * (Max - Min)) : 0f),
                _ => F
            };
        }

        /// <summary>
        /// 获取值并转换为 EffectValue
        /// </summary>
        public EffectValue ToEffectValue()
        {
            if (IsInt)
                return new EffectValue(GetIntValue());
            return new EffectValue(GetFloatValue());
        }

        /// <summary>
        /// 设置默认值
        /// </summary>
        public void SetDefaults(bool isFloat)
        {
            if (string.IsNullOrEmpty(Mode))
            {
                Mode = isFloat ? "Float" : "Int";
            }
            if (isFloat)
            {
                I = 0;
            }
            else
            {
                F = 0f;
            }
        }
    }
}
