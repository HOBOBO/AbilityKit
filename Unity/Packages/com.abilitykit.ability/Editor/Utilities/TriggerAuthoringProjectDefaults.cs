using System.Collections.Generic;
using AbilityKit.Ability.Config.Authoring;

namespace AbilityKit.Ability.Editor.Utilities
{
    internal static class TriggerAuthoringProjectDefaults
    {
        public static List<TriggerEventDefinitionData> CreateMobaEvents()
        {
            var events = new List<TriggerEventDefinitionData>
            {
                Prefix("skill.", "技能事件", "技能", "SkillCastContext", SkillFields(), false, true),
                Prefix("buff.", "增益效果事件", "增益效果", "BuffEventArgs", BuffFields(), false, true),
                Prefix("area.", "区域事件", "区域", "AreaEventArgs", AreaFields(), false, true),
                Prefix("projectile.", "投射物事件", "投射物", "ProjectileEventArgs", ProjectileFields(), false, true),
                Prefix("summon.", "召唤物事件", "召唤物", "SummonEventPayload", SummonFields(), false, true),
                Prefix("unit.", "单位事件", "单位", "UnitEventPayload", UnitFields(), false, true),
                Prefix("gameplay.", "玩法事件", "玩法", "GameplayLifecycleEventArgs", GameplayFields(), true, true),
                Prefix("presentation.", "表现事件", "表现", "PresentationEventArgs", PresentationFields(), true, false),
                Exact("damage.attack.created", "攻击已创建", "伤害", "AttackInfo", DamageFields(), false, true),
                Exact("damage.attack.before_calc", "伤害计算前", "伤害", "AttackInfo", DamageFields(), false, true),
                Exact("damage.calc.begin", "开始伤害计算", "伤害", "AttackCalcInfo", DamageFields(), false, true),
                Exact("damage.calc.after_base", "基础伤害计算后", "伤害", "AttackCalcInfo", DamageFields(), false, true),
                Exact("damage.calc.after_mitigate", "伤害减免后", "伤害", "AttackCalcInfo", DamageFields(), false, true),
                Exact("damage.calc.after_shield", "护盾结算后", "伤害", "AttackCalcInfo", DamageFields(), false, true),
                Exact("damage.calc.final", "最终伤害", "伤害", "AttackCalcInfo", DamageFields(), false, true),
                Exact("damage.apply.before", "应用伤害前", "伤害", "AttackCalcInfo", DamageFields(), false, true),
                Exact("damage.apply.after", "应用伤害后", "伤害", "DamageResult", DamageFields(), false, true),
                Exact("health.change.committed", "生命值已变化", "生命值", "MobaHealthChangeResult", DamageFields(), false, true),
                Exact("heal.apply.before", "应用治疗前", "治疗", "HealRequest", DamageFields(), false, true),
                Exact("heal.apply.after", "应用治疗后", "治疗", "HealResult", DamageFields(), false, true)
            };
            AddExactEvents(events, new[]
            {
                "skill.precast.start", "skill.precast.complete", "skill.precast.fail", "skill.precast.interrupt",
                "skill.cast.start", "skill.cast.complete", "skill.cast.fail", "skill.cast.interrupt"
            }, "技能", "SkillCastContext", SkillFields());
            AddExactEvents(events, new[]
            {
                "buff.apply", "buff.remove", "buff.interval", "buff.stack", "buff.refresh", "buff.tick",
                "buff.end", "buff.added", "buff.removed", "buff.stack_changed", "buff.effect_tick"
            }, "增益效果", "BuffEventArgs", BuffFields());
            AddExactEvents(events, new[] { "projectile.spawn", "projectile.tick", "projectile.hit", "projectile.exit" },
                "投射物", "ProjectileEventArgs", ProjectileFields());
            AddExactEvents(events, new[] { "area.spawn", "area.tick", "area.enter", "area.exit", "area.end" },
                "区域", "AreaEventArgs", AreaFields());
            AddExactEvents(events, new[] { "summon.spawn", "summon.despawn", "summon.die" },
                "召唤物", "SummonEventPayload", SummonFields());
            AddExactEvents(events, new[] { "unit.spawn", "unit.despawn", "unit.die", "unit.respawn" },
                "单位", "UnitEventPayload", UnitFields());
            AddExactEvents(events, new[] { "gameplay.started", "gameplay.tick", "gameplay.ended" },
                "玩法", "GameplayLifecycleEventArgs", GameplayFields(), true);
            return events;
        }

        private static void AddExactEvents(
            ICollection<TriggerEventDefinitionData> output,
            IEnumerable<string> ids,
            string category,
            string payloadType,
            List<TriggerPayloadFieldData> fields,
            bool allowExternal = false)
        {
            foreach (var id in ids)
            {
                output.Add(Exact(id, id, category, payloadType, new List<TriggerPayloadFieldData>(fields), allowExternal, true));
            }
        }

        public static List<TriggerGlobalBlackboardKeyData> CreateMobaBlackboardKeys()
        {
            return new List<TriggerGlobalBlackboardKeyData>
            {
                Key("skill.damagedTargets", "已受伤目标", TriggerValueType.IntegerList, "skill", true, true),
                Key("skill.hitCount", "命中次数", TriggerValueType.Integer, "skill", true, true),
                Key("skill.decayFactor", "伤害衰减系数", TriggerValueType.Number, "skill", true, true),
                Key("skill.loopGuards", "循环保护上下文", TriggerValueType.IntegerList, "skill", true, true)
            };
        }

        private static TriggerEventDefinitionData Exact(
            string id,
            string displayName,
            string category,
            string payloadType,
            List<TriggerPayloadFieldData> fields,
            bool allowExternal,
            bool deterministic)
        {
            return Event(id, TriggerEventMatchMode.Exact, displayName, category, payloadType, fields, allowExternal, deterministic);
        }

        private static TriggerEventDefinitionData Prefix(
            string id,
            string displayName,
            string category,
            string payloadType,
            List<TriggerPayloadFieldData> fields,
            bool allowExternal,
            bool deterministic)
        {
            return Event(id, TriggerEventMatchMode.Prefix, displayName, category, payloadType, fields, allowExternal, deterministic);
        }

        private static TriggerEventDefinitionData Event(
            string id,
            TriggerEventMatchMode matchMode,
            string displayName,
            string category,
            string payloadType,
            List<TriggerPayloadFieldData> fields,
            bool allowExternal,
            bool deterministic)
        {
            return new TriggerEventDefinitionData
            {
                Id = id,
                MatchMode = matchMode,
                DisplayName = displayName,
                Category = category,
                PayloadType = payloadType,
                PayloadFields = fields,
                AllowExternal = allowExternal,
                Deterministic = deterministic
            };
        }

        private static TriggerGlobalBlackboardKeyData Key(
            string key,
            string displayName,
            TriggerValueType type,
            string domain,
            bool canRead,
            bool canWrite)
        {
            return new TriggerGlobalBlackboardKeyData
            {
                Key = key,
                DisplayName = displayName,
                Type = type,
                Domain = domain,
                CanRead = canRead,
                CanWrite = canWrite,
                DefaultValue = new TriggerValueRefData { Source = TriggerValueSource.Constant, Type = type }
            };
        }

        private static TriggerPayloadFieldData Field(string path, TriggerValueType type)
        {
            return new TriggerPayloadFieldData { Path = path, DisplayName = path, Type = type };
        }

        private static List<TriggerPayloadFieldData> SkillFields()
        {
            return new List<TriggerPayloadFieldData>
            {
                Field("skill.id", TriggerValueType.Integer), Field("skill.slot", TriggerValueType.Integer),
                Field("skill.level", TriggerValueType.Integer), Field("skill.cost", TriggerValueType.Number),
                Field("skill.cooldown_ms", TriggerValueType.Number), Field("skill.cooldown_remaining_ms", TriggerValueType.Number),
                Field("caster.actor_id", TriggerValueType.Integer), Field("target.actor_id", TriggerValueType.Integer),
                Field("caster.mana", TriggerValueType.Number), Field("caster.mana.max", TriggerValueType.Number),
                Field("caster.mana.percent", TriggerValueType.Number), Field("aim.pos", TriggerValueType.Vector3),
                Field("aim.dir", TriggerValueType.Vector3), Field("fail.reason", TriggerValueType.String)
            };
        }

        private static List<TriggerPayloadFieldData> BuffFields()
        {
            return new List<TriggerPayloadFieldData>
            {
                Field("event_id", TriggerValueType.String), Field("source_actor_id", TriggerValueType.Integer),
                Field("target_actor_id", TriggerValueType.Integer), Field("buff_id", TriggerValueType.Integer),
                Field("effect_id", TriggerValueType.Integer), Field("stage", TriggerValueType.String),
                Field("stack_count", TriggerValueType.Integer), Field("duration_seconds", TriggerValueType.Number),
                Field("source_context_id", TriggerValueType.Integer)
            };
        }

        private static List<TriggerPayloadFieldData> AreaFields()
        {
            return new List<TriggerPayloadFieldData>
            {
                Field("area_id", TriggerValueType.Integer), Field("template_id", TriggerValueType.Integer),
                Field("owner_actor_id", TriggerValueType.Integer), Field("target_actor_id", TriggerValueType.Integer),
                Field("frame", TriggerValueType.Integer), Field("center", TriggerValueType.Vector3),
                Field("radius", TriggerValueType.Number), Field("max_targets", TriggerValueType.Integer)
            };
        }

        private static List<TriggerPayloadFieldData> ProjectileFields()
        {
            return new List<TriggerPayloadFieldData>
            {
                Field("source_actor_id", TriggerValueType.Integer), Field("target_actor_id", TriggerValueType.Integer),
                Field("projectile_template_id", TriggerValueType.Integer), Field("projectile_id", TriggerValueType.Integer),
                Field("frame", TriggerValueType.Integer), Field("position", TriggerValueType.Vector3),
                Field("direction", TriggerValueType.Vector3), Field("exit_reason", TriggerValueType.Integer)
            };
        }

        private static List<TriggerPayloadFieldData> SummonFields()
        {
            return new List<TriggerPayloadFieldData>
            {
                Field("summon_actor_id", TriggerValueType.Integer), Field("summon_id", TriggerValueType.Integer),
                Field("owner_actor_id", TriggerValueType.Integer), Field("root_owner_actor_id", TriggerValueType.Integer),
                Field("reason", TriggerValueType.Integer)
            };
        }

        private static List<TriggerPayloadFieldData> UnitFields()
        {
            return new List<TriggerPayloadFieldData>
            {
                Field("unit_actor_id", TriggerValueType.Integer), Field("team", TriggerValueType.Integer),
                Field("main_type", TriggerValueType.Integer), Field("unit_sub_type", TriggerValueType.Integer),
                Field("owner_player_id", TriggerValueType.Integer), Field("killer_actor_id", TriggerValueType.Integer)
            };
        }

        private static List<TriggerPayloadFieldData> GameplayFields()
        {
            return new List<TriggerPayloadFieldData>
            {
                Field("frame_index", TriggerValueType.Integer), Field("elapsed_seconds", TriggerValueType.Number),
                Field("delta_seconds", TriggerValueType.Number), Field("win_team_id", TriggerValueType.Integer)
            };
        }

        private static List<TriggerPayloadFieldData> PresentationFields()
        {
            return new List<TriggerPayloadFieldData>
            {
                Field("event_id", TriggerValueType.String), Field("source_actor_id", TriggerValueType.Integer),
                Field("target_actor_id", TriggerValueType.Integer), Field("presentation_id", TriggerValueType.Integer)
            };
        }

        private static List<TriggerPayloadFieldData> DamageFields()
        {
            return new List<TriggerPayloadFieldData>
            {
                Field("attacker_actor_id", TriggerValueType.Integer), Field("target_actor_id", TriggerValueType.Integer),
                Field("damage_value", TriggerValueType.Number), Field("target_hp", TriggerValueType.Number),
                Field("target_max_hp", TriggerValueType.Number), Field("damage_type", TriggerValueType.Integer),
                Field("crit_type", TriggerValueType.Integer), Field("reason_kind", TriggerValueType.Integer),
                Field("reason_param", TriggerValueType.Integer)
            };
        }
    }
}
