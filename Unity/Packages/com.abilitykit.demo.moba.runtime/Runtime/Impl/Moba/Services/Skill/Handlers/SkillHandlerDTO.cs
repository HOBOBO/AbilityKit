using System;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    // ========================================================================
    // 技能释放流程中的可选处理项
    //
    // 设计思路：
    //  1. 每个处理项都是一个独立的模块，可选地添加到技能流程中
    //  2. 通过配置决定有哪些处理项，以及它们的执行顺序
    //  3. 处理项可以是"检查"（条件验证）或"操作"（消耗、Buff等）
    //  4. 不绑定具体业务逻辑，通过 ActionId 路由到具体实现
    //
    // Luban 导出示例：
    // {
    //   "SkillId": 1001,
    //   "PreCastHandlers": [
    //     { "Type": "check_cooldown" },
    //     { "Type": "check_resource" },
    //     { "Type": "consume_mana", "Amount": 100 },
    //     { "Type": "apply_buff", "BuffId": 2001 }
    //   ]
    // }
    // ========================================================================

    /// <summary>
    /// 处理项类型
    /// </summary>
    public enum EHandlerType
    {
        // ========== 检查类 ==========
        /// <summary>冷却检查</summary>
        CheckCooldown = 1,
        /// <summary>资源检查（资源是否足够，但不扣除）</summary>
        CheckResource = 2,
        /// <summary>状态检查（眩晕、沉默等）</summary>
        CheckState = 3,
        /// <summary>目标检查（距离、有效性等）</summary>
        CheckTarget = 4,

        // ========== 操作类 ==========
        /// <summary>消耗资源（检查后实际扣除）</summary>
        ConsumeResource = 101,
        /// <summary>开始冷却</summary>
        StartCooldown = 102,
        /// <summary>添加Buff</summary>
        ApplyBuff = 103,
        /// <summary>添加标签</summary>
        AddTag = 104,
        /// <summary>移除标签</summary>
        RemoveTag = 105,

        // ========== 通用 ==========
        /// <summary>自定义Action（通过 ActionId 路由）</summary>
        CustomAction = 1000,
    }

    /// <summary>
    /// 技能处理项 DTO 基类
    /// 所有处理项 DTO 都应继承此类
    /// </summary>
    [Serializable]
    public abstract class SkillHandlerDTO
    {
        /// <summary>
        /// 处理项类型
        /// </summary>
        public int Type;
    }

    // ========================================================================
    // 检查类处理项
    // ========================================================================

    /// <summary>
    /// 冷却检查处理项
    /// </summary>
    [Serializable]
    public class CheckCooldownDTO : SkillHandlerDTO
    {
        public CheckCooldownDTO()
        {
            Type = (int)EHandlerType.CheckCooldown;
        }
    }

    /// <summary>
    /// 资源检查处理项（检查资源是否足够，不扣除）
    /// </summary>
    [Serializable]
    public class CheckResourceDTO : SkillHandlerDTO
    {
        /// <summary>
        /// 资源类型（0=Mana, 1=Hp, 2=Energy, ...）
        /// </summary>
        public int ResourceType;

        /// <summary>
        /// 需要检查的最小值（通过 NumericRefDTO 支持常量/变量等）
        /// </summary>
        public NumericRefDTO MinAmount;

        public CheckResourceDTO()
        {
            Type = (int)EHandlerType.CheckResource;
        }
    }

    /// <summary>
    /// 状态检查处理项（眩晕、沉默、禁言等）
    /// </summary>
    [Serializable]
    public class CheckStateDTO : SkillHandlerDTO
    {
        /// <summary>
        /// 要检查的状态标签列表
        /// </summary>
        public string[] RequiredTags;

        /// <summary>
        /// 不能有的状态标签列表
        /// </summary>
        public string[] BlockedTags;

        /// <summary>
        /// 检查对象：0=Caster, 1=Target
        /// </summary>
        public int Target;

        public CheckStateDTO()
        {
            Type = (int)EHandlerType.CheckState;
        }
    }

    /// <summary>
    /// 目标检查处理项
    /// </summary>
    [Serializable]
    public class CheckTargetDTO : SkillHandlerDTO
    {
        /// <summary>
        /// 是否必须有目标
        /// </summary>
        public bool RequireTarget;

        /// <summary>
        /// 最小距离（0表示不检查）
        /// </summary>
        public NumericRefDTO MinDistance;

        /// <summary>
        /// 最大距离（0表示不检查）
        /// </summary>
        public NumericRefDTO MaxDistance;

        /// <summary>
        /// 目标必须满足的标签
        /// </summary>
        public string[] TargetTags;

        public CheckTargetDTO()
        {
            Type = (int)EHandlerType.CheckTarget;
        }
    }

    // ========================================================================
    // 操作类处理项
    // ========================================================================

    /// <summary>
    /// 资源消耗处理项
    /// </summary>
    [Serializable]
    public class ConsumeResourceDTO : SkillHandlerDTO
    {
        /// <summary>
        /// 资源类型
        /// </summary>
        public int ResourceType;

        /// <summary>
        /// 消耗量（支持常量/变量/表达式）
        /// </summary>
        public NumericRefDTO Amount;

        /// <summary>
        /// 失败提示 Key
        /// </summary>
        public string FailMessageKey;

        public ConsumeResourceDTO()
        {
            Type = (int)EHandlerType.ConsumeResource;
        }
    }

    /// <summary>
    /// 开始冷却处理项
    /// </summary>
    [Serializable]
    public class StartCooldownDTO : SkillHandlerDTO
    {
        /// <summary>
        /// 冷却时间（毫秒，支持变量）
        /// </summary>
        public NumericRefDTO CooldownMs;

        public StartCooldownDTO()
        {
            Type = (int)EHandlerType.StartCooldown;
        }
    }

    /// <summary>
    /// 添加Buff处理项
    /// </summary>
    [Serializable]
    public class ApplyBuffDTO : SkillHandlerDTO
    {
        /// <summary>
        /// Buff配置ID
        /// </summary>
        public int BuffId;

        /// <summary>
        /// 添加目标：0=Caster, 1=Target
        /// </summary>
        public int Target;

        /// <summary>
        /// 叠加策略
        /// </summary>
        public int StackPolicy;

        public ApplyBuffDTO()
        {
            Type = (int)EHandlerType.ApplyBuff;
        }
    }

    /// <summary>
    /// 添加标签处理项
    /// </summary>
    [Serializable]
    public class AddTagDTO : SkillHandlerDTO
    {
        /// <summary>
        /// 要添加的标签列表
        /// </summary>
        public string[] Tags;

        /// <summary>
        /// 添加目标：0=Caster, 1=Target
        /// </summary>
        public int Target;

        /// <summary>
        /// 持续时间（毫秒），-1表示永久
        /// </summary>
        public NumericRefDTO DurationMs;

        public AddTagDTO()
        {
            Type = (int)EHandlerType.AddTag;
        }
    }

    /// <summary>
    /// 移除标签处理项
    /// </summary>
    [Serializable]
    public class RemoveTagDTO : SkillHandlerDTO
    {
        /// <summary>
        /// 要移除的标签列表
        /// </summary>
        public string[] Tags;

        /// <summary>
        /// 移除目标：0=Caster, 1=Target
        /// </summary>
        public int Target;

        public RemoveTagDTO()
        {
            Type = (int)EHandlerType.RemoveTag;
        }
    }

    // ========================================================================
    // 通用处理项
    // ========================================================================

    /// <summary>
    /// 自定义Action处理项
    /// 通过 ActionId 路由到具体的 PlanAction 实现
    /// </summary>
    [Serializable]
    public class CustomActionDTO : SkillHandlerDTO
    {
        /// <summary>
        /// Action名称（如 "consume_resource", "give_damage" 等）
        /// </summary>
        public string ActionName;

        /// <summary>
        /// 具名参数字典
        /// </summary>
        public NamedArgDTO[] Args;

        public CustomActionDTO()
        {
            Type = (int)EHandlerType.CustomAction;
        }
    }

    /// <summary>
    /// 具名参数 DTO
    /// </summary>
    [Serializable]
    public class NamedArgDTO
    {
        /// <summary>
        /// 参数名称
        /// </summary>
        public string Name;

        /// <summary>
        /// 参数值
        /// </summary>
        public NumericRefDTO Value;
    }

    // ========================================================================
    // 技能流程配置
    // ========================================================================

    /// <summary>
    /// 技能流程处理配置
    /// 包含所有可选的处理项
    /// </summary>
    [Serializable]
    public class SkillFlowHandlerConfigDTO
    {
        /// <summary>
        /// 释放前处理项列表（按顺序执行）
        /// </summary>
        public SkillHandlerDTO[] PreCastHandlers;

        /// <summary>
        /// 释放后处理项列表
        /// </summary>
        public SkillHandlerDTO[] PostCastHandlers;

        /// <summary>
        /// 释放失败时处理项列表（用于回滚）
        /// </summary>
        public SkillHandlerDTO[] OnFailHandlers;
    }
}
