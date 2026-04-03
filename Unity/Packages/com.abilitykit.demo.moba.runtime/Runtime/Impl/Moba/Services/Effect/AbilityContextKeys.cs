using System;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    /// <summary>
    /// 统一的能力上下文键枚举
    /// 用于替代 Magic String，避免键名拼写错误
    /// </summary>
    public enum AbilityContextKeys
    {
        // ========== 溯源相关 ==========
        /// <summary>
        /// 溯源上下文ID（已在接口中直接定义为 SourceContextId 属性）
        /// </summary>
        SourceContextId,

        /// <summary>
        /// 上下文类型
        /// </summary>
        ContextKind,

        // ========== 参与者相关 ==========
        /// <summary>
        /// 源角色ID（技能施法者/Buff来源等）
        /// </summary>
        SourceActorId,

        /// <summary>
        /// 目标角色ID
        /// </summary>
        TargetActorId,

        // ========== 技能相关 ==========
        /// <summary>
        /// 技能ID
        /// </summary>
        SkillId,

        /// <summary>
        /// 技能槽位
        /// </summary>
        SkillSlot,

        /// <summary>
        /// 技能等级
        /// </summary>
        SkillLevel,

        /// <summary>
        /// 施法序列号
        /// </summary>
        CastSequence,

        /// <summary>
        /// 目标位置
        /// </summary>
        AimPos,

        /// <summary>
        /// 目标方向
        /// </summary>
        AimDir,

        /// <summary>
        /// 失败原因
        /// </summary>
        FailReason,

        // ========== Buff 相关 ==========
        /// <summary>
        /// Buff ID
        /// </summary>
        BuffId,

        /// <summary>
        /// Buff 叠加层数
        /// </summary>
        BuffStackCount,

        // ========== 子弹相关 ==========
        /// <summary>
        /// 子弹 ID
        /// </summary>
        ProjectileId,

        /// <summary>
        /// 子弹发射位置
        /// </summary>
        LaunchPosition,

        /// <summary>
        /// 子弹发射方向
        /// </summary>
        LaunchDirection,

        /// <summary>
        /// 子弹速度
        /// </summary>
        ProjectileSpeed,

        /// <summary>
        /// 子弹命中触发器ID
        /// </summary>
        HitTriggerPlanId,

        // ========== AOE 区域相关 ==========
        /// <summary>
        /// AOE 区域 ID
        /// </summary>
        AreaId,

        /// <summary>
        /// AOE 中心位置
        /// </summary>
        AreaCenter,

        /// <summary>
        /// AOE 半径
        /// </summary>
        AreaRadius,

        /// <summary>
        /// AOE 进入触发器ID
        /// </summary>
        AreaEnterTriggerPlanId,

        /// <summary>
        /// AOE 离开触发器ID
        /// </summary>
        AreaLeaveTriggerPlanId,

        // ========== 命中相关 ==========
        /// <summary>
        /// 命中位置
        /// </summary>
        HitPosition,

        /// <summary>
        /// 命中法线
        /// </summary>
        HitNormal,

        // ========== 时间轴相关 ==========
        /// <summary>
        /// 时间轴下一个事件索引
        /// </summary>
        TimelineNextEventIndex,

        // ========== 被动技能相关 ==========
        /// <summary>
        /// 被动技能 ID
        /// </summary>
        PassiveSkillId,

        /// <summary>
        /// 被动技能冷却结束时间
        /// </summary>
        PassiveCooldownEndTimeMs,
    }

    /// <summary>
    /// 上下文键字符串映射
    /// 提供枚举到字符串的映射
    /// </summary>
    public sealed class AbilityContextKeyStrings
    {
        private static readonly string[] _keys;

        static AbilityContextKeyStrings()
        {
            _keys = new string[Enum.GetValues(typeof(AbilityContextKeys)).Length];
            foreach (AbilityContextKeys key in Enum.GetValues(typeof(AbilityContextKeys)))
            {
                _keys[(int)key] = ConvertToKeyString(key);
            }
        }

        /// <summary>
        /// 获取键的字符串表示
        /// </summary>
        public static string GetKey(AbilityContextKeys key)
        {
            return _keys[(int)key];
        }

        private static string ConvertToKeyString(AbilityContextKeys key)
        {
            var name = key.ToString();
            var result = new System.Text.StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (char.IsUpper(c) && i > 0)
                {
                    result.Append('.');
                }
                result.Append(char.ToLower(c));
            }
            return result.ToString();
        }
    }

    /// <summary>
    /// AbilityContextKeys 枚举的扩展方法
    /// </summary>
    public static class AbilityContextKeysExtensions
    {
        /// <summary>
        /// 获取键的字符串表示
        /// </summary>
        public static string ToKeyString(this AbilityContextKeys key)
        {
            return AbilityContextKeyStrings.GetKey(key);
        }
    }
}
