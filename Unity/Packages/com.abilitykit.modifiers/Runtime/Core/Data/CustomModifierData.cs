using System;

namespace AbilityKit.Modifiers
{
    // ============================================================================
    // 自定义修改器数据
    // ============================================================================

    /// <summary>
    /// 自定义修改器数据。
    /// 用于存储非数值类型的修改器数据。
    ///
    /// 使用示例：
    /// ```csharp
    /// // 技能 ID
    /// CustomModifierData.Int(999)
    ///
    /// // 标签/状态键
    /// CustomModifierData.String("Invincible")
    ///
    /// // 复杂数据（JSON 序列化）
    /// CustomModifierData.Bytes(jsonBytes)
    /// ```
    /// </summary>
    [Serializable]
    public struct CustomModifierData : IEquatable<CustomModifierData>
    {
        /// <summary>自定义类型 ID（业务层定义）</summary>
        public int CustomTypeId;

        /// <summary>整数数据</summary>
        public int IntValue;

        /// <summary>字符串数据</summary>
        public string StringValue;

        /// <summary>原始字节数据（用于序列化复杂结构）</summary>
        public byte[] RawData;

        #region 工厂方法

        /// <summary>创建整数数据</summary>
        public static CustomModifierData Int(int value)
            => new() { CustomTypeId = 1, IntValue = value };

        /// <summary>创建字符串数据</summary>
        public static CustomModifierData String(string value)
            => new() { CustomTypeId = 2, StringValue = value };

        /// <summary>创建字节数据</summary>
        public static CustomModifierData Bytes(byte[] data)
            => new() { CustomTypeId = 3, RawData = data };

        /// <summary>创建技能 ID 修改器</summary>
        public static CustomModifierData SkillId(int skillId)
            => new() { CustomTypeId = 100, IntValue = skillId };

        #endregion

        #region 静态属性

        /// <summary>空/无效的自定义数据</summary>
        public static CustomModifierData None => default;

        #endregion

        #region 辅助属性

        /// <summary>是否为空</summary>
        public bool IsEmpty => CustomTypeId == 0 && IntValue == 0;

        #endregion

        #region IEquatable

        public bool Equals(CustomModifierData other)
            => CustomTypeId == other.CustomTypeId && IntValue == other.IntValue;

        public override bool Equals(object obj) => obj is CustomModifierData other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(CustomTypeId, IntValue);

        #endregion
    }
}
