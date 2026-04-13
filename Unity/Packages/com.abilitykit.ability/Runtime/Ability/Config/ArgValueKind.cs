using System;

namespace AbilityKit.Ability.Config
{
    /// <summary>
    /// 运行时参数值类型枚举
    /// </summary>
    public enum ArgValueKind
    {
        None = 0,
        Int = 1,
        Float = 2,
        Bool = 3,
        String = 4,
        Object = 5
    }

    /// <summary>
    /// 运行时参数条目（核心版本，不依赖 Unity）
    /// Unity 实现使用 GlobalVarEntry 或 engine 包中的 ArgRuntimeEntry
    /// </summary>
    [Serializable]
    public class ArgRuntimeEntryCore
    {
        public string Key;
        public ArgValueKind Kind;
        public object Value;

        public object GetBoxedValue()
        {
            return Value;
        }

        public ArgRuntimeEntryCore Clone()
        {
            return new ArgRuntimeEntryCore
            {
                Key = Key,
                Kind = Kind,
                Value = Value
            };
        }
    }
}
