using System;
using System.Collections.Generic;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Eventing;

namespace AbilityKit.Ability.Impl.Moba.Systems
{
    /// <summary>
    /// 统一的触发器ID生成常量类
    /// 集中管理所有Action和Event的ID前缀，避免分散在多处
    /// </summary>
    public static class TriggeringConstants
    {
        /// <summary>
        /// Action ID 前缀
        /// </summary>
        public const string ActionPrefix = "action:";

        /// <summary>
        /// Event ID 前缀
        /// </summary>
        public const string EventPrefix = "event:";

        /// <summary>
        /// 预定义的Action名称
        /// </summary>
        public static class Actions
        {
            public const string GiveDamage = "give_damage";
            public const string TakeDamage = "take_damage";
            public const string DebugLog = "debug_log";
            public const string ShootProjectile = "shoot_projectile";
            public const string AddBuff = "add_buff";
            public const string RemoveBuff = "remove_buff";
            public const string PlayEffect = "play_effect";
            public const string PlaySound = "play_sound";
            public const string Heal = "heal";
            public const string Summon = "summon";
        }

        /// <summary>
        /// 预定义的Event名称
        /// </summary>
        public static class Events
        {
            public const string OnDamage = "on_damage";
            public const string OnKill = "on_kill";
            public const string OnDeath = "on_death";
            public const string OnBuffAdded = "on_buff_added";
            public const string OnBuffRemoved = "on_buff_removed";
            public const string OnSkillCast = "on_skill_cast";
            public const string OnSkillHit = "on_skill_hit";
        }

        /// <summary>
        /// 缓存的Action ID
        /// </summary>
        private static readonly Dictionary<string, ActionId> _actionIdCache = new(StringComparer.Ordinal);

        /// <summary>
        /// 缓存的Event ID
        /// </summary>
        private static readonly Dictionary<string, int> _eventIdCache = new(StringComparer.Ordinal);

        /// <summary>
        /// 获取Action ID（带缓存）
        /// </summary>
        public static ActionId GetActionId(string actionName)
        {
            if (string.IsNullOrEmpty(actionName))
                return default;

            if (!_actionIdCache.TryGetValue(actionName, out var id))
            {
                id = new ActionId(StableStringId.Get(ActionPrefix + actionName));
                _actionIdCache[actionName] = id;
            }
            return id;
        }

        /// <summary>
        /// 获取Event ID（带缓存）
        /// </summary>
        public static int GetEventId(string eventName)
        {
            if (string.IsNullOrEmpty(eventName))
                return 0;

            if (!_eventIdCache.TryGetValue(eventName, out var id))
            {
                id = StableStringId.Get(EventPrefix + eventName);
                _eventIdCache[eventName] = id;
            }
            return id;
        }

        /// <summary>
        /// 获取预定义的Action ID
        /// </summary>
        public static ActionId GiveDamageId => GetActionId(Actions.GiveDamage);
        public static ActionId TakeDamageId => GetActionId(Actions.TakeDamage);
        public static ActionId DebugLogId => GetActionId(Actions.DebugLog);
        public static ActionId ShootProjectileId => GetActionId(Actions.ShootProjectile);
        public static ActionId AddBuffId => GetActionId(Actions.AddBuff);

        /// <summary>
        /// 获取预定义的Event ID
        /// </summary>
        public static int OnDamageId => GetEventId(Events.OnDamage);
        public static int OnKillId => GetEventId(Events.OnKill);
        public static int OnDeathId => GetEventId(Events.OnDeath);
        public static int OnBuffAddedId => GetEventId(Events.OnBuffAdded);

        /// <summary>
        /// 清理缓存（通常在测试时使用）
        /// </summary>
        public static void ClearCache()
        {
            _actionIdCache.Clear();
            _eventIdCache.Clear();
        }
    }
}
