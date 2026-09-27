using System;
using System.Text;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// 统一的 MOBA 运行时上下文键。
    /// 这些键仅用于跨模块共享数据袋，不作为强类型事件参数的替代品。
    /// </summary>
    public enum AbilityContextKeys
    {
        // 溯源与执行环境
        SourceContextId,
        ContextKind,
        TriggerId,
        OwnerKey,
        SourceConfigId,
        Stage,
        Phase,
        Frame,

        // 参与者
        SourceActorId,
        TargetActorId,

        // 技能运行时
        SkillRuntimeId,
        SkillRuntimeHandle,
        SkillId,
        SkillSlot,
        SkillLevel,
        CastSequence,
        AimPos,
        AimDir,
        FailReason,

        // Buff 运行时
        BuffId,
        BuffStackCount,

        // 投射物运行时
        ProjectileId,
        LaunchPosition,
        LaunchDirection,
        ProjectileSpeed,
        HitTriggerPlanId,

        // 区域运行时
        AreaId,
        AreaCenter,
        AreaRadius,
        AreaEnterTriggerPlanId,
        AreaLeaveTriggerPlanId,

        // 命中信息
        HitPosition,
        HitNormal,

        // 时间轴运行时
        TimelineNextEventIndex,

        // 被动技能运行时
        PassiveSkillId,
        PassiveCooldownEndTimeMs,
    }

    /// <summary>
    /// MOBA 上下文键字符串映射。
    /// </summary>
    public static class MobaContextKeyStrings
    {
        private static readonly string[] Keys;

        static MobaContextKeyStrings()
        {
            Keys = new string[Enum.GetValues(typeof(AbilityContextKeys)).Length];
            foreach (AbilityContextKeys key in Enum.GetValues(typeof(AbilityContextKeys)))
            {
                Keys[(int)key] = ConvertToKeyString(key);
            }
        }

        public static string GetKey(AbilityContextKeys key)
        {
            return Keys[(int)key];
        }

        private static string ConvertToKeyString(AbilityContextKeys key)
        {
            var name = key.ToString();
            var result = new StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                var c = name[i];
                if (char.IsUpper(c) && i > 0)
                {
                    result.Append('.');
                }

                result.Append(char.ToLower(c));
            }

            return result.ToString();
        }
    }

    public static class AbilityContextKeysExtensions
    {
        public static string ToKeyString(this AbilityContextKeys key)
        {
            return MobaContextKeyStrings.GetKey(key);
        }
    }
}
