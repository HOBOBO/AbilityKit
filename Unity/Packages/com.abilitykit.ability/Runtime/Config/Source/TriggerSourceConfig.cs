using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Config.Source
{
    /// <summary>
    /// 触发器源配置根对象
    /// 这是可直接编辑的 JSON 格式，使用人类可读的名称而非哈希 ID
    /// </summary>
    [Serializable]
    public sealed class TriggerSourceConfig
    {
        /// <summary>
        /// JSON Schema 版本，用于格式验证
        /// </summary>
        public string Schema = "abilitykit-trigger-source-v1";

        /// <summary>
        /// 配置版本号
        /// </summary>
        public string Version = "1.0";

        /// <summary>
        /// 元数据
        /// </summary>
        public SourceMetadata Metadata;

        /// <summary>
        /// 预定义变量声明
        /// </summary>
        public List<SourceVariable> Variables;

        /// <summary>
        /// 动作类型定义（可选，用于 IDE 提示和验证）
        /// </summary>
        public Dictionary<string, ActionTypeDefinition> Actions;

        /// <summary>
        /// 条件类型定义（可选，用于 IDE 提示和验证）
        /// </summary>
        public Dictionary<string, ConditionTypeDefinition> Conditions;

        /// <summary>
        /// 触发器列表
        /// </summary>
        public List<SourceTriggerConfig> Triggers;
    }

    /// <summary>
    /// 元数据
    /// </summary>
    [Serializable]
    public sealed class SourceMetadata
    {
        /// <summary>
        /// 作者/团队
        /// </summary>
        public string Author = "team";

        /// <summary>
        /// 创建时间（ISO 8601）
        /// </summary>
        public string CreatedAt;

        /// <summary>
        /// 描述
        /// </summary>
        public string Description = "触发器源配置（可直接编辑）";

        /// <summary>
        /// 最后修改时间
        /// </summary>
        public string LastModified;

        public SourceMetadata()
        {
            CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            LastModified = CreatedAt;
        }
    }

    /// <summary>
    /// 预定义变量
    /// </summary>
    [Serializable]
    public sealed class SourceVariable
    {
        /// <summary>
        /// 变量名（带 $ 前缀）
        /// </summary>
        public string Name;

        /// <summary>
        /// 描述
        /// </summary>
        public string Description;

        /// <summary>
        /// 默认值（可选）
        /// </summary>
        public object DefaultValue;

        public SourceVariable() { }

        public SourceVariable(string name, string description = null)
        {
            Name = name;
            Description = description;
        }
    }

    /// <summary>
    /// 单个触发器配置
    /// </summary>
    [Serializable]
    public sealed class SourceTriggerConfig
    {
        /// <summary>
        /// 触发器唯一 ID（正整数）
        /// </summary>
        public int Id;

        /// <summary>
        /// 触发器名称（人类可读）
        /// </summary>
        public string Name;

        /// <summary>
        /// 绑定的事件名称
        /// </summary>
        public string Event;

        /// <summary>
        /// 优先级（数字越大优先级越高）
        /// </summary>
        public int Priority = 0;

        /// <summary>
        /// 执行阶段
        /// </summary>
        public string Phase = "immediate";

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool Enabled = true;

        /// <summary>
        /// 是否允许外部触发
        /// </summary>
        public bool AllowExternal = false;

        /// <summary>
        /// 条件列表（为空表示无条件）
        /// </summary>
        public List<SourceConditionConfig> Conditions;

        /// <summary>
        /// 动作列表
        /// </summary>
        public List<SourceActionConfig> Actions;

        /// <summary>
        /// 备注
        /// </summary>
        public string Comment;
    }

    /// <summary>
    /// 动作配置
    /// </summary>
    [Serializable]
    public class SourceActionConfig
    {
        /// <summary>
        /// 动作类型
        /// </summary>
        public string Type;

        /// <summary>
        /// 子动作列表（用于 seq 等复合动作）
        /// </summary>
        public List<SourceActionConfig> Items;

        /// <summary>
        /// 具名参数字典
        /// 注意：运行时参数名可能与这里的 key 不同，这里是源格式
        /// </summary>
        [Newtonsoft.Json.JsonExtensionData]
        public Dictionary<string, object> Args;
    }

    /// <summary>
    /// 条件配置
    /// </summary>
    [Serializable]
    public class SourceConditionConfig
    {
        /// <summary>
        /// 条件类型
        /// </summary>
        public string Type;

        /// <summary>
        /// 子条件列表（用于 all, any 等复合条件）
        /// </summary>
        public List<SourceConditionConfig> Items;

        /// <summary>
        /// 单一子条件（用于 not 等）
        /// </summary>
        public SourceConditionConfig Item;

        /// <summary>
        /// 具名参数字典
        /// </summary>
        [Newtonsoft.Json.JsonExtensionData]
        public Dictionary<string, object> Args;
    }

    /// <summary>
    /// 动作类型定义（用于元数据）
    /// </summary>
    [Serializable]
    public sealed class ActionTypeDefinition
    {
        /// <summary>
        /// 类型名
        /// </summary>
        public string Type;

        /// <summary>
        /// 显示名称
        /// </summary>
        public string DisplayName;

        /// <summary>
        /// 描述
        /// </summary>
        public string Description;

        /// <summary>
        /// 分类
        /// </summary>
        public string Category;

        /// <summary>
        /// 参数定义
        /// </summary>
        public List<ParameterDefinition> Params;

        /// <summary>
        /// 是否是复合动作（会包含 Items）
        /// </summary>
        public bool IsComposite = false;
    }

    /// <summary>
    /// 条件类型定义（用于元数据）
    /// </summary>
    [Serializable]
    public sealed class ConditionTypeDefinition
    {
        /// <summary>
        /// 类型名
        /// </summary>
        public string Type;

        /// <summary>
        /// 显示名称
        /// </summary>
        public string DisplayName;

        /// <summary>
        /// 描述
        /// </summary>
        public string Description;

        /// <summary>
        /// 分类
        /// </summary>
        public string Category;

        /// <summary>
        /// 参数定义
        /// </summary>
        public List<ParameterDefinition> Params;

        /// <summary>
        /// 是否是复合条件（会包含 Items 或 Item）
        /// </summary>
        public bool IsComposite = false;
    }

    /// <summary>
    /// 参数定义
    /// </summary>
    [Serializable]
    public sealed class ParameterDefinition
    {
        /// <summary>
        /// 参数名
        /// </summary>
        public string Name;

        /// <summary>
        /// 参数类型
        /// </summary>
        public string Type;

        /// <summary>
        /// 是否必填
        /// </summary>
        public bool Required = true;

        /// <summary>
        /// 默认值
        /// </summary>
        public object DefaultValue;

        /// <summary>
        /// 描述
        /// </summary>
        public string Description;

        /// <summary>
        /// 允许的值列表（枚举）
        /// </summary>
        public List<string> AllowedValues;

        public ParameterDefinition() { }

        public ParameterDefinition(string name, string type, bool required = true)
        {
            Name = name;
            Type = type;
            Required = required;
        }
    }
}
