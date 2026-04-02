using System;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    // ========================================================================
    // 条件 DTO 基类 — 用于 Luban 导出
    //
    // 设计原则:
    //  1. 所有条件 DTO 继承此抽象类，用于 Luban 识别和导出
    //  2. 基类是空的，具体参数在各自的 DTO 类中定义
    //  3. 通过转换器转为触发器的 ICondition，实现代码复用
    // ========================================================================

    /// <summary>
    /// 条件 DTO 空基类
    /// 所有配置化条件都应继承此类，用于 Luban 导出识别
    /// </summary>
    [Serializable]
    public abstract class SkillConditionDTO
    {
        /// <summary>
        /// 条件类型标识（对应触发器中的 Condition 类型名）
        /// </summary>
        public string Type;
    }

    // ========================================================================
    // 简单条件 DTO（无额外参数）
    // ========================================================================

    /// <summary>
    /// 常量条件 DTO - 始终返回固定结果
    /// </summary>
    [Serializable]
    public class ConstConditionDTO : SkillConditionDTO
    {
        /// <summary>
        /// 常量值（true=通过，false=失败）
        /// </summary>
        public bool Value = true;

        public ConstConditionDTO()
        {
            Type = "Const";
        }
    }

    /// <summary>
    /// 目标存在条件 DTO
    /// </summary>
    [Serializable]
    public class HasTargetConditionDTO : SkillConditionDTO
    {
        /// <summary>
        /// 是否取反（true=要求没有目标）
        /// </summary>
        public bool Negate;

        public HasTargetConditionDTO()
        {
            Type = "HasTarget";
        }
    }

    // ========================================================================
    // 复合条件 DTO
    // ========================================================================

    /// <summary>
    /// And 组合条件 DTO
    /// </summary>
    [Serializable]
    public class AndConditionDTO : SkillConditionDTO
    {
        public SkillConditionDTO Left;
        public SkillConditionDTO Right;

        public AndConditionDTO()
        {
            Type = "And";
        }
    }

    /// <summary>
    /// Or 组合条件 DTO
    /// </summary>
    [Serializable]
    public class OrConditionDTO : SkillConditionDTO
    {
        public SkillConditionDTO Left;
        public SkillConditionDTO Right;

        public OrConditionDTO()
        {
            Type = "Or";
        }
    }

    /// <summary>
    /// Not 条件 DTO
    /// </summary>
    [Serializable]
    public class NotConditionDTO : SkillConditionDTO
    {
        public SkillConditionDTO Inner;

        public NotConditionDTO()
        {
            Type = "Not";
        }
    }

    /// <summary>
    /// 多条件组合 DTO（支持多个条件）
    /// </summary>
    [Serializable]
    public class MultiConditionDTO : SkillConditionDTO
    {
        /// <summary>
        /// 组合方式：0 = And, 1 = Or
        /// </summary>
        public int Combinator;

        /// <summary>
        /// 子条件列表
        /// </summary>
        public SkillConditionDTO[] Conditions;

        public MultiConditionDTO()
        {
            Type = "Multi";
        }
    }

    // ========================================================================
    // 数值引用 DTO
    // 与触发器包中的 NumericValueRef 对齐
    // ========================================================================

    /// <summary>
    /// 数值引用类型（与 ENumericValueRefKind 对齐）
    /// </summary>
    [Serializable]
    public enum ENumericRefKind : byte
    {
        Const = 0,
        Blackboard = 1,
        PayloadField = 2,
        Var = 3,
        Expr = 4,
    }

    /// <summary>
    /// 比较操作符
    /// </summary>
    [Serializable]
    public enum ECompareOp : byte
    {
        Equal = 0,
        NotEqual = 1,
        GreaterThan = 2,
        GreaterThanOrEqual = 3,
        LessThan = 4,
        LessThanOrEqual = 5,
    }

    /// <summary>
    /// 数值引用 DTO
    /// 与触发器包中的 NumericValueRef 结构对齐
    /// </summary>
    [Serializable]
    public class NumericRefDTO
    {
        /// <summary>
        /// 引用类型
        /// </summary>
        public ENumericRefKind Kind;

        /// <summary>
        /// 常量值（Kind = Const 时使用）
        /// </summary>
        public double ConstValue;

        /// <summary>
        /// 黑板ID（Kind = Blackboard 时使用）
        /// </summary>
        public int BoardId;

        /// <summary>
        /// 黑板键ID（Kind = Blackboard 时使用）
        /// </summary>
        public int KeyId;

        /// <summary>
        /// 字段ID（Kind = PayloadField 时使用）
        /// </summary>
        public int FieldId;

        /// <summary>
        /// 域ID（Kind = Var 时使用）
        /// </summary>
        public string DomainId;

        /// <summary>
        /// 键名（Kind = Var 时使用）
        /// </summary>
        public string Key;

        /// <summary>
        /// 表达式文本（Kind = Expr 时使用）
        /// </summary>
        public string ExprText;

        /// <summary>
        /// 创建常量引用
        /// </summary>
        public static NumericRefDTO Const(double value) => new NumericRefDTO { Kind = ENumericRefKind.Const, ConstValue = value };

        /// <summary>
        /// 创建黑板引用
        /// </summary>
        public static NumericRefDTO Blackboard(int boardId, int keyId) => new NumericRefDTO { Kind = ENumericRefKind.Blackboard, BoardId = boardId, KeyId = keyId };

        /// <summary>
        /// 创建 Payload 字段引用
        /// </summary>
        public static NumericRefDTO PayloadField(int fieldId) => new NumericRefDTO { Kind = ENumericRefKind.PayloadField, FieldId = fieldId };

        /// <summary>
        /// 创建变量引用
        /// </summary>
        public static NumericRefDTO Var(string domainId, string key) => new NumericRefDTO { Kind = ENumericRefKind.Var, DomainId = domainId, Key = key };

        /// <summary>
        /// 创建表达式引用
        /// </summary>
        public static NumericRefDTO Expr(string exprText) => new NumericRefDTO { Kind = ENumericRefKind.Expr, ExprText = exprText };
    }

    /// <summary>
    /// 数值比较条件 DTO
    /// </summary>
    [Serializable]
    public class NumericCompareConditionDTO : SkillConditionDTO
    {
        /// <summary>
        /// 比较操作符
        /// </summary>
        public ECompareOp Op;

        /// <summary>
        /// 左操作数
        /// </summary>
        public NumericRefDTO Left;

        /// <summary>
        /// 右操作数
        /// </summary>
        public NumericRefDTO Right;

        public NumericCompareConditionDTO()
        {
            Type = "NumericCompare";
        }
    }

    /// <summary>
    /// Payload 字段比较条件 DTO
    /// </summary>
    [Serializable]
    public class PayloadCompareConditionDTO : SkillConditionDTO
    {
        /// <summary>
        /// Payload 字段ID
        /// </summary>
        public int FieldId;

        /// <summary>
        /// 比较操作符
        /// </summary>
        public ECompareOp Op;

        /// <summary>
        /// 比较值
        /// </summary>
        public NumericRefDTO CompareValue;

        /// <summary>
        /// 是否取反
        /// </summary>
        public bool Negate;

        public PayloadCompareConditionDTO()
        {
            Type = "PayloadCompare";
        }
    }

    // ========================================================================
    // Moba 特有条件 DTO
    // 这些是 Moba 业务特有的条件，不通用
    // ========================================================================

    /// <summary>
    /// 冷却条件 DTO（Moba 特有）
    /// </summary>
    [Serializable]
    public class CooldownConditionDTO : SkillConditionDTO
    {
        public CooldownConditionDTO()
        {
            Type = "Moba_Cooldown";
        }
    }

    /// <summary>
    /// 施法状态条件 DTO（Moba 特有）
    /// </summary>
    [Serializable]
    public class CastingStateConditionDTO : SkillConditionDTO
    {
        /// <summary>
        /// 是否检查正在施法（false=检查未在施法）
        /// </summary>
        public bool ExpectCasting;

        public CastingStateConditionDTO()
        {
            Type = "Moba_CastingState";
        }
    }

    /// <summary>
    /// 自身释放条件 DTO（Moba 特有）
    /// </summary>
    [Serializable]
    public class SelfOnlyConditionDTO : SkillConditionDTO
    {
        public SelfOnlyConditionDTO()
        {
            Type = "Moba_SelfOnly";
        }
    }

    /// <summary>
    /// 标签条件 DTO（Moba 特有，基于触发器的 TagQuery）
    /// </summary>
    [Serializable]
    public class TagConditionDTO : SkillConditionDTO
    {
        /// <summary>
        /// 需要的标签列表
        /// </summary>
        public string[] RequiredTags;

        /// <summary>
        /// 忽略的标签列表
        /// </summary>
        public string[] IgnoreTags;

        /// <summary>
        /// 是否取反
        /// </summary>
        public bool Negate;

        public TagConditionDTO()
        {
            Type = "Moba_Tag";
        }
    }
}
