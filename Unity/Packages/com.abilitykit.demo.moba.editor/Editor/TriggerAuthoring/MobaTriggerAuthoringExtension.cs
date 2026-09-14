#if UNITY_EDITOR
using System;
using AbilityKit.Ability.Config.Authoring;
using AbilityKit.Ability.Editor.Utilities;

namespace AbilityKit.Demo.Moba.Editor.TriggerAuthoring
{
    public sealed class MobaTriggerAuthoringExtension : ITriggerAuthoringExtension
    {
        public const string ExtensionId = "abilitykit.demo.moba";

        public string Id => ExtensionId;

        public void Register(TriggerAuthoringExtensionContext context)
        {
            if (context.AcceptsEvents)
            {
                var scan = TriggerEventCatalogAssemblyScanner.ScanLoadedAssemblies();
                for (var i = 0; i < scan.Events.Count; i++)
                {
                    var definition = scan.Events[i];
                    if (definition?.Description?.IndexOf(
                            "MobaTriggerEventAttribute",
                            StringComparison.Ordinal) >= 0)
                        context.RegisterEvent(definition);
                }
            }
            if (context.AcceptsNodes)
            {
                RegisterConditions(context);
                RegisterActions(context);
            }
            if (context.AcceptsValueSources) RegisterSkillRuntimeValues(context);
        }

        private static void RegisterSkillRuntimeValues(TriggerAuthoringExtensionContext context)
        {
            RegisterSkillRuntimeValue(context, "cast", "showcase_combo", TriggerValueType.Number, "技能运行时/Cast/连击计数");
            RegisterSkillRuntimeValue(context, "effect", "showcase_snapshot_damage", TriggerValueType.Number, "技能运行时/Effect/伤害快照");
            RegisterSkillRuntimeValue(context, "target", "showcase_snapshot_damage", TriggerValueType.Number, "技能运行时/Target/逐目标伤害快照");
            RegisterSkillRuntimeValue(context, "child", "showcase_runtime_id", TriggerValueType.Integer, "技能运行时/Child/生成对象 ID");
            RegisterSkillRuntimeValue(context, "child", "showcase_result_count", TriggerValueType.Integer, "技能运行时/Child/生成数量");
        }

        private static void RegisterSkillRuntimeValue(
            TriggerAuthoringExtensionContext context,
            string scope,
            string key,
            TriggerValueType type,
            string displayName)
        {
            var fullKey = "skill_runtime." + scope + "." + key;
            context.RegisterValueSource(new TriggerAuthoringValueSourceDescriptor(
                "skill_runtime:" + scope + "." + key,
                type,
                displayName,
                "MOBA skill pipeline scoped Blackboard value.",
                fullKey,
                canWrite: true,
                blackboardName: "skill_runtime." + scope,
                blackboardKey: fullKey));
        }

        private static void RegisterConditions(TriggerAuthoringExtensionContext context)
        {
            context.RegisterCondition(Condition("has_buff", "拥有增益效果", "Condition/Combat",
                Required("buff_id", TriggerValueType.Integer),
                Optional("check_stack", TriggerValueType.Boolean),
                Choice("target_mode", false, Option(0, "目标"), Option(1, "来源")),
                ObjectParameter("options", false,
                    Optional("check_stack", TriggerValueType.Boolean),
                    Choice("target_mode", false, Option(0, "目标"), Option(1, "来源"))),
                ObjectParameter("target", false,
                    Choice("mode", false, Option(0, "目标"), Option(1, "来源")),
                    Choice("target_mode", false, Option(0, "目标"), Option(1, "来源")))),
                new HasBuffConditionCompiler());
            context.RegisterCondition(Condition("health_percent", "生命值百分比", "Condition/Combat",
                Required("threshold", TriggerValueType.Number),
                Choice("compare_type", false, Option(0, "小于"), Option(1, "大于"))),
                new HealthPercentConditionCompiler());
            context.RegisterCondition(Condition(
                    "owner_matches_payload_source", "所有者匹配事件来源", "Condition/Context"),
                new FunctionConditionCompiler("predicate:owner_matches_payload_source"));
            context.RegisterCondition(Condition(
                    "owner_matches_payload_target", "所有者匹配事件目标", "Condition/Context"),
                new FunctionConditionCompiler("predicate:owner_matches_payload_target"));
            context.RegisterCondition(Condition(
                    "target_is_flying_projectile", "目标是飞行投射物", "Condition/Context"),
                new FunctionConditionCompiler("predicate:target_is_flying_projectile"));
        }

        private sealed class FunctionConditionCompiler : ITriggerAuthoringConditionCompiler
        {
            private readonly string _functionKey;

            public FunctionConditionCompiler(string functionKey)
            {
                _functionKey = functionKey;
            }

            public void Compile(TriggerAuthoringConditionCompilerContext context)
            {
                context.EmitFunction(_functionKey);
            }
        }

        private sealed class HasBuffConditionCompiler : ITriggerAuthoringConditionCompiler
        {
            private static readonly string[] BuffIdAliases =
                { "buff_id", "buff.id", "buff.buff_id", "options.id", "options.buff_id" };
            private static readonly string[] CheckStackAliases =
                { "check_stack", "options.check_stack" };
            private static readonly string[] TargetModeAliases =
                { "target_mode", "options.target_mode", "target.mode", "target.target_mode" };

            public void Compile(TriggerAuthoringConditionCompilerContext context)
            {
                var buffId = context.CompileArgument(true, BuffIdAliases);
                var checkStack = context.CompileArgument(false, CheckStackAliases);
                var owner = false;
                if (context.HasArgument(TargetModeAliases))
                {
                    if (!context.TryGetConstantNumber(out var targetMode, TargetModeAliases))
                    {
                        context.ReportError(
                            "TRG2021",
                            ".arguments.target_mode",
                            "has_buff 的 target_mode 必须是常量，以便选择运行时谓词。");
                        return;
                    }
                    owner = targetMode != 0d;
                }
                if (buffId != null)
                    context.EmitFunction(owner ? "predicate:has_buff_owner" : "predicate:has_buff", buffId, checkStack);
            }
        }

        private sealed class HealthPercentConditionCompiler : ITriggerAuthoringConditionCompiler
        {
            public void Compile(TriggerAuthoringConditionCompilerContext context)
            {
                if (!context.TryGetConstantNumber(out var threshold, "threshold"))
                {
                    context.ReportError(
                        "TRG2022",
                        ".arguments.threshold",
                        "导出 Runtime Plan 时，health_percent 的 threshold 必须是数值常量。");
                    return;
                }

                var compareValue = 0d;
                if (context.HasArgument("compare_type") &&
                    !context.TryGetConstantNumber(out compareValue, "compare_type"))
                {
                    context.ReportError(
                        "TRG2023",
                        ".arguments.compare_type",
                        "health_percent 的 compare_type 必须是常量。");
                    return;
                }
                if (compareValue != 0d && compareValue != 1d)
                {
                    context.ReportError(
                        "TRG2024",
                        ".arguments.compare_type",
                        "health_percent 的 compare_type 必须为 0（小于）或 1（大于）。");
                    return;
                }

                context.EmitPayloadScaledComparison(
                    "target_hp",
                    "target_max_hp",
                    threshold / 100d,
                    compareValue == 0d ? "LessThan" : "GreaterThan");
            }
        }

        private static void RegisterActions(TriggerAuthoringExtensionContext context)
        {
            context.RegisterAction(Action(
                "query_target_collection",
                "查询目标集合",
                "Action/Targeting",
                WithTargets(Output("result", TriggerValueType.Integer))));
            context.RegisterAction(Action("give_damage", "造成伤害", "Action/Combat", WithTargets(WithMagnitude("damage_amount",
                OneOf("damage_amount", "damage_value", TriggerValueType.Number),
                OneOf("damage_amount", "source_attack_ratio", TriggerValueType.Number),
                DamageType("damage_type"),
                DamageReason("reason_kind"),
                Optional("reason_param", TriggerValueType.Integer),
                Choice("attribute_source", false, Option(0, "归属实体"), Option(1, "触发器所有者"))))));
            context.RegisterAction(Action("adjust_damage_number", "调整伤害数值", "Action/Combat",
                OneOf("damage_modifier", "value", TriggerValueType.Number),
                OneOf("damage_modifier", "repeat_target_decay_factor", TriggerValueType.Number),
                OneOf("damage_modifier", "target_missing_hp_ratio_coefficient", TriggerValueType.Number),
                Optional("number_slot", TriggerValueType.Integer),
                Optional("op", TriggerValueType.Integer),
                Optional("source_id", TriggerValueType.Integer),
                DamageReason("reason_kind"),
                Optional("reason_param", TriggerValueType.Integer),
                Optional("require_skill_runtime", TriggerValueType.Boolean),
                Optional("skip_first_hit", TriggerValueType.Boolean),
                Optional("target_hit_count_key_base", TriggerValueType.Integer)));
            context.RegisterAction(Action("take_damage", "承受伤害", "Action/Combat",
                Optional("rate", TriggerValueType.Number), Optional("reason_param", TriggerValueType.Integer)));
            context.RegisterAction(Action("heal", "治疗", "Action/Combat", WithTargets(WithMagnitude("heal_amount",
                OneOf("heal_amount", "amount", TriggerValueType.Number), DamageType("heal_type"),
                DamageReason("reason_kind"), Optional("reason_param", TriggerValueType.Integer)))));

            context.RegisterAction(Action("add_buff", "添加增益效果", "Action/Buff", WithTargets(
                Required("buff_ids", TriggerValueType.IntegerList))));
            context.RegisterAction(Action("remove_buff", "移除增益效果", "Action/Buff", WithTargets(
                Optional("buff_id", TriggerValueType.Integer),
                Optional("source_actor_id", TriggerValueType.Integer),
                Optional("remove_all", TriggerValueType.Boolean),
                Optional("remove_slow", TriggerValueType.Boolean),
                Optional("reason", TriggerValueType.Integer))));
            context.RegisterAction(Action("add_shield", "添加护盾", "Action/Shield", WithTargets(WithMagnitude("shield_amount",
                Optional("shield_id", TriggerValueType.Integer),
                OneOf("shield_amount", "shield_value", TriggerValueType.Number),
                Optional("absorb_ratio", TriggerValueType.Number),
                Optional("priority", TriggerValueType.Integer),
                Optional("damage_type_mask", TriggerValueType.Integer),
                Optional("duration_frames", TriggerValueType.Integer),
                Optional("duration_ms", TriggerValueType.Integer),
                Choice("stacking_policy", false,
                    Option(0, "独立叠加"), Option(1, "合并同护盾与来源"),
                    Option(2, "刷新同护盾与来源"), Option(3, "替换较低优先级")),
                Choice("consume_policy", false,
                    Option(0, "优先级后按最早"), Option(1, "优先级后按最新"),
                    Option(2, "最早优先"), Option(3, "最新优先")),
                OptionalOutput("result", TriggerValueType.Integer),
                OptionalOutput("result_count", TriggerValueType.Integer)))));
            context.RegisterAction(Action("remove_shield", "移除护盾", "Action/Shield", WithTargets(
                OneOf("shield_identity", "shield_id", TriggerValueType.Integer),
                OneOf("shield_identity", "instance_id", TriggerValueType.Integer),
                Optional("source_actor_id", TriggerValueType.Integer),
                Optional("remove_all", TriggerValueType.Boolean))));

            context.RegisterAction(Action("modify_resource", "修改资源", "Action/Resource", WithTargets(
                Required("amount", TriggerValueType.Number), ResourceType("resource_type"),
                Optional("min", TriggerValueType.Number), Optional("max", TriggerValueType.Number))));
            context.RegisterAction(Action("consume_resource", "消耗资源", "Action/Resource",
                Optional("amount", TriggerValueType.Number), ResourceType("resource_type")));
            context.RegisterAction(Action("convert_resource_to_heal", "将资源转为治疗", "Action/Resource", WithTargets(
                Required("amount", TriggerValueType.Number), ResourceType("resource_type"),
                Optional("heal_ratio", TriggerValueType.Number),
                Optional("out_of_combat_seconds", TriggerValueType.Number),
                DamageType("heal_type"), DamageReason("reason_kind"),
                Optional("reason_param", TriggerValueType.Integer))));

            context.RegisterAction(Action("shoot_projectile", "发射投射物", "Action/Projectile", WithTargets(
                Required("launcher_id", TriggerValueType.Integer),
                Required("projectile_id", TriggerValueType.Integer),
                Optional("continuous_process_id", TriggerValueType.Integer),
                Optional("track_target", TriggerValueType.Boolean),
                OptionalOutput("result", TriggerValueType.Integer),
                OptionalOutput("result_count", TriggerValueType.Integer))));
            context.RegisterAction(Action("remove_projectile", "移除投射物", "Action/Projectile"));
            context.RegisterAction(Action("spawn_summon", "生成召唤物", "Action/Summon",
                Required("summon_id", TriggerValueType.Integer),
                Optional("position_mode", TriggerValueType.Integer),
                Optional("rotation_mode", TriggerValueType.Integer),
                Optional("interval_ms", TriggerValueType.Number),
                Optional("duration_ms", TriggerValueType.Number),
                Optional("total_count", TriggerValueType.Integer),
                Optional("query_template_id", TriggerValueType.Integer),
                Optional("target_mode", TriggerValueType.Integer),
                OptionalOutput("result", TriggerValueType.Integer),
                OptionalOutput("result_count", TriggerValueType.Integer)));
            context.RegisterAction(Action("remove_summon", "移除召唤物", "Action/Summon", WithTargets(
                Optional("summon_id", TriggerValueType.Integer),
                Optional("summon_actor_id", TriggerValueType.Integer),
                Optional("root_owner_actor_id", TriggerValueType.Integer),
                Optional("remove_all", TriggerValueType.Boolean),
                Optional("reason", TriggerValueType.Integer))));
            context.RegisterAction(Action("spawn_area", "生成区域", "Action/Area", WithTargets(
                Required("area_id", TriggerValueType.Integer),
                Optional("position_mode", TriggerValueType.Integer), Optional("radius", TriggerValueType.Number),
                Optional("duration_frames", TriggerValueType.Integer), Optional("duration_ms", TriggerValueType.Integer),
                Optional("stay_interval_frames", TriggerValueType.Integer),
                Optional("collision_layer_mask", TriggerValueType.Integer),
                Optional("offset_x", TriggerValueType.Number), Optional("offset_y", TriggerValueType.Number),
                Optional("offset_z", TriggerValueType.Number),
                OptionalOutput("result", TriggerValueType.Integer),
                OptionalOutput("result_count", TriggerValueType.Integer))));
            context.RegisterAction(Action("remove_area", "移除区域", "Action/Area", WithTargets(
                OneOf("area_identity", "area_id", TriggerValueType.Integer),
                OneOf("area_identity", "template_id", TriggerValueType.Integer),
                OneOf("area_identity", "owner_actor_id", TriggerValueType.Integer),
                Optional("remove_all", TriggerValueType.Boolean))));
            context.RegisterAction(Action("cancel_skill", "取消技能", "Action/Skill", WithTargets(
                Choice("mode", false, Option(0, "自动"), Option(1, "全部"),
                    Option(2, "技能槽位"), Option(3, "技能 ID")),
                Optional("skill_id", TriggerValueType.Integer), Optional("skill_slot", TriggerValueType.Integer),
                Optional("remove_all", TriggerValueType.Boolean))));
            context.RegisterAction(Action("start_cooldown", "开始冷却", "Action/Skill",
                Optional("skill_id", TriggerValueType.Integer), Optional("skill_slot", TriggerValueType.Integer),
                Required("cooldown_ms", TriggerValueType.Integer)));
            context.RegisterAction(Action("reset_cooldown", "重置冷却", "Action/Skill", WithTargets(
                OneOf("skill_identity", "skill_id", TriggerValueType.Integer),
                OneOf("skill_identity", "skill_slot", TriggerValueType.Integer))));

            context.RegisterAction(Action("blink", "闪现", "Action/Motion",
                Optional("distance", TriggerValueType.Number), Optional("direction_mode", TriggerValueType.Integer),
                Optional("priority", TriggerValueType.Integer), Optional("apply_to_caster", TriggerValueType.Boolean),
                Optional("pass_through_walls", TriggerValueType.Boolean)));
            context.RegisterAction(Action("dash", "冲刺", "Action/Motion", WithContinuous(
                Optional("speed", TriggerValueType.Number), Optional("duration_ms", TriggerValueType.Number),
                Optional("direction_mode", TriggerValueType.Integer), Optional("priority", TriggerValueType.Integer),
                Optional("apply_to_caster", TriggerValueType.Boolean),
                Optional("hit_trigger_plan_id", TriggerValueType.Integer),
                Optional("motion_group_id", TriggerValueType.Integer),
                Optional("move_to_aim_position", TriggerValueType.Boolean),
                Optional("pass_through_walls", TriggerValueType.Boolean))));
            context.RegisterAction(Action("jump", "跳跃", "Action/Motion", WithContinuous(
                Optional("height", TriggerValueType.Number), Optional("duration_ms", TriggerValueType.Number),
                Optional("priority", TriggerValueType.Integer), Optional("apply_to_caster", TriggerValueType.Boolean),
                Optional("motion_group_id", TriggerValueType.Integer),
                Optional("landing_trigger_ids", TriggerValueType.IntegerList))));
            context.RegisterAction(Action("pull", "拉拽", "Action/Motion", WithTargets(WithContinuous(
                Optional("speed", TriggerValueType.Number), Optional("duration_ms", TriggerValueType.Number),
                Optional("direction_mode", TriggerValueType.Integer), Optional("target_distance", TriggerValueType.Number),
                Optional("priority", TriggerValueType.Integer), Optional("motion_group_id", TriggerValueType.Integer)))));

            context.RegisterAction(Action("play_presentation", "播放表现", "Action/Presentation",
                Required("template_id", TriggerValueType.Integer), Optional("target_mode", TriggerValueType.Integer),
                Optional("duration_ms", TriggerValueType.Integer), Optional("stop", TriggerValueType.Boolean),
                Optional("x", TriggerValueType.Number), Optional("y", TriggerValueType.Number),
                Optional("z", TriggerValueType.Number), Optional("scale", TriggerValueType.Number),
                Optional("radius", TriggerValueType.Number)));
            context.RegisterAction(Action("emit", "发送表现事件", "Action/Presentation",
                Required("emitter_id", TriggerValueType.Integer)));
            context.RegisterAction(Action("set_gameplay_var", "设置玩法变量", "Action/Gameplay",
                Required("key_id", TriggerValueType.Integer), Optional("value", TriggerValueType.Number)));
            context.RegisterAction(Action("add_gameplay_var", "增加玩法变量", "Action/Gameplay",
                Required("key_id", TriggerValueType.Integer), Optional("delta", TriggerValueType.Number)));
            context.RegisterAction(Action("advance_gameplay_counter", "推进玩法计数器", "Action/Gameplay",
                Required("key_id", TriggerValueType.Integer),
                Required("scope_payload_field_id", TriggerValueType.Integer),
                Required("threshold", TriggerValueType.Number), Optional("delta", TriggerValueType.Number),
                Optional("reset_value", TriggerValueType.Number), Required("trigger_id", TriggerValueType.Integer)));
            context.RegisterAction(Action("end_game", "结束游戏", "Action/Gameplay",
                Optional("reason_id", TriggerValueType.Integer), Optional("win_team_id", TriggerValueType.Integer)));
        }

        private static TriggerTypeDescriptor Condition(
            string type, string displayName, string category, params TriggerParameterDescriptor[] parameters)
        {
            return new TriggerTypeDescriptor(TriggerNodeKind.Condition, type, displayName, category, 0, 0, parameters);
        }

        private static TriggerTypeDescriptor Action(
            string type, string displayName, string category, params TriggerParameterDescriptor[] parameters)
        {
            return new TriggerTypeDescriptor(TriggerNodeKind.Action, type, displayName, category, 0, 0, true, parameters);
        }

        private static TriggerParameterDescriptor Required(string name, TriggerValueType type)
        {
            return new TriggerParameterDescriptor(name, type);
        }

        private static TriggerParameterDescriptor Optional(string name, TriggerValueType type)
        {
            return new TriggerParameterDescriptor(name, type, false);
        }

        private static TriggerParameterDescriptor Output(string name, TriggerValueType type)
        {
            const TriggerValueSourceMask variables =
                TriggerValueSourceMask.Context |
                TriggerValueSourceMask.LocalBlackboard |
                TriggerValueSourceMask.GlobalBlackboard;
            return new TriggerParameterDescriptor(
                name,
                type,
                true,
                variables,
                TriggerParameterAccess.Output);
        }

        private static TriggerParameterDescriptor OptionalOutput(string name, TriggerValueType type)
        {
            const TriggerValueSourceMask variables =
                TriggerValueSourceMask.Context |
                TriggerValueSourceMask.LocalBlackboard |
                TriggerValueSourceMask.GlobalBlackboard;
            return new TriggerParameterDescriptor(
                name,
                type,
                false,
                variables,
                TriggerParameterAccess.Output);
        }

        private static TriggerParameterDescriptor OneOf(string group, string name, TriggerValueType type)
        {
            return new TriggerParameterDescriptor(
                name, type, false, TriggerValueSourceMask.All, TriggerParameterAccess.Read, group);
        }

        private static TriggerParameterDescriptor ObjectParameter(
            string name, bool required, params TriggerParameterDescriptor[] fields)
        {
            return new TriggerParameterDescriptor(
                name, TriggerValueType.Object, required, TriggerValueSourceMask.All,
                TriggerParameterAccess.Read, null, fields);
        }

        private static TriggerParameterDescriptor Choice(
            string name, bool required, params TriggerParameterOption[] options)
        {
            return new TriggerParameterDescriptor(
                name, TriggerValueType.Integer, required, TriggerValueSourceMask.All,
                TriggerParameterAccess.Read, null, options);
        }

        private static TriggerParameterOption Option(long value, string displayName)
        {
            return new TriggerParameterOption(value, displayName);
        }

        private static TriggerParameterDescriptor DamageType(string name)
        {
            return Choice(name, false,
                Option(0, "无"), Option(1, "物理"), Option(2, "魔法"), Option(4, "真实"));
        }

        private static TriggerParameterDescriptor DamageReason(string name)
        {
            return Choice(name, false,
                Option(0, "无"), Option(1, "技能"), Option(2, "普通攻击"),
                Option(3, "增益效果"), Option(4, "道具"), Option(5, "环境"));
        }

        private static TriggerParameterDescriptor ResourceType(string name)
        {
            return Choice(name, false,
                Option(0, "无"), Option(1, "生命值"), Option(2, "法力"), Option(3, "怒气"),
                Option(4, "能量"), Option(5, "弹药"), Option(6, "连击点"));
        }

        private static TriggerParameterDescriptor[] WithTargets(params TriggerParameterDescriptor[] parameters)
        {
            return Append(parameters, new[]
            {
                ObjectParameter("target", false,
                    Optional("query_template_id", TriggerValueType.Integer),
                    Optional("actor_id", TriggerValueType.Integer),
                    Optional("payload_field_id", TriggerValueType.Integer),
                    Choice("source", false,
                        Option(3, "上下文目标"), Option(4, "自身"), Option(2, "指定实体"),
                        Option(1, "全部实体"), Option(5, "同队实体"), Option(6, "敌方实体"),
                        Option(7, "主类型"), Option(8, "单位子类型"), Option(1000, "查询模板")),
                    Optional("source_param", TriggerValueType.Integer),
                    Choice("filter", false,
                        Option(0, "无"), Option(0x0204, "要求有效 ID"), Option(0x0205, "要求位置信息"),
                        Option(0x0101, "圆形范围"), Option(0x0102, "扇形范围"),
                        Option(0x0301, "排除施法者"), Option(0x0302, "排除上下文目标"),
                        Option(0x0201, "白名单"), Option(0x0202, "黑名单")),
                    Optional("filter_param", TriggerValueType.Integer), Optional("radius", TriggerValueType.Number),
                    Optional("half_angle_deg", TriggerValueType.Number),
                    Choice("order", false,
                        Option(0, "无"), Option(0x2001, "固定为零"), Option(0x2002, "随机"),
                        Option(0x2004, "距施法者距离"), Option(0x2005, "距上下文目标距离")),
                    Optional("order_param", TriggerValueType.Integer),
                    Choice("select", false, Option(0x1001, "前 K 个"), Option(0x1002, "流式选择前 K 个")),
                    Optional("max_count", TriggerValueType.Integer), Optional("self", TriggerValueType.Boolean)),
                Optional("query_template_id", TriggerValueType.Integer),
                Optional("target_actor_id", TriggerValueType.Integer),
                Optional("target_payload_field_id", TriggerValueType.Integer),
                Choice("target_source", false,
                    Option(3, "上下文目标"), Option(4, "自身"), Option(2, "指定实体"),
                    Option(1, "全部实体"), Option(5, "同队实体"), Option(6, "敌方实体"),
                    Option(7, "主类型"), Option(8, "单位子类型"), Option(1000, "查询模板")),
                Optional("target_source_param", TriggerValueType.Integer),
                Choice("target_filter", false,
                    Option(0, "无"), Option(0x0204, "要求有效 ID"), Option(0x0205, "要求位置信息"),
                    Option(0x0101, "圆形范围"), Option(0x0102, "扇形范围"),
                    Option(0x0301, "排除施法者"), Option(0x0302, "排除上下文目标"),
                    Option(0x0201, "白名单"), Option(0x0202, "黑名单")),
                Optional("target_filter_param", TriggerValueType.Integer),
                Optional("target_radius", TriggerValueType.Number),
                Optional("target_half_angle_deg", TriggerValueType.Number),
                Choice("target_order", false,
                    Option(0, "无"), Option(0x2001, "固定为零"), Option(0x2002, "随机"),
                    Option(0x2004, "距施法者距离"), Option(0x2005, "距上下文目标距离")),
                Optional("target_order_param", TriggerValueType.Integer),
                Choice("target_select", false,
                    Option(0x1001, "前 K 个"), Option(0x1002, "流式选择前 K 个")),
                Optional("target_max_count", TriggerValueType.Integer), Optional("target_self", TriggerValueType.Boolean)
            });
        }

        private static TriggerParameterDescriptor[] WithContinuous(params TriggerParameterDescriptor[] parameters)
        {
            return Append(parameters, new[]
            {
                Optional("continuous_process_id", TriggerValueType.Integer),
                Optional("continuous_tag_template_id", TriggerValueType.Integer),
                Optional("trigger_ids", TriggerValueType.IntegerList),
                Optional("interval_ms", TriggerValueType.Integer),
                Optional("interval_trigger_ids", TriggerValueType.IntegerList)
            });
        }

        private static TriggerParameterDescriptor[] WithMagnitude(
            string amountGroup,
            params TriggerParameterDescriptor[] parameters)
        {
            return Append(parameters, new[]
            {
                OneOf(amountGroup, "magnitude_value", TriggerValueType.Number),
                Choice("magnitude_type", false,
                    Option(0, "固定值"), Option(1, "等级曲线"), Option(2, "属性"),
                    Option(3, "时间衰减"), Option(5, "上下文数值")),
                Optional("magnitude_coefficient", TriggerValueType.Number),
                Optional("magnitude_attribute", TriggerValueType.Integer),
                Optional("magnitude_duration", TriggerValueType.Number),
                Choice("magnitude_decay", false,
                    Option(0, "线性"), Option(1, "指数"), Option(2, "对数"),
                    Option(3, "缓出"), Option(4, "缓入"), Option(5, "缓入缓出")),
                Optional("magnitude_context_key", TriggerValueType.String),
                Choice("magnitude_secondary_type", false,
                    Option(0, "固定值"), Option(1, "等级曲线"), Option(2, "属性"),
                    Option(3, "时间衰减"), Option(5, "上下文数值")),
                Optional("magnitude_secondary_value", TriggerValueType.Number),
                Optional("magnitude_secondary_coefficient", TriggerValueType.Number),
                Optional("magnitude_secondary_attribute", TriggerValueType.Integer),
                Optional("magnitude_secondary_duration", TriggerValueType.Number),
                Choice("magnitude_secondary_decay", false,
                    Option(0, "线性"), Option(1, "指数"), Option(2, "对数"),
                    Option(3, "缓出"), Option(4, "缓入"), Option(5, "缓入缓出")),
                Optional("magnitude_secondary_context_key", TriggerValueType.String),
                Choice("magnitude_combine", false,
                    Option(0, "相加"), Option(1, "相乘"), Option(2, "覆盖")),
                Choice("magnitude_source_role", false,
                    Option(0, "归因实体"), Option(1, "技能施法者"), Option(2, "所有者"),
                    Option(3, "根所有者"), Option(4, "目标"), Option(5, "自身"), Option(6, "子实体")),
                Choice("magnitude_evaluation", false, Option(0, "实时"), Option(1, "快照")),
                OptionalOutput("magnitude_capture", TriggerValueType.Number)
            });
        }

        private static TriggerParameterDescriptor[] Append(
            TriggerParameterDescriptor[] first, TriggerParameterDescriptor[] second)
        {
            var result = new TriggerParameterDescriptor[first.Length + second.Length];
            Array.Copy(first, result, first.Length);
            Array.Copy(second, 0, result, first.Length, second.Length);
            return result;
        }
    }
}
#endif
