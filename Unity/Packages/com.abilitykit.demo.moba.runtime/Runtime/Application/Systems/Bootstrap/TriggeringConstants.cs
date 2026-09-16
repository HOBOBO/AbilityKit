using System;
using System.Collections.Generic;
using AbilityKit.Core.Logging;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Eventing;

namespace AbilityKit.Demo.Moba.Systems
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
            public const string AdjustDamageNumber = "adjust_damage_number";
            public const string TakeDamage = "take_damage";
            public const string DebugLog = "debug_log";
            public const string ShootProjectile = "shoot_projectile";
            public const string AddBuff = "add_buff";
            public const string RemoveBuff = "remove_buff";
            public const string CancelSkill = "cancel_skill";
            public const string AddShield = "add_shield";
            public const string RemoveShield = "remove_shield";
            public const string RemoveSummon = "remove_summon";
            public const string RemoveProjectile = "remove_projectile";
            public const string RemoveArea = "remove_area";
            public const string SpawnArea = "spawn_area";
            public const string PlayEffect = "play_effect";
            public const string PlaySound = "play_sound";
            public const string Heal = "heal";
            public const string Summon = "summon";
            public const string SpawnSummon = "spawn_summon";
            public const string PlayPresentation = "play_presentation";
            public const string Emit = "emit";
            public const string EndGame = "end_game";
            public const string SetGameplayVar = "set_gameplay_var";
            public const string AddGameplayVar = "add_gameplay_var";
            public const string AdvanceGameplayCounter = "advance_gameplay_counter";
            public const string ExecuteTrigger = "execute_trigger";
            public const string QueryTargetCollection = "query_target_collection";

            // 位移类 Action。
            public const string Dash = "dash";
            public const string Blink = "blink";
            public const string Pull = "pull";
            public const string Jump = "jump";

            // 资源类 Action。
            public const string ConsumeResource = "consume_resource";
            public const string ModifyResource = "modify_resource";
            public const string ConvertResourceToHeal = "convert_resource_to_heal";
            public const string StartCooldown = "start_cooldown";
            public const string ResetCooldown = "reset_cooldown";
            public const string AddSkillParamModifier = "add_skill_param_modifier";
            public const string RemoveSkillParamModifiers = "remove_skill_param_modifiers";
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
        private static readonly object _actionIdCacheLock = new object();

        /// <summary>
        /// 缓存的Event ID
        /// </summary>
        private static readonly Dictionary<string, int> _eventIdCache = new(StringComparer.Ordinal);
        private static readonly object _eventIdCacheLock = new object();

        /// <summary>
        /// 获取Action ID（带缓存）
        /// </summary>
        public static ActionId GetActionId(string actionName)
        {
            if (string.IsNullOrEmpty(actionName))
                return default;

            lock (_actionIdCacheLock)
            {
                if (!_actionIdCache.TryGetValue(actionName, out var id))
                {
                    id = new ActionId(StableStringId.Get(ActionPrefix + actionName));
                    _actionIdCache[actionName] = id;
                }

                return id;
            }
        }

        /// <summary>
        /// 获取Event ID（带缓存）
        /// </summary>
        public static int GetEventId(string eventName)
        {
            if (string.IsNullOrEmpty(eventName))
                return 0;

            lock (_eventIdCacheLock)
            {
                if (!_eventIdCache.TryGetValue(eventName, out var id))
                {
                    id = StableStringId.Get(EventPrefix + eventName);
                    _eventIdCache[eventName] = id;
                }

                return id;
            }
        }

        /// <summary>
        /// 获取预定义的Action ID
        /// </summary>
        public static ActionId GiveDamageId => GetActionId(Actions.GiveDamage);
        public static ActionId TakeDamageId => GetActionId(Actions.TakeDamage);
        public static ActionId HealId => GetActionId(Actions.Heal);
        public static ActionId DebugLogId => GetActionId(Actions.DebugLog);
        public static ActionId ShootProjectileId => GetActionId(Actions.ShootProjectile);
        public static ActionId AddBuffId => GetActionId(Actions.AddBuff);
        public static ActionId RemoveBuffId => GetActionId(Actions.RemoveBuff);
        public static ActionId CancelSkillId => GetActionId(Actions.CancelSkill);
        public static ActionId AddShieldId => GetActionId(Actions.AddShield);
        public static ActionId RemoveShieldId => GetActionId(Actions.RemoveShield);
        public static ActionId RemoveSummonId => GetActionId(Actions.RemoveSummon);
        public static ActionId RemoveProjectileId => GetActionId(Actions.RemoveProjectile);
        public static ActionId RemoveAreaId => GetActionId(Actions.RemoveArea);
        public static ActionId SpawnAreaId => GetActionId(Actions.SpawnArea);
        public static ActionId SpawnSummonId => GetActionId(Actions.SpawnSummon);
        public static ActionId PlayPresentationId => GetActionId(Actions.PlayPresentation);
        public static ActionId EmitId => GetActionId(Actions.Emit);
        public static ActionId EndGameId => GetActionId(Actions.EndGame);
        public static ActionId SetGameplayVarId => GetActionId(Actions.SetGameplayVar);
        public static ActionId AddGameplayVarId => GetActionId(Actions.AddGameplayVar);
        public static ActionId AdvanceGameplayCounterId => GetActionId(Actions.AdvanceGameplayCounter);
        public static ActionId ExecuteTriggerId => GetActionId(Actions.ExecuteTrigger);
        public static ActionId QueryTargetCollectionId => GetActionId(Actions.QueryTargetCollection);

        // 位移类 Action ID。
        public static ActionId DashId => GetActionId(Actions.Dash);
        public static ActionId BlinkId => GetActionId(Actions.Blink);
        public static ActionId PullId => GetActionId(Actions.Pull);
        public static ActionId JumpId => GetActionId(Actions.Jump);

        // 资源类 Action ID。
        public static ActionId ConsumeResourceId => GetActionId(Actions.ConsumeResource);
        public static ActionId ModifyResourceId => GetActionId(Actions.ModifyResource);
        public static ActionId ConvertResourceToHealId => GetActionId(Actions.ConvertResourceToHeal);
        public static ActionId StartCooldownId => GetActionId(Actions.StartCooldown);
        public static ActionId ResetCooldownId => GetActionId(Actions.ResetCooldown);
        public static ActionId AddSkillParamModifierId => GetActionId(Actions.AddSkillParamModifier);
        public static ActionId RemoveSkillParamModifiersId => GetActionId(Actions.RemoveSkillParamModifiers);

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
            lock (_actionIdCacheLock)
            {
                _actionIdCache.Clear();
            }

            lock (_eventIdCacheLock)
            {
                _eventIdCache.Clear();
            }
        }
    }
}
