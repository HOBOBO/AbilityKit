#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace AbilityKit.Ability.Editor.Utilities
{
    /// <summary>
    /// 可读的触发器计划格式 - 用于人工编辑
    /// 
    /// 特点：
    /// 1. 使用字符串名称代替 ActionId 哈希值
    /// 2. 每个 Action 直接展示参数，无需嵌套结构
    /// 3. ActionDefs 集中定义所有动作的参数列表
    /// 4. 支持导入导出，方便人工配置
    /// </summary>
    [Serializable]
    internal sealed class ReadableTriggerPlanDatabase
    {
        /// <summary>
        /// 格式版本，用于兼容性检查
        /// </summary>
        [JsonProperty("$schema")]
        public string Schema => "ability-trigger-plan-readable-v1";

        /// <summary>
        /// 动作定义 - 映射动作名称到参数列表
        /// </summary>
        public Dictionary<string, ReadableActionDef> ActionDefs = new();

        /// <summary>
        /// 触发器列表
        /// </summary>
        public List<ReadableTriggerPlan> Triggers = new();

        /// <summary>
        /// 字符串表 - 用于存储需要国际化的字符串
        /// </summary>
        public Dictionary<string, string> Strings = new();
    }

    /// <summary>
    /// 动作定义
    /// </summary>
    [Serializable]
    internal sealed class ReadableActionDef
    {
        /// <summary>
        /// 参数名称列表
        /// </summary>
        public List<string> Args = new();

        /// <summary>
        /// 动作描述（可选）
        /// </summary>
        public string Description;
    }

    /// <summary>
    /// 可读的触发器计划
    /// </summary>
    [Serializable]
    internal sealed class ReadableTriggerPlan
    {
        /// <summary>
        /// 触发器ID
        /// </summary>
        public int TriggerId;

        /// <summary>
        /// 事件名称（人类可读）
        /// </summary>
        public string Event = "";

        /// <summary>
        /// 是否允许外部触发
        /// </summary>
        public bool AllowExternal;

        /// <summary>
        /// 阶段
        /// </summary>
        public int Phase;

        /// <summary>
        /// 优先级
        /// </summary>
        public int Priority;

        /// <summary>
        /// 条件
        /// </summary>
        public ReadablePredicate Predicate = new();

        /// <summary>
        /// 动作列表 - 使用可读格式
        /// </summary>
        public List<ReadableActionCall> Actions = new();
    }

    /// <summary>
    /// 可读的条件
    /// </summary>
    [Serializable]
    internal sealed class ReadablePredicate
    {
        /// <summary>
        /// 条件类型: none, expr, function
        /// </summary>
        public string Kind = "none";

        /// <summary>
        /// 表达式节点（当 Kind=expr 时使用）
        /// </summary>
        public List<ReadableBoolExprNode> Nodes;

        // =============== 便捷构造方法 ===============

        public static ReadablePredicate None() => new() { Kind = "none" };

        public static ReadablePredicate Expr(List<ReadableBoolExprNode> nodes)
            => new() { Kind = "expr", Nodes = nodes };
    }

    /// <summary>
    /// 可读的布尔表达式节点
    /// </summary>
    [Serializable]
    internal sealed class ReadableBoolExprNode
    {
        /// <summary>
        /// 节点类型: And, Or, Not, Compare
        /// </summary>
        public string Kind;

        /// <summary>
        /// 常量值
        /// </summary>
        public bool ConstValue;

        /// <summary>
        /// 比较操作符（当 Kind=Compare 时使用）
        /// </summary>
        public string CompareOp;

        /// <summary>
        /// 左操作数
        /// </summary>
        public ReadableValueRef Left;

        /// <summary>
        /// 右操作数
        /// </summary>
        public ReadableValueRef Right;
    }

    /// <summary>
    /// 可读的数值引用 - 简化格式
    /// </summary>
    [Serializable]
    internal sealed class ReadableValueRef
    {
        /// <summary>
        /// 值类型: Const, Board, Field, Domain, Expr
        /// </summary>
        public string Kind = "Const";

        /// <summary>
        /// 常量值（当 Kind=Const 时使用）
        /// </summary>
        public double ConstValue;

        /// <summary>
        /// 棋盘ID（当 Kind=Board 时使用）
        /// </summary>
        public int BoardId;

        /// <summary>
        /// 键ID（当 Kind=Field 时使用）
        /// </summary>
        public int KeyId;

        /// <summary>
        /// 字段ID
        /// </summary>
        public int FieldId;

        /// <summary>
        /// 域名（当 Kind=Domain 时使用）
        /// </summary>
        public string DomainId;

        /// <summary>
        /// 键名（当 Kind=Domain 时使用）
        /// </summary>
        public string Key;

        /// <summary>
        /// 表达式文本（当 Kind=Expr 时使用）
        /// </summary>
        public string ExprText;

        // =============== 便捷构造方法 ===============

        public static ReadableValueRef Const(double value) => new()
        {
            Kind = "Const",
            ConstValue = value
        };

        public static ReadableValueRef Board(int boardId, int keyId) => new()
        {
            Kind = "Board",
            BoardId = boardId,
            KeyId = keyId
        };

        public static ReadableValueRef Field(int fieldId, int keyId = 0) => new()
        {
            Kind = "Field",
            FieldId = fieldId,
            KeyId = keyId
        };

        public static ReadableValueRef Domain(string domainId, string key) => new()
        {
            Kind = "Domain",
            DomainId = domainId,
            Key = key
        };

        public static ReadableValueRef Expr(string exprText) => new()
        {
            Kind = "Expr",
            ExprText = exprText
        };

        /// <summary>
        /// 判断是否为默认值（表示空）
        /// </summary>
        public bool IsDefault()
        {
            return Kind == "Const" && ConstValue == 0 &&
                   BoardId == 0 && KeyId == 0 && FieldId == 0 &&
                   string.IsNullOrEmpty(DomainId) && string.IsNullOrEmpty(Key) &&
                   string.IsNullOrEmpty(ExprText);
        }

        /// <summary>
        /// 转为简洁的字符串表示
        /// </summary>
        public override string ToString()
        {
            return Kind switch
            {
                "Const" => ConstValue.ToString(),
                "Board" => $"Board[{BoardId}][{KeyId}]",
                "Field" => $"Field[{FieldId}][{KeyId}]",
                "Domain" => $"Domain[{DomainId}][{Key}]",
                "Expr" => $"({ExprText})",
                _ => "?"
            };
        }
    }

    /// <summary>
    /// 可读的动作调用 - 支持扁平参数和嵌套子动作
    /// </summary>
    [Serializable]
    internal sealed class ReadableActionCall
    {
        /// <summary>
        /// 动作名称（人类可读）
        /// </summary>
        public string Action;

        // 动态参数 - 通过反射序列化和反序列化
        // 不需要在类中预定义所有可能的参数

        /// <summary>
        /// 参数值字典
        /// </summary>
        [JsonExtensionData]
        public Dictionary<string, object> Args = new();

        /// <summary>
        /// 子动作列表 - 用于复合 Action（如 sequence, parallel, random 等）
        /// 当存在子动作时，这个 Action 是一个复合节点
        /// </summary>
        public List<ReadableActionCall> Children;

        /// <summary>
        /// 获取参数值
        /// </summary>
        public double GetArgAsDouble(string name, double defaultValue = 0)
        {
            if (Args.TryGetValue(name, out var value))
            {
                if (value is double d) return d;
                if (value is int i) return i;
                if (value is long l) return l;
                if (value is float f) return f;
                if (value is string s && double.TryParse(s, out var parsed)) return parsed;
            }
            return defaultValue;
        }

        /// <summary>
        /// 获取参数值作为整数
        /// </summary>
        public int GetArgAsInt(string name, int defaultValue = 0)
        {
            if (Args.TryGetValue(name, out var value))
            {
                if (value is int i) return i;
                if (value is long l) return (int)l;
                if (value is double d) return (int)d;
                if (value is float f) return (int)f;
                if (value is string s && int.TryParse(s, out var parsed)) return parsed;
            }
            return defaultValue;
        }

        /// <summary>
        /// 获取参数值作为布尔
        /// </summary>
        public bool GetArgAsBool(string name, bool defaultValue = false)
        {
            if (Args.TryGetValue(name, out var value))
            {
                if (value is bool b) return b;
                if (value is int i) return i != 0;
                if (value is double d) return d != 0;
            }
            return defaultValue;
        }
    }
}
#endif
