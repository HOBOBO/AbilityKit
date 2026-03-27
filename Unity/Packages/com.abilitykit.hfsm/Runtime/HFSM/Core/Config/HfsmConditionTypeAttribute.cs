using System;

namespace UnityHFSM.Config
{
    /// <summary>
    /// 标记一个条件类对应的配置类型名称，用于序列化/反序列化
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public class HfsmConditionTypeAttribute : Attribute
    {
        /// <summary>
        /// 类型的唯一标识符，用于序列化
        /// </summary>
        public string TypeName { get; }

        /// <summary>
        /// 类型的显示名称，用于编辑器 UI
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// 类型描述
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// 分类标签，用于编辑器分组
        /// </summary>
        public string Category { get; }

        public HfsmConditionTypeAttribute(string typeName, string displayName = null, string description = null, string category = null)
        {
            TypeName = typeName ?? throw new ArgumentNullException(nameof(typeName));
            DisplayName = displayName ?? typeName;
            Description = description ?? string.Empty;
            Category = category ?? "Default";
        }
    }
}
