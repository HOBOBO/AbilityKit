using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config.Authoring;

namespace AbilityKit.Ability.Editor.Utilities
{
    public sealed class TriggerParameterOption
    {
        public TriggerParameterOption(long value, string displayName)
        {
            Value = value;
            DisplayName = displayName ?? value.ToString();
        }

        public long Value { get; }
        public string DisplayName { get; }
    }

    [Flags]
    public enum TriggerValueSourceMask
    {
        None = 0,
        Constant = 1 << 0,
        Payload = 1 << 1,
        Context = 1 << 2,
        LocalBlackboard = 1 << 3,
        GlobalBlackboard = 1 << 4,
        TemplateParameter = 1 << 5,
        Expression = 1 << 6,
        All = Constant | Payload | Context | LocalBlackboard | GlobalBlackboard | TemplateParameter | Expression
    }

    [Flags]
    public enum TriggerParameterSemanticPolicy
    {
        None = 0,
        ValidateConstant = 1 << 0
    }

    public sealed class TriggerParameterDescriptor
    {
        public TriggerParameterDescriptor(
            string name,
            TriggerValueType type,
            bool required = true,
            TriggerValueSourceMask allowedSources = TriggerValueSourceMask.All,
            TriggerParameterAccess access = TriggerParameterAccess.Read,
            string requiredGroup = null,
            params TriggerParameterOption[] options)
            : this(
                name,
                type,
                required,
                allowedSources,
                access,
                requiredGroup,
                (IReadOnlyList<TriggerParameterDescriptor>)null,
                options)
        {
        }

        public TriggerParameterDescriptor(
            string name,
            TriggerValueType type,
            string semanticId,
            bool required = true,
            TriggerValueSourceMask allowedSources = TriggerValueSourceMask.All,
            TriggerParameterAccess access = TriggerParameterAccess.Read,
            string requiredGroup = null,
            TriggerParameterSemanticPolicy semanticPolicy = TriggerParameterSemanticPolicy.ValidateConstant)
            : this(
                name,
                type,
                required,
                allowedSources,
                access,
                requiredGroup,
                null,
                semanticId,
                semanticPolicy,
                null)
        {
        }

        public TriggerParameterDescriptor(
            string name,
            TriggerValueType type,
            bool required,
            TriggerValueSourceMask allowedSources,
            TriggerParameterAccess access,
            string requiredGroup,
            IReadOnlyList<TriggerParameterDescriptor> fields,
            params TriggerParameterOption[] options)
            : this(
                name,
                type,
                required,
                allowedSources,
                access,
                requiredGroup,
                fields,
                null,
                TriggerParameterSemanticPolicy.None,
                options)
        {
        }

        private TriggerParameterDescriptor(
            string name,
            TriggerValueType type,
            bool required,
            TriggerValueSourceMask allowedSources,
            TriggerParameterAccess access,
            string requiredGroup,
            IReadOnlyList<TriggerParameterDescriptor> fields,
            string semanticId,
            TriggerParameterSemanticPolicy semanticPolicy,
            TriggerParameterOption[] options)
        {
            Name = name ?? string.Empty;
            Type = type;
            Required = required;
            if (access == TriggerParameterAccess.Output)
            {
                const TriggerValueSourceMask writableBoards =
                    TriggerValueSourceMask.Context |
                    TriggerValueSourceMask.LocalBlackboard |
                    TriggerValueSourceMask.GlobalBlackboard;
                var outputSources = allowedSources & writableBoards;
                AllowedSources = outputSources != TriggerValueSourceMask.None
                    ? outputSources
                    : writableBoards;
            }
            else
            {
                AllowedSources = allowedSources;
            }
            Access = access;
            RequiredGroup = requiredGroup ?? string.Empty;
            Fields = fields ?? Array.Empty<TriggerParameterDescriptor>();
            Options = options ?? Array.Empty<TriggerParameterOption>();
            SemanticId = semanticId?.Trim() ?? string.Empty;
            SemanticPolicy = string.IsNullOrEmpty(SemanticId)
                ? TriggerParameterSemanticPolicy.None
                : semanticPolicy;
        }

        public string Name { get; }
        public TriggerValueType Type { get; }
        public bool Required { get; }
        public TriggerValueSourceMask AllowedSources { get; }
        public TriggerParameterAccess Access { get; }
        public string RequiredGroup { get; }
        public IReadOnlyList<TriggerParameterDescriptor> Fields { get; }
        public IReadOnlyList<TriggerParameterOption> Options { get; }
        public string SemanticId { get; }
        public TriggerParameterSemanticPolicy SemanticPolicy { get; }
    }

    public enum TriggerParameterAccess
    {
        Read = 0,
        Write = 1,
        // Outputs are Blackboard bindings populated by the action implementation.
        Output = 2
    }

    public static class TriggerParameterAccessRules
    {
        public static bool IsWrite(TriggerParameterAccess access)
        {
            return access == TriggerParameterAccess.Write || access == TriggerParameterAccess.Output;
        }
    }

    public sealed class TriggerTypeDescriptor
    {
        public TriggerTypeDescriptor(
            TriggerNodeKind kind,
            string type,
            string displayName,
            string category,
            int minChildren = 0,
            int maxChildren = 0,
            params TriggerParameterDescriptor[] parameters)
            : this(kind, type, displayName, category, minChildren, maxChildren, false, parameters)
        {
        }

        public TriggerTypeDescriptor(
            TriggerNodeKind kind,
            string type,
            string displayName,
            string category,
            int minChildren,
            int maxChildren,
            bool runtimeSupported,
            params TriggerParameterDescriptor[] parameters)
        {
            Kind = kind;
            Type = type ?? string.Empty;
            DisplayName = displayName ?? Type;
            Category = category ?? string.Empty;
            MinChildren = minChildren;
            MaxChildren = maxChildren;
            RuntimeSupported = runtimeSupported;
            Parameters = parameters ?? Array.Empty<TriggerParameterDescriptor>();
        }

        public TriggerNodeKind Kind { get; }
        public string Type { get; }
        public string DisplayName { get; }
        public string Category { get; }
        public int MinChildren { get; }
        public int MaxChildren { get; }
        public bool RuntimeSupported { get; }
        public IReadOnlyList<TriggerParameterDescriptor> Parameters { get; }
    }

    public sealed class TriggerTypeDescriptorCatalog
    {
        private readonly Dictionary<string, TriggerTypeDescriptor> _entries =
            new Dictionary<string, TriggerTypeDescriptor>(StringComparer.Ordinal);
        private readonly Dictionary<string, ITriggerAuthoringConditionCompiler> _conditionCompilers =
            new Dictionary<string, ITriggerAuthoringConditionCompiler>(StringComparer.Ordinal);

        public void Register(TriggerTypeDescriptor descriptor)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            if (string.IsNullOrWhiteSpace(descriptor.Type))
                throw new ArgumentException("描述符必须指定节点类型。", nameof(descriptor));

            var key = BuildKey(descriptor.Kind, descriptor.Type);
            _entries[key] = descriptor;
            if (descriptor.Kind == TriggerNodeKind.Condition) _conditionCompilers.Remove(key);
        }

        public bool TryGet(TriggerNodeKind kind, string type, out TriggerTypeDescriptor descriptor)
        {
            return _entries.TryGetValue(BuildKey(kind, type), out descriptor);
        }

        public List<TriggerTypeDescriptor> GetAll(TriggerNodeKind kind)
        {
            var result = new List<TriggerTypeDescriptor>();
            foreach (var entry in _entries.Values)
            {
                if (entry.Kind == kind) result.Add(entry);
            }
            result.Sort((left, right) =>
            {
                var category = string.Compare(left.Category, right.Category, StringComparison.Ordinal);
                return category != 0
                    ? category
                    : string.Compare(left.DisplayName, right.DisplayName, StringComparison.Ordinal);
            });
            return result;
        }

        internal void RegisterConditionCompiler(
            string type,
            ITriggerAuthoringConditionCompiler compiler)
        {
            if (string.IsNullOrWhiteSpace(type)) throw new ArgumentException("必须指定条件类型。", nameof(type));
            if (compiler == null) throw new ArgumentNullException(nameof(compiler));
            _conditionCompilers[BuildKey(TriggerNodeKind.Condition, type)] = compiler;
        }

        internal bool TryGetConditionCompiler(
            string type,
            out ITriggerAuthoringConditionCompiler compiler)
        {
            return _conditionCompilers.TryGetValue(
                BuildKey(TriggerNodeKind.Condition, type),
                out compiler);
        }

        public static TriggerTypeDescriptorCatalog CreateProjectDefaults()
        {
            var catalog = new TriggerTypeDescriptorCatalog();
            RegisterCompleteProjectTypes(catalog);
            // Compatibility for standalone validation callers that predate project-scoped extensions.
            RegisterLegacyMobaConditions(catalog);
            RegisterCombatActions(catalog);
            RegisterBuffAndShieldActions(catalog);
            RegisterResourceActions(catalog);
            RegisterSpawnAndSkillActions(catalog);
            RegisterMotionActions(catalog);
            RegisterPresentationAndGameplayActions(catalog);
            return catalog;
        }

        public static TriggerTypeDescriptorCatalog CreateForProject(TriggerAuthoringProjectAsset project)
        {
            var catalog = new TriggerTypeDescriptorCatalog();
            RegisterCompleteProjectTypes(catalog);
            TriggerAuthoringExtensionRegistry.ApplyTypes(project, catalog);
            return catalog;
        }

        private static void RegisterCompleteProjectTypes(TriggerTypeDescriptorCatalog catalog)
        {
            RegisterCompleteConditions(catalog);
            RegisterCompleteActions(catalog);
        }

        private static void RegisterCompleteConditions(TriggerTypeDescriptorCatalog catalog)
        {
            catalog.Register(new TriggerTypeDescriptor(TriggerNodeKind.Condition, "all", "全部满足", "Condition/Composite", 1, -1));
            catalog.Register(new TriggerTypeDescriptor(TriggerNodeKind.Condition, "any", "任一满足", "Condition/Composite", 1, -1));
            catalog.Register(new TriggerTypeDescriptor(TriggerNodeKind.Condition, "not", "结果取反", "Condition/Composite", 1, 1));
            catalog.Register(Condition("always_true", "始终满足", "Condition/Constant"));
            catalog.Register(Condition("always_false", "始终不满足", "Condition/Constant"));

            catalog.Register(Condition("arg_eq", "参数等于", "Condition/Compare",
                Required("left", TriggerValueType.None), Required("right", TriggerValueType.None)));
            catalog.Register(Condition("arg_neq", "参数不等于", "Condition/Compare",
                Required("left", TriggerValueType.None), Required("right", TriggerValueType.None)));
            RegisterNumericComparison(catalog, "arg_gt", "参数大于");
            RegisterNumericComparison(catalog, "arg_gte", "参数大于或等于");
            RegisterNumericComparison(catalog, "arg_geq", "参数大于或等于（别名）");
            RegisterNumericComparison(catalog, "arg_lt", "参数小于");
            RegisterNumericComparison(catalog, "arg_lte", "参数小于或等于");
            RegisterNumericComparison(catalog, "arg_leq", "参数小于或等于（别名）");

            RegisterNumericVariableComparison(catalog, "num_var_gt", "数值变量大于");
            RegisterNumericVariableComparison(catalog, "num_var_lt", "数值变量小于");
            RegisterNumericVariableComparison(catalog, "num_var_eq", "数值变量等于");

        }

        private static void RegisterNumericComparison(
            TriggerTypeDescriptorCatalog catalog,
            string type,
            string displayName)
        {
            catalog.Register(Condition(type, displayName, "Condition/Compare",
                Required("left", TriggerValueType.Number), Required("right", TriggerValueType.Number)));
        }

        private static void RegisterLegacyMobaConditions(TriggerTypeDescriptorCatalog catalog)
        {
            catalog.Register(Condition("has_buff", "拥有增益效果", "Condition/Combat",
                Required("buff_id", TriggerValueType.Integer),
                Optional("check_stack", TriggerValueType.Boolean),
                Choice("target_mode", false, Option(0, "目标"), Option(1, "来源")),
                ObjectParameter("options", false,
                    Optional("check_stack", TriggerValueType.Boolean),
                    Choice("target_mode", false, Option(0, "目标"), Option(1, "来源"))),
                ObjectParameter("target", false,
                    Choice("mode", false, Option(0, "目标"), Option(1, "来源")),
                    Choice("target_mode", false, Option(0, "目标"), Option(1, "来源")))));
            catalog.Register(Condition("health_percent", "生命值百分比", "Condition/Combat",
                Required("threshold", TriggerValueType.Number),
                Choice("compare_type", false, Option(0, "小于"), Option(1, "大于"))));
            catalog.Register(Condition("owner_matches_payload_source", "所有者匹配事件来源", "Condition/Context"));
            catalog.Register(Condition("owner_matches_payload_target", "所有者匹配事件目标", "Condition/Context"));
            catalog.Register(Condition("target_is_flying_projectile", "目标是飞行投射物", "Condition/Context"));
        }

        private static void RegisterNumericVariableComparison(
            TriggerTypeDescriptorCatalog catalog,
            string type,
            string displayName)
        {
            const TriggerValueSourceMask variables =
                TriggerValueSourceMask.LocalBlackboard | TriggerValueSourceMask.GlobalBlackboard;
            catalog.Register(Condition(type, displayName, "Condition/Blackboard",
                new TriggerParameterDescriptor("variable", TriggerValueType.Number, true, variables),
                Required("value", TriggerValueType.Number)));
        }

        private static void RegisterCompleteActions(TriggerTypeDescriptorCatalog catalog)
        {
            catalog.Register(new TriggerTypeDescriptor(TriggerNodeKind.Action, "seq", "顺序执行", "Action/Flow", 1, -1, true));
            catalog.Register(new TriggerTypeDescriptor(TriggerNodeKind.Action, "selector", "顺序选择", "Action/Flow", 1, -1, true));
            catalog.Register(new TriggerTypeDescriptor(TriggerNodeKind.Action, "random", "随机选择", "Action/Flow", 1, -1, true));
            catalog.Register(new TriggerTypeDescriptor(TriggerNodeKind.Action, "parallel", "并行执行", "Action/Flow", 1, -1, true));
            catalog.Register(new TriggerTypeDescriptor(
                TriggerNodeKind.Action,
                "repeat",
                "重复执行",
                "Action/Flow",
                1,
                1,
                true,
                new TriggerParameterDescriptor(
                    "count",
                    TriggerValueType.Integer,
                    true,
                    TriggerValueSourceMask.Constant)));
            catalog.Register(new TriggerTypeDescriptor(
                TriggerNodeKind.Action,
                "until",
                "执行直到",
                "Action/Flow",
                1,
                1,
                true,
                new TriggerParameterDescriptor(
                    "max_iterations",
                    TriggerValueType.Integer,
                    true,
                    TriggerValueSourceMask.Constant)));
            catalog.Register(new TriggerTypeDescriptor(TriggerNodeKind.Action, "invert", "反转结果", "Action/Flow", 1, 1, true));
            catalog.Register(new TriggerTypeDescriptor(TriggerNodeKind.Action, "succeed", "强制成功", "Action/Flow", 0, 1, true));
            catalog.Register(new TriggerTypeDescriptor(
                TriggerNodeKind.Action,
                "fail",
                "强制失败",
                "Action/Flow",
                0,
                1,
                true,
                new TriggerParameterDescriptor(
                    "reason",
                    TriggerValueType.String,
                    false,
                    TriggerValueSourceMask.Constant)));
            catalog.Register(new TriggerTypeDescriptor(
                TriggerNodeKind.Action,
                "weighted",
                "分支权重",
                "Action/Flow",
                1,
                1,
                true,
                new TriggerParameterDescriptor(
                    "weight",
                    TriggerValueType.Number,
                    true,
                    TriggerValueSourceMask.Constant)));
            catalog.Register(new TriggerTypeDescriptor(
                TriggerNodeKind.Action,
                "scheduled",
                "调度执行",
                "Action/Flow",
                1,
                1,
                true,
                Choice("schedule_mode", true,
                    Option(0, "立即"), Option(1, "延迟一次"), Option(2, "周期"),
                    Option(3, "外部驱动"), Option(4, "条件驱动"), Option(5, "持续")),
                Optional("interval_ms", TriggerValueType.Number),
                Optional("max_executions", TriggerValueType.Integer),
                Optional("can_be_interrupted", TriggerValueType.Boolean)));
            catalog.Register(new TriggerTypeDescriptor(
                TriggerNodeKind.Action,
                "for_each",
                "遍历集合",
                "Action/Flow",
                1,
                -1,
                true,
                Required("collection", TriggerValueType.Integer),
                Writable("item", TriggerValueType.Integer),
                new TriggerParameterDescriptor(
                    "max_iterations",
                    TriggerValueType.Integer,
                    true,
                    TriggerValueSourceMask.Constant)));
            catalog.Register(Action("execute_trigger", "执行触发效果", "Action/Flow",
                new TriggerParameterDescriptor(
                    "trigger_id",
                    TriggerValueType.Integer,
                    true,
                    TriggerValueSourceMask.Constant)));
            catalog.Register(new TriggerTypeDescriptor(
                TriggerNodeKind.Action,
                "conditional",
                "条件分支",
                "Action/Flow",
                1,
                -1,
                true));
            catalog.Register(Action("debug_log", "输出调试日志", "Action/Debug",
                Required("message", TriggerValueType.String),
                Optional("dump_args", TriggerValueType.Boolean)));
            catalog.Register(Action("set_var", "设置变量", "Action/Variable",
                Writable("target", TriggerValueType.None), Required("value", TriggerValueType.None)));
            catalog.Register(Action("set_num_var", "设置数值变量", "Action/Variable",
                Writable("target", TriggerValueType.Number), Required("value", TriggerValueType.Number)));
            catalog.Register(Action("add_num_var", "增加数值变量", "Action/Variable",
                Writable("target", TriggerValueType.Number), Required("value", TriggerValueType.Number)));
            catalog.Register(AuthoringOnlyAction("attr_effect_duration", "添加限时属性效果", "Action/Attribute",
                Required("attr", TriggerValueType.String),
                Required("op", TriggerValueType.String),
                Required("value", TriggerValueType.Number),
                Optional("source_id", TriggerValueType.Integer),
                Optional("duration", TriggerValueType.Number)));

        }

        private static void RegisterCombatActions(TriggerTypeDescriptorCatalog catalog)
        {
            catalog.Register(Action("give_damage", "造成伤害", "Action/Combat", WithTargets(
                OneOf("damage_amount", "damage_value", TriggerValueType.Number),
                OneOf("damage_amount", "source_attack_ratio", TriggerValueType.Number),
                DamageType("damage_type"),
                DamageReason("reason_kind"),
                Optional("reason_param", TriggerValueType.Integer),
                Choice("attribute_source", false, Option(0, "归属实体"), Option(1, "触发器所有者")))));
            catalog.Register(Action("adjust_damage_number", "调整伤害数值", "Action/Combat",
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
            catalog.Register(Action("take_damage", "承受伤害", "Action/Combat",
                Optional("rate", TriggerValueType.Number),
                Optional("reason_param", TriggerValueType.Integer)));
            catalog.Register(Action("heal", "治疗", "Action/Combat", WithTargets(
                Required("amount", TriggerValueType.Number),
                DamageType("heal_type"),
                DamageReason("reason_kind"),
                Optional("reason_param", TriggerValueType.Integer))));
        }

        private static void RegisterBuffAndShieldActions(TriggerTypeDescriptorCatalog catalog)
        {
            catalog.Register(Action("add_buff", "添加增益效果", "Action/Buff", WithTargets(
                Required("buff_ids", TriggerValueType.IntegerList))));
            catalog.Register(Action("remove_buff", "移除增益效果", "Action/Buff", WithTargets(
                Optional("buff_id", TriggerValueType.Integer),
                Optional("source_actor_id", TriggerValueType.Integer),
                Optional("remove_all", TriggerValueType.Boolean),
                Optional("remove_slow", TriggerValueType.Boolean),
                Optional("reason", TriggerValueType.Integer))));

            catalog.Register(Action("add_shield", "添加护盾", "Action/Shield", WithTargets(
                Optional("shield_id", TriggerValueType.Integer),
                Required("shield_value", TriggerValueType.Number),
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
                    Option(2, "最早优先"), Option(3, "最新优先")))));
            catalog.Register(Action("remove_shield", "移除护盾", "Action/Shield", WithTargets(
                OneOf("shield_identity", "shield_id", TriggerValueType.Integer),
                OneOf("shield_identity", "instance_id", TriggerValueType.Integer),
                Optional("source_actor_id", TriggerValueType.Integer),
                Optional("remove_all", TriggerValueType.Boolean))));
        }

        private static void RegisterResourceActions(TriggerTypeDescriptorCatalog catalog)
        {
            catalog.Register(Action("modify_resource", "修改资源", "Action/Resource", WithTargets(
                Required("amount", TriggerValueType.Number),
                ResourceType("resource_type"),
                Optional("min", TriggerValueType.Number),
                Optional("max", TriggerValueType.Number))));
            catalog.Register(Action("consume_resource", "消耗资源", "Action/Resource",
                Optional("amount", TriggerValueType.Number), ResourceType("resource_type")));
            catalog.Register(Action("convert_resource_to_heal", "将资源转为治疗", "Action/Resource", WithTargets(
                Required("amount", TriggerValueType.Number),
                ResourceType("resource_type"),
                Optional("heal_ratio", TriggerValueType.Number),
                Optional("out_of_combat_seconds", TriggerValueType.Number),
                DamageType("heal_type"),
                DamageReason("reason_kind"),
                Optional("reason_param", TriggerValueType.Integer))));
        }

        private static void RegisterSpawnAndSkillActions(TriggerTypeDescriptorCatalog catalog)
        {
            catalog.Register(Action("shoot_projectile", "发射投射物", "Action/Projectile", WithTargets(
                Required("launcher_id", TriggerValueType.Integer),
                Required("projectile_id", TriggerValueType.Integer),
                Optional("continuous_process_id", TriggerValueType.Integer),
                Optional("track_target", TriggerValueType.Boolean))));
            catalog.Register(Action("remove_projectile", "移除投射物", "Action/Projectile"));

            catalog.Register(Action("spawn_summon", "生成召唤物", "Action/Summon",
                Required("summon_id", TriggerValueType.Integer),
                Optional("position_mode", TriggerValueType.Integer),
                Optional("rotation_mode", TriggerValueType.Integer),
                Optional("interval_ms", TriggerValueType.Number),
                Optional("duration_ms", TriggerValueType.Number),
                Optional("total_count", TriggerValueType.Integer),
                Optional("query_template_id", TriggerValueType.Integer),
                Optional("target_mode", TriggerValueType.Integer)));
            catalog.Register(Action("remove_summon", "移除召唤物", "Action/Summon", WithTargets(
                Optional("summon_id", TriggerValueType.Integer),
                Optional("summon_actor_id", TriggerValueType.Integer),
                Optional("root_owner_actor_id", TriggerValueType.Integer),
                Optional("remove_all", TriggerValueType.Boolean),
                Optional("reason", TriggerValueType.Integer))));

            catalog.Register(Action("spawn_area", "生成区域", "Action/Area", WithTargets(
                Required("area_id", TriggerValueType.Integer),
                Optional("position_mode", TriggerValueType.Integer),
                Optional("radius", TriggerValueType.Number),
                Optional("duration_frames", TriggerValueType.Integer),
                Optional("duration_ms", TriggerValueType.Integer),
                Optional("stay_interval_frames", TriggerValueType.Integer),
                Optional("collision_layer_mask", TriggerValueType.Integer),
                Optional("offset_x", TriggerValueType.Number),
                Optional("offset_y", TriggerValueType.Number),
                Optional("offset_z", TriggerValueType.Number))));
            catalog.Register(Action("remove_area", "移除区域", "Action/Area", WithTargets(
                OneOf("area_identity", "area_id", TriggerValueType.Integer),
                OneOf("area_identity", "template_id", TriggerValueType.Integer),
                OneOf("area_identity", "owner_actor_id", TriggerValueType.Integer),
                Optional("remove_all", TriggerValueType.Boolean))));

            catalog.Register(Action("cancel_skill", "取消技能", "Action/Skill", WithTargets(
                Choice("mode", false, Option(0, "自动"), Option(1, "全部"), Option(2, "技能槽位"), Option(3, "技能 ID")),
                Optional("skill_id", TriggerValueType.Integer),
                Optional("skill_slot", TriggerValueType.Integer),
                Optional("remove_all", TriggerValueType.Boolean))));
            catalog.Register(Action("start_cooldown", "开始冷却", "Action/Skill",
                Optional("skill_id", TriggerValueType.Integer),
                Optional("skill_slot", TriggerValueType.Integer),
                Required("cooldown_ms", TriggerValueType.Integer)));
            catalog.Register(Action("reset_cooldown", "重置冷却", "Action/Skill", WithTargets(
                OneOf("skill_identity", "skill_id", TriggerValueType.Integer),
                OneOf("skill_identity", "skill_slot", TriggerValueType.Integer))));
        }

        private static void RegisterMotionActions(TriggerTypeDescriptorCatalog catalog)
        {
            catalog.Register(Action("blink", "闪现", "Action/Motion",
                Optional("distance", TriggerValueType.Number),
                Optional("direction_mode", TriggerValueType.Integer),
                Optional("priority", TriggerValueType.Integer),
                Optional("apply_to_caster", TriggerValueType.Boolean),
                Optional("pass_through_walls", TriggerValueType.Boolean)));
            catalog.Register(Action("dash", "冲刺", "Action/Motion", WithContinuous(
                Optional("speed", TriggerValueType.Number),
                Optional("duration_ms", TriggerValueType.Number),
                Optional("direction_mode", TriggerValueType.Integer),
                Optional("priority", TriggerValueType.Integer),
                Optional("apply_to_caster", TriggerValueType.Boolean),
                Optional("hit_trigger_plan_id", TriggerValueType.Integer),
                Optional("motion_group_id", TriggerValueType.Integer),
                Optional("move_to_aim_position", TriggerValueType.Boolean),
                Optional("pass_through_walls", TriggerValueType.Boolean))));
            catalog.Register(Action("jump", "跳跃", "Action/Motion", WithContinuous(
                Optional("height", TriggerValueType.Number),
                Optional("duration_ms", TriggerValueType.Number),
                Optional("priority", TriggerValueType.Integer),
                Optional("apply_to_caster", TriggerValueType.Boolean),
                Optional("motion_group_id", TriggerValueType.Integer),
                Optional("landing_trigger_ids", TriggerValueType.IntegerList))));
            catalog.Register(Action("pull", "拉拽", "Action/Motion", WithTargets(WithContinuous(
                Optional("speed", TriggerValueType.Number),
                Optional("duration_ms", TriggerValueType.Number),
                Optional("direction_mode", TriggerValueType.Integer),
                Optional("target_distance", TriggerValueType.Number),
                Optional("priority", TriggerValueType.Integer),
                Optional("motion_group_id", TriggerValueType.Integer)))));
        }

        private static void RegisterPresentationAndGameplayActions(TriggerTypeDescriptorCatalog catalog)
        {
            catalog.Register(Action("play_presentation", "播放表现", "Action/Presentation",
                Required("template_id", TriggerValueType.Integer),
                Optional("target_mode", TriggerValueType.Integer),
                Optional("duration_ms", TriggerValueType.Integer),
                Optional("stop", TriggerValueType.Boolean),
                Optional("x", TriggerValueType.Number),
                Optional("y", TriggerValueType.Number),
                Optional("z", TriggerValueType.Number),
                Optional("scale", TriggerValueType.Number),
                Optional("radius", TriggerValueType.Number)));
            catalog.Register(Action("emit", "发送表现事件", "Action/Presentation",
                Required("emitter_id", TriggerValueType.Integer)));

            catalog.Register(Action("set_gameplay_var", "设置玩法变量", "Action/Gameplay",
                Required("key_id", TriggerValueType.Integer), Optional("value", TriggerValueType.Number)));
            catalog.Register(Action("add_gameplay_var", "增加玩法变量", "Action/Gameplay",
                Required("key_id", TriggerValueType.Integer), Optional("delta", TriggerValueType.Number)));
            catalog.Register(Action("advance_gameplay_counter", "推进玩法计数器", "Action/Gameplay",
                Required("key_id", TriggerValueType.Integer),
                Required("scope_payload_field_id", TriggerValueType.Integer),
                Required("threshold", TriggerValueType.Number),
                Optional("delta", TriggerValueType.Number),
                Optional("reset_value", TriggerValueType.Number),
                Required("trigger_id", TriggerValueType.Integer)));
            catalog.Register(Action("end_game", "结束游戏", "Action/Gameplay",
                Optional("reason_id", TriggerValueType.Integer),
                Optional("win_team_id", TriggerValueType.Integer)));
        }

        private static TriggerTypeDescriptor Condition(
            string type,
            string displayName,
            string category,
            params TriggerParameterDescriptor[] parameters)
        {
            return new TriggerTypeDescriptor(TriggerNodeKind.Condition, type, displayName, category, 0, 0, parameters);
        }

        private static TriggerTypeDescriptor Action(
            string type,
            string displayName,
            string category,
            params TriggerParameterDescriptor[] parameters)
        {
            return new TriggerTypeDescriptor(TriggerNodeKind.Action, type, displayName, category, 0, 0, true, parameters);
        }

        private static TriggerTypeDescriptor AuthoringOnlyAction(
            string type,
            string displayName,
            string category,
            params TriggerParameterDescriptor[] parameters)
        {
            return new TriggerTypeDescriptor(TriggerNodeKind.Action, type, displayName, category, 0, 0, false, parameters);
        }

        private static TriggerParameterDescriptor Required(string name, TriggerValueType type)
        {
            return new TriggerParameterDescriptor(name, type);
        }

        private static TriggerParameterDescriptor Optional(string name, TriggerValueType type)
        {
            return new TriggerParameterDescriptor(name, type, false);
        }

        private static TriggerParameterDescriptor Writable(string name, TriggerValueType type)
        {
            const TriggerValueSourceMask variables =
                TriggerValueSourceMask.Context |
                TriggerValueSourceMask.LocalBlackboard |
                TriggerValueSourceMask.GlobalBlackboard;
            return new TriggerParameterDescriptor(
                name, type, true, variables, TriggerParameterAccess.Write);
        }

        private static TriggerParameterDescriptor OneOf(string group, string name, TriggerValueType type)
        {
            return new TriggerParameterDescriptor(
                name, type, false, TriggerValueSourceMask.All, TriggerParameterAccess.Read, group);
        }

        private static TriggerParameterDescriptor ObjectParameter(
            string name,
            bool required,
            params TriggerParameterDescriptor[] fields)
        {
            return new TriggerParameterDescriptor(
                name,
                TriggerValueType.Object,
                required,
                TriggerValueSourceMask.All,
                TriggerParameterAccess.Read,
                null,
                fields);
        }

        private static TriggerParameterDescriptor Choice(
            string name,
            bool required,
            params TriggerParameterOption[] options)
        {
            return new TriggerParameterDescriptor(
                name,
                TriggerValueType.Integer,
                required,
                TriggerValueSourceMask.All,
                TriggerParameterAccess.Read,
                null,
                options);
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
                        Option(0, "无"), Option(0x0204, "要求有效 ID"),
                        Option(0x0205, "要求位置信息"), Option(0x0101, "圆形范围"),
                        Option(0x0102, "扇形范围"), Option(0x0301, "排除施法者"),
                        Option(0x0302, "排除上下文目标"), Option(0x0201, "白名单"),
                        Option(0x0202, "黑名单")),
                    Optional("filter_param", TriggerValueType.Integer),
                    Optional("radius", TriggerValueType.Number),
                    Optional("half_angle_deg", TriggerValueType.Number),
                    Choice("order", false,
                        Option(0, "无"), Option(0x2001, "固定为零"), Option(0x2002, "随机"),
                        Option(0x2004, "距施法者距离"), Option(0x2005, "距上下文目标距离")),
                    Optional("order_param", TriggerValueType.Integer),
                    Choice("select", false,
                        Option(0x1001, "前 K 个"), Option(0x1002, "流式选择前 K 个")),
                    Optional("max_count", TriggerValueType.Integer),
                    Optional("self", TriggerValueType.Boolean)),
                Optional("query_template_id", TriggerValueType.Integer),
                Optional("target_actor_id", TriggerValueType.Integer),
                Optional("target_payload_field_id", TriggerValueType.Integer),
                Choice("target_source", false,
                    Option(3, "上下文目标"), Option(4, "自身"), Option(2, "指定实体"),
                    Option(1, "全部实体"), Option(5, "同队实体"), Option(6, "敌方实体"),
                    Option(7, "主类型"), Option(8, "单位子类型"), Option(1000, "查询模板")),
                Optional("target_source_param", TriggerValueType.Integer),
                Choice("target_filter", false,
                    Option(0, "无"), Option(0x0204, "要求有效 ID"),
                    Option(0x0205, "要求位置信息"), Option(0x0101, "圆形范围"),
                    Option(0x0102, "扇形范围"), Option(0x0301, "排除施法者"),
                    Option(0x0302, "排除上下文目标"), Option(0x0201, "白名单"),
                    Option(0x0202, "黑名单")),
                Optional("target_filter_param", TriggerValueType.Integer),
                Optional("target_radius", TriggerValueType.Number),
                Optional("target_half_angle_deg", TriggerValueType.Number),
                Choice("target_order", false,
                    Option(0, "无"), Option(0x2001, "固定为零"), Option(0x2002, "随机"),
                    Option(0x2004, "距施法者距离"), Option(0x2005, "距上下文目标距离")),
                Optional("target_order_param", TriggerValueType.Integer),
                Choice("target_select", false,
                    Option(0x1001, "前 K 个"), Option(0x1002, "流式选择前 K 个")),
                Optional("target_max_count", TriggerValueType.Integer),
                Optional("target_self", TriggerValueType.Boolean)
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

        private static TriggerParameterDescriptor[] Append(
            TriggerParameterDescriptor[] first,
            TriggerParameterDescriptor[] second)
        {
            var result = new TriggerParameterDescriptor[first.Length + second.Length];
            Array.Copy(first, 0, result, 0, first.Length);
            Array.Copy(second, 0, result, first.Length, second.Length);
            return result;
        }

        private static void RegisterOpenProjectTypes(
            TriggerTypeDescriptorCatalog catalog,
            TriggerNodeKind kind,
            IEnumerable<string> types)
        {
            foreach (var type in types)
            {
                catalog.Register(new TriggerTypeDescriptor(kind, type, type, "项目", 0, 0));
            }
        }

        private static string BuildKey(TriggerNodeKind kind, string type)
        {
            return ((int)kind).ToString() + ":" + (type ?? string.Empty);
        }
    }

    internal enum TriggerAuthoringDiagnosticSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    internal sealed class TriggerAuthoringDiagnostic
    {
        public TriggerAuthoringDiagnostic(
            string code,
            TriggerAuthoringDiagnosticSeverity severity,
            string path,
            string message)
        {
            Code = code;
            Severity = severity;
            Path = path;
            Message = message;
        }

        public string Code { get; }
        public TriggerAuthoringDiagnosticSeverity Severity { get; }
        public string Path { get; }
        public string Message { get; }
    }

    internal static class TriggerAuthoringValidator
    {
        public static List<TriggerAuthoringDiagnostic> Validate(
            TriggerAuthoringModuleData module,
            TriggerTypeDescriptorCatalog catalog = null)
        {
            return Validate(module, new TriggerAuthoringValidationContext
            {
                Types = catalog ?? TriggerTypeDescriptorCatalog.CreateProjectDefaults()
            });
        }

        public static List<TriggerAuthoringDiagnostic> Validate(
            TriggerAuthoringModuleData module,
            TriggerAuthoringValidationContext context)
        {
            context = context ?? new TriggerAuthoringValidationContext();
            var catalog = context.Types ?? TriggerTypeDescriptorCatalog.CreateProjectDefaults();
            var diagnostics = new List<TriggerAuthoringDiagnostic>();
            if (module == null)
            {
                AddError(diagnostics, "TRG1000", "module", "模块为空。");
                return diagnostics;
            }

            if (string.IsNullOrWhiteSpace(module.ModuleId))
                AddError(diagnostics, "TRG1001", "module.moduleId", "必须填写模块 ID。");

            ValidateNodeIdentities(diagnostics, module);

            if (context.Events == null)
                AddWarning(diagnostics, "TRG1402", "module", "尚未分配事件目录，事件和 Payload 校验能力受限。");

            var moduleKeys = ValidateBlackboard(
                diagnostics,
                module.Blackboard,
                "module.blackboard",
                TriggerAuthoringLocalBlackboardScope.Module);
            ValidateGroups(
                diagnostics,
                module,
                module.ConditionGroups,
                TriggerNodeKind.Condition,
                "module.conditionGroups",
                catalog,
                moduleKeys,
                context.GlobalBlackboard,
                context.References);
            ValidateGroups(
                diagnostics,
                module,
                module.ActionGroups,
                TriggerNodeKind.Action,
                "module.actionGroups",
                catalog,
                moduleKeys,
                context.GlobalBlackboard,
                context.References);
            var triggerIds = new HashSet<int>();
            var triggers = module.Triggers ?? new List<TriggerDefinitionData>();
            for (var i = 0; i < triggers.Count; i++)
            {
                var trigger = triggers[i];
                var path = $"module.triggers[{i}]";
                if (trigger == null)
                {
                    AddError(diagnostics, "TRG1002", path, "触发器为空。");
                    continue;
                }

                if (trigger.Id <= 0)
                    AddError(diagnostics, "TRG1003", path + ".id", "触发器 ID 必须大于零。");
                else if (!triggerIds.Add(trigger.Id))
                    AddError(diagnostics, "TRG1004", path + ".id", $"触发器 ID 重复：{trigger.Id}。");

                var effectiveTrigger = trigger;
                if (trigger.Template != null)
                    effectiveTrigger = TriggerAuthoringTemplateDefinition.ResolveEffective(trigger, context.Templates);

                if (effectiveTrigger.EntryMode == TriggerEntryMode.Event && string.IsNullOrWhiteSpace(effectiveTrigger.Event))
                    AddError(diagnostics, "TRG1005", path + ".event", "必须设置事件。");

                TriggerEventDefinitionData eventDefinition = null;
                if (effectiveTrigger.EntryMode == TriggerEntryMode.Event &&
                    !string.IsNullOrWhiteSpace(effectiveTrigger.Event) && context.Events != null &&
                    !context.Events.TryResolve(effectiveTrigger.Event, out eventDefinition))
                {
                    AddError(diagnostics, "TRG1400", path + ".event", $"未知事件：{effectiveTrigger.Event}。");
                }
                else if (eventDefinition != null && effectiveTrigger.AllowExternal && !eventDefinition.AllowExternal)
                {
                    AddError(diagnostics, "TRG1401", path + ".allowExternal", $"事件“{effectiveTrigger.Event}”不允许外部派发。");
                }

                var triggerKeys = new Dictionary<string, BlackboardSymbol>(moduleKeys, StringComparer.Ordinal);
                var declaredTriggerKeys = ValidateBlackboard(
                    diagnostics,
                    effectiveTrigger.Blackboard,
                    path + ".blackboard",
                    TriggerAuthoringLocalBlackboardScope.Trigger);
                ValidateCallableParameters(
                    diagnostics,
                    effectiveTrigger,
                    path + ".callableParameters",
                    declaredTriggerKeys);
                foreach (var pair in declaredTriggerKeys) triggerKeys[pair.Key] = pair.Value;
                ValidateTemplateReference(diagnostics, trigger, path, context, eventDefinition, moduleKeys);
                if (trigger.Template == null || trigger.Condition != null)
                    ValidateResolvedNode(diagnostics, module, effectiveTrigger.Condition, TriggerNodeKind.Condition, path + ".condition", catalog, triggerKeys, eventDefinition, context.GlobalBlackboard, context.References);
                if (trigger.Template == null || trigger.Actions != null)
                    ValidateResolvedNode(diagnostics, module, effectiveTrigger.Actions, TriggerNodeKind.Action, path + ".actions", catalog, triggerKeys, eventDefinition, context.GlobalBlackboard, context.References);

                if (effectiveTrigger.Actions != null && TriggerAuthoringGroupResolver.TryExpand(
                        module,
                        effectiveTrigger.Actions,
                        TriggerNodeKind.Action,
                        out var expandedActions,
                        out _))
                    ValidateTriggerReferenceNode(
                        diagnostics,
                        module,
                        effectiveTrigger,
                        expandedActions,
                        path + ".actions",
                        triggerKeys,
                        eventDefinition,
                        context.GlobalBlackboard,
                        context.Templates);
            }

            return diagnostics;
        }

        private static void ValidateNodeIdentities(
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            TriggerAuthoringModuleData module)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var visited = new HashSet<TriggerNodeData>();
            ValidateGroupNodeIdentities(
                diagnostics,
                module.ConditionGroups,
                "module.conditionGroups",
                ids,
                visited);
            ValidateGroupNodeIdentities(
                diagnostics,
                module.ActionGroups,
                "module.actionGroups",
                ids,
                visited);

            var triggers = module.Triggers;
            if (triggers == null) return;
            for (var i = 0; i < triggers.Count; i++)
            {
                var trigger = triggers[i];
                if (trigger == null) continue;
                var path = "module.triggers[" + i + "]";
                ValidateNodeIdentity(diagnostics, trigger.Condition, path + ".condition", ids, visited);
                ValidateNodeIdentity(diagnostics, trigger.Actions, path + ".actions", ids, visited);
            }
        }

        private static void ValidateGroupNodeIdentities(
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            IReadOnlyList<TriggerNodeGroupData> groups,
            string path,
            ISet<string> ids,
            ISet<TriggerNodeData> visited)
        {
            if (groups == null) return;
            for (var i = 0; i < groups.Count; i++)
                ValidateNodeIdentity(diagnostics, groups[i]?.Root, path + "[" + i + "].root", ids, visited);
        }

        private static void ValidateNodeIdentity(
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            TriggerNodeData node,
            string path,
            ISet<string> ids,
            ISet<TriggerNodeData> visited)
        {
            if (node == null || !visited.Add(node)) return;
            if (!string.IsNullOrWhiteSpace(node.NodeId))
            {
                if (!TriggerAuthoringNodeIdentity.IsValid(node.NodeId))
                    AddError(diagnostics, "TRG1250", path + ".nodeId", "Node ID has an invalid format.");
                else if (!ids.Add(node.NodeId))
                    AddError(diagnostics, "TRG1251", path + ".nodeId", "Duplicate node ID: " + node.NodeId + ".");
            }
            ValidateNodeIdentity(diagnostics, node.Condition, path + ".condition", ids, visited);
            ValidateChildNodeIdentities(diagnostics, node.Children, path + ".children", ids, visited);
            ValidateChildNodeIdentities(diagnostics, node.ElseChildren, path + ".elseChildren", ids, visited);
        }

        private static void ValidateChildNodeIdentities(
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            IReadOnlyList<TriggerNodeData> children,
            string path,
            ISet<string> ids,
            ISet<TriggerNodeData> visited)
        {
            if (children == null) return;
            for (var i = 0; i < children.Count; i++)
                ValidateNodeIdentity(diagnostics, children[i], path + "[" + i + "]", ids, visited);
        }

        private static void ValidateTriggerReferenceNode(
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            TriggerAuthoringModuleData module,
            TriggerDefinitionData owner,
            TriggerNodeData node,
            string path,
            IReadOnlyDictionary<string, BlackboardSymbol> localKeys,
            TriggerEventDefinitionData eventDefinition,
            TriggerGlobalBlackboardDescriptorCatalog globalBlackboard,
            TriggerTemplateDescriptorCatalog templates)
        {
            if (node == null || !node.Enabled) return;
            if (TriggerAuthoringTriggerReuse.IsReference(node))
            {
                if (!TriggerAuthoringTriggerReuse.TryGetReferencedTriggerId(node, out var targetId))
                {
                    AddError(diagnostics, "TRG1701", path + ".arguments.trigger_id", "必须设置有效的触发器 ID。");
                }
                else
                {
                    var target = TriggerAuthoringTriggerReuse.FindTrigger(module, targetId);
                    if (target == null)
                        AddError(diagnostics, "TRG1702", path + ".arguments.trigger_id", $"未找到触发器：{targetId}。");
                    else if (owner != null && ReferencesTrigger(module, target, owner.Id, new HashSet<int>()))
                        AddError(diagnostics, "TRG1703", path + ".arguments.trigger_id", $"触发器引用形成循环：{owner.Id} -> {targetId}。");
                    else
                        ValidateCallableBindings(
                            diagnostics,
                            node,
                            TriggerAuthoringTemplateDefinition.ResolveEffective(target, templates),
                            path,
                            localKeys,
                            eventDefinition,
                            globalBlackboard);
                }
            }

            ValidateTriggerReferenceNode(diagnostics, module, owner, node.Condition, path + ".condition", localKeys, eventDefinition, globalBlackboard, templates);
            ValidateTriggerReferenceChildren(diagnostics, module, owner, node.Children, path + ".children", localKeys, eventDefinition, globalBlackboard, templates);
            ValidateTriggerReferenceChildren(diagnostics, module, owner, node.ElseChildren, path + ".elseChildren", localKeys, eventDefinition, globalBlackboard, templates);
        }

        private static void ValidateTriggerReferenceChildren(
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            TriggerAuthoringModuleData module,
            TriggerDefinitionData owner,
            IReadOnlyList<TriggerNodeData> children,
            string path,
            IReadOnlyDictionary<string, BlackboardSymbol> localKeys,
            TriggerEventDefinitionData eventDefinition,
            TriggerGlobalBlackboardDescriptorCatalog globalBlackboard,
            TriggerTemplateDescriptorCatalog templates)
        {
            if (children == null) return;
            for (var i = 0; i < children.Count; i++)
                ValidateTriggerReferenceNode(diagnostics, module, owner, children[i], path + "[" + i + "]", localKeys, eventDefinition, globalBlackboard, templates);
        }

        private static void ValidateCallableParameters(
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            TriggerDefinitionData trigger,
            string path,
            IReadOnlyDictionary<string, BlackboardSymbol> triggerKeys)
        {
            var parameters = trigger?.CallableParameters;
            if (parameters == null || parameters.Count == 0) return;
            if (!string.Equals(trigger.Scope, "owner", StringComparison.OrdinalIgnoreCase))
                AddError(diagnostics, "TRG1710", path, "带参数的可调用触发器必须使用 owner 作用域。");

            var names = new HashSet<string>(StringComparer.Ordinal);
            var localKeys = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < parameters.Count; i++)
            {
                var parameter = parameters[i];
                var parameterPath = path + "[" + i + "]";
                if (parameter == null)
                {
                    AddError(diagnostics, "TRG1711", parameterPath, "调用参数不能为空。");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(parameter.Name))
                    AddError(diagnostics, "TRG1711", parameterPath + ".name", "必须填写调用参数名称。");
                else if (string.Equals(parameter.Name, TriggerAuthoringTriggerReuse.TriggerIdArgument, StringComparison.Ordinal))
                    AddError(diagnostics, "TRG1711", parameterPath + ".name", "调用参数名称不能使用保留名称 trigger_id。");
                else if (!names.Add(parameter.Name))
                    AddError(diagnostics, "TRG1712", parameterPath + ".name", $"调用参数重复：{parameter.Name}。");

                if (!IsRuntimeBlackboardType(parameter.Type))
                    AddError(diagnostics, "TRG1713", parameterPath + ".type", $"调用参数不支持类型 {parameter.Type}。");
                if (parameter.Direction != TriggerCallableParameterDirection.Input &&
                    parameter.Direction != TriggerCallableParameterDirection.Output)
                    AddError(diagnostics, "TRG1713", parameterPath + ".direction", "调用参数方向必须是 Input 或 Output。");
                if (string.IsNullOrWhiteSpace(parameter.LocalVariableKey))
                {
                    AddError(diagnostics, "TRG1714", parameterPath + ".localVariableKey", "调用参数必须绑定触发器局部变量。");
                    continue;
                }
                if (!localKeys.Add(parameter.LocalVariableKey))
                    AddError(diagnostics, "TRG1715", parameterPath + ".localVariableKey", $"多个调用参数不能绑定同一个局部变量：{parameter.LocalVariableKey}。");
                if (triggerKeys == null || !triggerKeys.TryGetValue(parameter.LocalVariableKey, out var symbol))
                {
                    AddError(diagnostics, "TRG1716", parameterPath + ".localVariableKey", $"未找到调用参数对应的触发器局部变量：{parameter.LocalVariableKey}。");
                    continue;
                }
                if (!IsTypeCompatible(parameter.Type, symbol.Type))
                    AddError(diagnostics, "TRG1717", parameterPath + ".type", $"调用参数类型 {parameter.Type} 与局部变量类型 {symbol.Type} 不一致。");
                if (symbol.ReadOnly)
                    AddError(diagnostics, "TRG1722", parameterPath + ".localVariableKey", "调用参数对应的局部变量必须允许运行时写入。");

                // Runtime needs to populate inputs before entering the target plan. Inside the
                // callable, input symbols remain read-only at authoring time.
                if (parameter.Direction == TriggerCallableParameterDirection.Input)
                    symbol.ReadOnly = true;

                if (parameter.Direction == TriggerCallableParameterDirection.Output && parameter.HasDefault)
                    AddError(diagnostics, "TRG1718", parameterPath + ".hasDefault", "输出参数不能设置默认值。");
                if (parameter.Direction == TriggerCallableParameterDirection.Input && parameter.HasDefault)
                {
                    if (parameter.DefaultValue == null || parameter.DefaultValue.Source != TriggerValueSource.Constant)
                        AddError(diagnostics, "TRG1719", parameterPath + ".defaultValue", "调用参数默认值必须是常量。");
                    else if (!IsTypeCompatible(parameter.Type, parameter.DefaultValue.Type))
                        AddError(diagnostics, "TRG1719", parameterPath + ".defaultValue.type", "调用参数默认值类型不匹配。");
                }
            }
        }

        private static void ValidateCallableBindings(
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            TriggerNodeData call,
            TriggerDefinitionData target,
            string path,
            IReadOnlyDictionary<string, BlackboardSymbol> localKeys,
            TriggerEventDefinitionData eventDefinition,
            TriggerGlobalBlackboardDescriptorCatalog globalBlackboard)
        {
            var parameters = target?.CallableParameters;
            var bindings = new Dictionary<string, TriggerArgumentData>(StringComparer.Ordinal);
            var arguments = call.Arguments ?? new List<TriggerArgumentData>();
            for (var i = 0; i < arguments.Count; i++)
            {
                var argument = arguments[i];
                if (argument == null || string.IsNullOrWhiteSpace(argument.Name) ||
                    string.Equals(argument.Name, TriggerAuthoringTriggerReuse.TriggerIdArgument, StringComparison.Ordinal))
                    continue;
                bindings[argument.Name] = argument;
            }

            for (var i = 0; i < (parameters?.Count ?? 0); i++)
            {
                var parameter = parameters[i];
                if (parameter == null || string.IsNullOrWhiteSpace(parameter.Name)) continue;
                if (!bindings.TryGetValue(parameter.Name, out var binding))
                {
                    if (parameter.Required && !(parameter.Direction == TriggerCallableParameterDirection.Input && parameter.HasDefault))
                        AddError(diagnostics, "TRG1720", path + ".arguments." + parameter.Name, $"缺少调用参数绑定：{parameter.Name}。");
                    continue;
                }

                var output = parameter.Direction == TriggerCallableParameterDirection.Output;
                ValidateValue(
                    diagnostics,
                    binding.Value,
                    new TriggerParameterDescriptor(
                        parameter.Name,
                        parameter.Type,
                        parameter.Required,
                        output
                            ? TriggerValueSourceMask.LocalBlackboard | TriggerValueSourceMask.GlobalBlackboard
                            : TriggerValueSourceMask.All,
                        output ? TriggerParameterAccess.Output : TriggerParameterAccess.Read),
                    path + ".arguments." + parameter.Name,
                    localKeys,
                    eventDefinition,
                    globalBlackboard,
                    null);
            }

            foreach (var pair in bindings)
                if (TriggerAuthoringTriggerReuse.FindParameter(target, pair.Key) == null)
                    AddWarning(diagnostics, "TRG1721", path + ".arguments." + pair.Key, $"目标触发器未声明调用参数：{pair.Key}。");
        }

        private static bool ReferencesTrigger(
            TriggerAuthoringModuleData module,
            TriggerDefinitionData current,
            int soughtId,
            ISet<int> visiting)
        {
            if (current == null || !visiting.Add(current.Id)) return false;
            try
            {
                if (!TriggerAuthoringGroupResolver.TryExpand(
                        module,
                        current.Actions,
                        TriggerNodeKind.Action,
                        out var actions,
                        out _)) return false;
                return NodeReferencesTrigger(module, actions, soughtId, visiting);
            }
            finally
            {
                visiting.Remove(current.Id);
            }
        }

        private static bool NodeReferencesTrigger(
            TriggerAuthoringModuleData module,
            TriggerNodeData node,
            int soughtId,
            ISet<int> visiting)
        {
            if (node == null || !node.Enabled) return false;
            if (TriggerAuthoringTriggerReuse.TryGetReferencedTriggerId(node, out var targetId))
            {
                if (targetId == soughtId) return true;
                var target = TriggerAuthoringTriggerReuse.FindTrigger(module, targetId);
                if (target != null && ReferencesTrigger(module, target, soughtId, visiting)) return true;
            }
            if (NodeReferencesTrigger(module, node.Condition, soughtId, visiting)) return true;
            if (ChildrenReferenceTrigger(module, node.Children, soughtId, visiting)) return true;
            return ChildrenReferenceTrigger(module, node.ElseChildren, soughtId, visiting);
        }

        private static bool ChildrenReferenceTrigger(
            TriggerAuthoringModuleData module,
            IReadOnlyList<TriggerNodeData> children,
            int soughtId,
            ISet<int> visiting)
        {
            if (children == null) return false;
            for (var i = 0; i < children.Count; i++)
                if (NodeReferencesTrigger(module, children[i], soughtId, visiting)) return true;
            return false;
        }

        private static void ValidateTemplateReference(
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            TriggerDefinitionData trigger,
            string path,
            TriggerAuthoringValidationContext context,
            TriggerEventDefinitionData eventDefinition,
            IReadOnlyDictionary<string, BlackboardSymbol> localKeys)
        {
            if (trigger?.Template == null) return;
            if (!TriggerAuthoringTemplateValidator.TryResolveReference(
                    trigger.Template,
                    trigger,
                    path + ".template",
                    context,
                    diagnostics,
                    out var asset))
                return;

            var template = asset.Template;
            var parameters = TriggerAuthoringTemplateValidator.BuildParameterMap(template);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var bindings = trigger.Template.Bindings ?? new List<TriggerArgumentData>();
            for (var i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                var bindingPath = $"{path}.template.bindings[{i}]";
                if (binding == null || string.IsNullOrWhiteSpace(binding.Name))
                {
                    AddError(diagnostics, "TRG1605", bindingPath + ".name", "必须填写模板绑定名称。");
                    continue;
                }
                if (!seen.Add(binding.Name))
                {
                    AddError(diagnostics, "TRG1606", bindingPath + ".name", $"模板绑定重复：{binding.Name}。");
                    continue;
                }
                if (!parameters.TryGetValue(binding.Name, out var parameter))
                {
                    AddError(diagnostics, "TRG1605", bindingPath + ".name", $"未知模板参数：{binding.Name}。");
                    continue;
                }
                if (binding.Value == null)
                {
                    AddError(diagnostics, "TRG1607", bindingPath + ".value", "必须设置模板绑定值。");
                    continue;
                }
                if (!TriggerAuthoringTemplateValidator.IsTypeCompatible(parameter.Type, binding.Value.Type))
                    AddError(diagnostics, "TRG1608", bindingPath + ".value.type", $"模板参数“{parameter.Name}”需要 {parameter.Type}，当前为 {binding.Value.Type}。");
                var sourceMask = (TriggerTemplateValueSourceMask)(1 << (int)binding.Value.Source);
                if ((parameter.AllowedSources & sourceMask) == 0)
                    AddError(diagnostics, "TRG1609", bindingPath + ".value.source", $"模板参数“{parameter.Name}”不允许使用来源 {binding.Value.Source}。");
                ValidateValue(
                    diagnostics,
                    binding.Value,
                    new TriggerParameterDescriptor(
                        parameter.Name,
                        parameter.Type,
                        true,
                        (TriggerValueSourceMask)(int)parameter.AllowedSources),
                    bindingPath + ".value",
                    localKeys,
                    eventDefinition,
                    context.GlobalBlackboard,
                    context.References);
            }

            foreach (var pair in parameters)
            {
                var parameter = pair.Value;
                if (!parameter.Required || parameter.HasDefault || seen.Contains(parameter.Name)) continue;
                AddError(
                    diagnostics,
                    "TRG1607",
                    path + ".template.bindings",
                    $"必填模板参数尚未绑定：{parameter.Name}。");
            }
        }

        public static bool HasErrors(IReadOnlyList<TriggerAuthoringDiagnostic> diagnostics)
        {
            if (diagnostics == null) return false;
            for (var i = 0; i < diagnostics.Count; i++)
            {
                if (diagnostics[i].Severity == TriggerAuthoringDiagnosticSeverity.Error) return true;
            }
            return false;
        }

        private static void ValidateGroups(
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            TriggerAuthoringModuleData module,
            IReadOnlyList<TriggerNodeGroupData> groups,
            TriggerNodeKind kind,
            string path,
            TriggerTypeDescriptorCatalog catalog,
            IReadOnlyDictionary<string, BlackboardSymbol> moduleKeys,
            TriggerGlobalBlackboardDescriptorCatalog globalBlackboard,
            TriggerAuthoringReferenceCatalog references)
        {
            if (groups == null) return;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var duplicateIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                var groupPath = $"{path}[{i}]";
                if (group == null)
                {
                    AddError(diagnostics, "TRG1500", groupPath, $"{KindLabel(kind)}分组为空。");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(group.Id))
                    AddError(diagnostics, "TRG1501", groupPath + ".id", $"必须填写{KindLabel(kind)}分组 ID。");
                else if (!ids.Add(group.Id))
                {
                    duplicateIds.Add(group.Id);
                    AddError(diagnostics, "TRG1502", groupPath + ".id", $"{KindLabel(kind)}分组 ID 重复：{group.Id}。");
                }
                if (group.Root == null)
                    AddError(diagnostics, "TRG1503", groupPath + ".root", $"{KindLabel(kind)}分组必须包含根节点。");
            }

            if (duplicateIds.Count > 0) return;
            for (var i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                if (group == null || group.Root == null) continue;
                ValidateResolvedNode(
                    diagnostics,
                    module,
                    group.Root,
                    kind,
                    $"{path}[{i}].root",
                    catalog,
                    moduleKeys,
                    null,
                    globalBlackboard,
                    references);
            }
        }

        private static void ValidateResolvedNode(
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            TriggerAuthoringModuleData module,
            TriggerNodeData node,
            TriggerNodeKind expectedKind,
            string path,
            TriggerTypeDescriptorCatalog catalog,
            IReadOnlyDictionary<string, BlackboardSymbol> localKeys,
            TriggerEventDefinitionData eventDefinition,
            TriggerGlobalBlackboardDescriptorCatalog globalBlackboard,
            TriggerAuthoringReferenceCatalog references)
        {
            ValidateReferenceShape(diagnostics, node, expectedKind, path);
            if (!TriggerAuthoringGroupResolver.TryExpand(
                    module,
                    node,
                    expectedKind,
                    out var expanded,
                    out var failure))
            {
                AddError(
                    diagnostics,
                    failure != null ? failure.Code : "TRG1505",
                    path + ".groupReference",
                    failure != null ? failure.Message : $"无法解析{KindLabel(expectedKind)}分组引用。");
                return;
            }

            ValidateNode(
                diagnostics,
                expanded,
                expectedKind,
                path,
                catalog,
                localKeys,
                eventDefinition,
                globalBlackboard,
                references);
            if (expectedKind == TriggerNodeKind.Action)
            {
                ValidateEmbeddedActionFlow(
                    diagnostics,
                    module,
                    expanded,
                    path,
                    catalog,
                    localKeys,
                    eventDefinition,
                    globalBlackboard,
                    references);
            }
        }

        private static void ValidateReferenceShape(
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            TriggerNodeData node,
            TriggerNodeKind expectedKind,
            string path)
        {
            if (node == null) return;
            if (!node.Enabled) return;
            if (!string.IsNullOrWhiteSpace(node.GroupReference))
            {
                if (node.Kind != expectedKind)
                    AddError(diagnostics, "TRG1201", path + ".kind", $"节点应为{KindLabel(expectedKind)}，当前为{KindLabel(node.Kind)}。");
                if (!string.IsNullOrWhiteSpace(node.Type) ||
                    node.Arguments != null && node.Arguments.Count > 0 ||
                    node.Condition != null ||
                    node.Children != null && node.Children.Count > 0 ||
                    node.ElseChildren != null && node.ElseChildren.Count > 0)
                {
                    AddError(
                        diagnostics,
                        "TRG1507",
                        path,
                        "分组引用节点不能同时包含类型、参数或子节点。");
                }
                return;
            }

            if (expectedKind == TriggerNodeKind.Action &&
                (string.Equals(node.Type, "conditional", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(node.Type, "until", StringComparison.OrdinalIgnoreCase)))
            {
                ValidateReferenceShape(
                    diagnostics,
                    node.Condition,
                    TriggerNodeKind.Condition,
                    path + ".condition");
                var elseChildren = node.ElseChildren;
                if (elseChildren != null)
                {
                    for (var i = 0; i < elseChildren.Count; i++)
                        ValidateReferenceShape(
                            diagnostics,
                            elseChildren[i],
                            TriggerNodeKind.Action,
                            $"{path}.elseChildren[{i}]");
                }
            }

            if (node.Children == null) return;
            for (var i = 0; i < node.Children.Count; i++)
                ValidateReferenceShape(diagnostics, node.Children[i], expectedKind, $"{path}.children[{i}]");
        }

        private static void ValidateEmbeddedActionFlow(
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            TriggerAuthoringModuleData module,
            TriggerNodeData node,
            string path,
            TriggerTypeDescriptorCatalog catalog,
            IReadOnlyDictionary<string, BlackboardSymbol> localKeys,
            TriggerEventDefinitionData eventDefinition,
            TriggerGlobalBlackboardDescriptorCatalog globalBlackboard,
            TriggerAuthoringReferenceCatalog references)
        {
            if (node == null || !node.Enabled) return;
            if (string.Equals(node.Type, "conditional", StringComparison.OrdinalIgnoreCase))
            {
                if (node.Condition == null || !node.Condition.Enabled)
                {
                    AddError(diagnostics, "TRG1230", path + ".condition", "条件分支必须配置并启用判断条件。");
                }
                else
                {
                    ValidateResolvedNode(
                        diagnostics,
                        module,
                        node.Condition,
                        TriggerNodeKind.Condition,
                        path + ".condition",
                        catalog,
                        localKeys,
                        eventDefinition,
                        globalBlackboard,
                        references);
                }

                var elseChildren = node.ElseChildren ?? new List<TriggerNodeData>();
                for (var i = 0; i < elseChildren.Count; i++)
                {
                    var child = elseChildren[i];
                    if (child == null || !child.Enabled) continue;
                    ValidateNode(
                        diagnostics,
                        child,
                        TriggerNodeKind.Action,
                        $"{path}.elseChildren[{i}]",
                        catalog,
                        localKeys,
                        eventDefinition,
                        globalBlackboard,
                        references);
                    ValidateEmbeddedActionFlow(
                        diagnostics,
                        module,
                        child,
                        $"{path}.elseChildren[{i}]",
                        catalog,
                        localKeys,
                        eventDefinition,
                        globalBlackboard,
                        references);
                }
            }
            else if (string.Equals(node.Type, "until", StringComparison.OrdinalIgnoreCase))
            {
                if (node.Condition == null || !node.Condition.Enabled)
                {
                    AddError(diagnostics, "TRG1231", path + ".condition", "执行直到节点必须配置并启用结束条件。");
                }
                else
                {
                    ValidateResolvedNode(
                        diagnostics,
                        module,
                        node.Condition,
                        TriggerNodeKind.Condition,
                        path + ".condition",
                        catalog,
                        localKeys,
                        eventDefinition,
                        globalBlackboard,
                        references);
                }
            }

            var children = node.Children;
            if (children == null) return;
            for (var i = 0; i < children.Count; i++)
                ValidateEmbeddedActionFlow(
                    diagnostics,
                    module,
                    children[i],
                    $"{path}.children[{i}]",
                    catalog,
                    localKeys,
                    eventDefinition,
                    globalBlackboard,
                    references);
        }

        private sealed class BlackboardSymbol
        {
            public TriggerValueType Type;
            public bool ReadOnly;
            public TriggerAuthoringLocalBlackboardScope Scope;
        }

        private static Dictionary<string, BlackboardSymbol> ValidateBlackboard(
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            IReadOnlyList<TriggerBlackboardVariableData> variables,
            string path,
            TriggerAuthoringLocalBlackboardScope scope)
        {
            var keys = new Dictionary<string, BlackboardSymbol>(StringComparer.Ordinal);
            if (variables == null) return keys;
            for (var i = 0; i < variables.Count; i++)
            {
                var variable = variables[i];
                var itemPath = $"{path}[{i}]";
                if (variable == null || string.IsNullOrWhiteSpace(variable.Key))
                {
                    AddError(diagnostics, "TRG1100", itemPath + ".key", "必须填写黑板 Key。");
                    continue;
                }
                if (keys.ContainsKey(variable.Key))
                    AddError(diagnostics, "TRG1101", itemPath + ".key", $"黑板 Key 重复：{variable.Key}。");
                else
                {
                    var symbol = new BlackboardSymbol
                    {
                        Type = variable.Type,
                        ReadOnly = variable.ReadOnly,
                        Scope = scope
                    };
                    keys.Add(variable.Key, symbol);
                    keys.Add(TriggerAuthoringLocalBlackboardPath.Format(scope, variable.Key), symbol);
                }
                if (variable.Type == TriggerValueType.None)
                    AddError(diagnostics, "TRG1102", itemPath + ".type", "必须设置黑板值类型。");
                else if (!IsRuntimeBlackboardType(variable.Type))
                    AddError(diagnostics, "TRG1103", itemPath + ".type",
                        $"项目触发器黑板不支持类型 {variable.Type}。");
            }
            return keys;
        }

        private static bool IsRuntimeBlackboardType(TriggerValueType type)
        {
            return type == TriggerValueType.Integer ||
                   type == TriggerValueType.Number ||
                   type == TriggerValueType.Boolean ||
                   type == TriggerValueType.String ||
                   type == TriggerValueType.Entity ||
                   type == TriggerValueType.ObjectId;
        }

        private static void ValidateNode(
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            TriggerNodeData node,
            TriggerNodeKind expectedKind,
            string path,
            TriggerTypeDescriptorCatalog catalog,
            IReadOnlyDictionary<string, BlackboardSymbol> localKeys,
            TriggerEventDefinitionData eventDefinition,
            TriggerGlobalBlackboardDescriptorCatalog globalBlackboard,
            TriggerAuthoringReferenceCatalog references)
        {
            if (node == null)
            {
                if (expectedKind == TriggerNodeKind.Action)
                    AddError(diagnostics, "TRG1200", path, "必须包含行为根节点。");
                return;
            }
            if (!node.Enabled) return;
            if (node.Kind != expectedKind)
                AddError(diagnostics, "TRG1201", path + ".kind", $"节点应为{KindLabel(expectedKind)}，当前为{KindLabel(node.Kind)}。");
            if (string.IsNullOrWhiteSpace(node.Type))
            {
                AddError(diagnostics, "TRG1202", path + ".type", "必须设置节点类型。");
                return;
            }
            if (!catalog.TryGet(expectedKind, node.Type, out var descriptor))
            {
                AddError(diagnostics, "TRG1203", path + ".type", $"未知{KindLabel(expectedKind)}类型：{node.Type}。");
                return;
            }

            var children = node.Children ?? new List<TriggerNodeData>();
            var enabledChildCount = CountEnabledChildren(children);
            if (enabledChildCount < descriptor.MinChildren)
                AddError(diagnostics, "TRG1204", path + ".children", $"节点至少需要 {descriptor.MinChildren} 个子节点。");
            if (descriptor.MaxChildren >= 0 && enabledChildCount > descriptor.MaxChildren)
                AddError(diagnostics, "TRG1205", path + ".children", $"节点最多允许 {descriptor.MaxChildren} 个子节点。");

            var arguments = new Dictionary<string, TriggerArgumentData>(StringComparer.Ordinal);
            var nodeArguments = node.Arguments ?? new List<TriggerArgumentData>();
            for (var i = 0; i < nodeArguments.Count; i++)
            {
                var argument = nodeArguments[i];
                var argumentPath = $"{path}.arguments[{i}]";
                if (argument == null || string.IsNullOrWhiteSpace(argument.Name))
                {
                    AddError(diagnostics, "TRG1210", argumentPath + ".name", "必须填写参数名称。");
                    continue;
                }
                if (arguments.ContainsKey(argument.Name))
                    AddError(diagnostics, "TRG1211", argumentPath + ".name", $"参数重复：{argument.Name}。");
                else
                {
                    arguments.Add(argument.Name, argument);
                    var known = false;
                    for (var parameterIndex = 0; parameterIndex < descriptor.Parameters.Count; parameterIndex++)
                    {
                        if (!string.Equals(
                                descriptor.Parameters[parameterIndex].Name,
                                argument.Name,
                                StringComparison.Ordinal)) continue;
                        known = true;
                        break;
                    }
                    if (!known && !TriggerAuthoringTriggerReuse.IsReference(node))
                        AddWarning(diagnostics, "TRG1214", argumentPath + ".name",
                            $"未知参数“{argument.Name}”会继续保留，但编辑 Schema 将忽略该参数。");
                }
            }

            for (var i = 0; i < descriptor.Parameters.Count; i++)
            {
                var parameter = descriptor.Parameters[i];
                if (!arguments.TryGetValue(parameter.Name, out var argument))
                {
                    if (parameter.Required)
                        AddError(diagnostics, "TRG1212", path + ".arguments", $"缺少必填参数：{parameter.Name}。");
                    continue;
                }
                ValidateValue(
                    diagnostics,
                    argument.Value,
                    parameter,
                    path + ".arguments." + parameter.Name,
                    localKeys,
                    eventDefinition,
                    globalBlackboard,
                    references);
            }

            ValidateSetVariableTypes(diagnostics, node, arguments, path);
            ValidateForEachLimits(diagnostics, node, arguments, path);
            ValidateRepeatAndUntilLimits(diagnostics, node, arguments, path);

            var requiredGroups = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < descriptor.Parameters.Count; i++)
            {
                var parameter = descriptor.Parameters[i];
                if (!string.IsNullOrEmpty(parameter.RequiredGroup))
                    requiredGroups.Add(parameter.RequiredGroup);
            }
            foreach (var group in requiredGroups)
            {
                var found = false;
                var choices = new List<string>();
                for (var i = 0; i < descriptor.Parameters.Count; i++)
                {
                    var parameter = descriptor.Parameters[i];
                    if (!string.Equals(parameter.RequiredGroup, group, StringComparison.Ordinal)) continue;
                    choices.Add(parameter.Name);
                    if (arguments.ContainsKey(parameter.Name)) found = true;
                }
                if (!found)
                    AddError(diagnostics, "TRG1213", path + ".arguments",
                        $"参数组“{group}”至少需要设置一项：{string.Join(", ", choices)}。");
            }

            for (var i = 0; i < children.Count; i++)
            {
                if (children[i] == null || !children[i].Enabled) continue;
                ValidateNode(diagnostics, children[i], expectedKind, $"{path}.children[{i}]", catalog, localKeys, eventDefinition, globalBlackboard, references);
            }
        }

        private static int CountEnabledChildren(IReadOnlyList<TriggerNodeData> children)
        {
            if (children == null) return 0;
            var count = 0;
            for (var i = 0; i < children.Count; i++)
                if (children[i] != null && children[i].Enabled) count++;
            return count;
        }

        private static void ValidateSetVariableTypes(
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            TriggerNodeData node,
            IReadOnlyDictionary<string, TriggerArgumentData> arguments,
            string path)
        {
            if (node.Kind != TriggerNodeKind.Action ||
                !string.Equals(node.Type, "set_var", StringComparison.Ordinal) ||
                !arguments.TryGetValue("target", out var targetArgument) ||
                !arguments.TryGetValue("value", out var valueArgument) ||
                targetArgument?.Value == null || valueArgument?.Value == null)
                return;

            var targetType = targetArgument.Value.Type;
            var valueType = valueArgument.Value.Type;
            if (!IsSetVariableType(targetType))
            {
                AddError(diagnostics, "TRG1315", path + ".arguments.target.type",
                    $"set_var 目标类型必须是数值、Boolean 或 String，当前为 {targetType}。");
                return;
            }
            if (!IsSetVariableType(valueType))
            {
                AddError(diagnostics, "TRG1315", path + ".arguments.value.type",
                    $"set_var 值类型必须是数值、Boolean 或 String，当前为 {valueType}。");
                return;
            }
            if (!IsSetVariableTypeCompatible(targetType, valueType))
                AddError(diagnostics, "TRG1315", path + ".arguments.value.type",
                    $"set_var 目标类型 {targetType} 与值类型 {valueType} 不匹配。");
        }

        private static void ValidateForEachLimits(
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            TriggerNodeData node,
            IReadOnlyDictionary<string, TriggerArgumentData> arguments,
            string path)
        {
            if (node.Kind != TriggerNodeKind.Action ||
                !string.Equals(node.Type, "for_each", StringComparison.Ordinal) ||
                !arguments.TryGetValue("max_iterations", out var argument) ||
                argument?.Value == null)
                return;

            var value = argument.Value;
            if (value.Source != TriggerValueSource.Constant ||
                value.Type != TriggerValueType.Integer ||
                value.IntegerValue <= 0 || value.IntegerValue > int.MaxValue)
                AddError(
                    diagnostics,
                    "TRG1316",
                    path + ".arguments.max_iterations",
                    "for_each 的 max_iterations 必须是大于 0 的整数常量。");
        }

        private static void ValidateRepeatAndUntilLimits(
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            TriggerNodeData node,
            IReadOnlyDictionary<string, TriggerArgumentData> arguments,
            string path)
        {
            if (node.Kind != TriggerNodeKind.Action) return;
            var isRepeat = string.Equals(node.Type, "repeat", StringComparison.Ordinal);
            var isUntil = string.Equals(node.Type, "until", StringComparison.Ordinal);
            if (!isRepeat && !isUntil) return;

            var parameterName = isRepeat ? "count" : "max_iterations";
            if (!arguments.TryGetValue(parameterName, out var argument) || argument?.Value == null) return;
            var value = argument.Value;
            if (value.Source == TriggerValueSource.Constant &&
                value.Type == TriggerValueType.Integer &&
                value.IntegerValue > 0 && value.IntegerValue <= int.MaxValue)
                return;

            AddError(
                diagnostics,
                isRepeat ? "TRG1317" : "TRG1318",
                path + ".arguments." + parameterName,
                node.Type + " 的 " + parameterName + " 必须是大于 0 的整数常量。");
        }

        private static bool IsSetVariableType(TriggerValueType type)
        {
            return type == TriggerValueType.Integer || type == TriggerValueType.Number ||
                   type == TriggerValueType.Boolean || type == TriggerValueType.String ||
                   type == TriggerValueType.Entity || type == TriggerValueType.ObjectId;
        }

        private static bool IsSetVariableTypeCompatible(TriggerValueType target, TriggerValueType value)
        {
            var targetIsNumeric = target == TriggerValueType.Integer || target == TriggerValueType.Number;
            var valueIsNumeric = value == TriggerValueType.Integer || value == TriggerValueType.Number;
            return target == value || targetIsNumeric && valueIsNumeric;
        }

        private static void ValidateValue(
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            TriggerValueRefData value,
            TriggerParameterDescriptor parameter,
            string path,
            IReadOnlyDictionary<string, BlackboardSymbol> localKeys,
            TriggerEventDefinitionData eventDefinition,
            TriggerGlobalBlackboardDescriptorCatalog globalBlackboard,
            TriggerAuthoringReferenceCatalog references)
        {
            if (value == null)
            {
                AddError(diagnostics, "TRG1300", path, "必须设置值。");
                return;
            }
            if (parameter.Type != TriggerValueType.None && !IsTypeCompatible(parameter.Type, value.Type))
                AddError(diagnostics, "TRG1301", path + ".type", $"值类型应为 {parameter.Type}，当前为 {value.Type}。");
            if ((parameter.AllowedSources & ToMask(value.Source)) == 0)
                AddError(diagnostics, "TRG1302", path + ".source", $"不允许使用值来源 {value.Source}。");
            if (value.Source == TriggerValueSource.Constant && value.Type == TriggerValueType.Object)
            {
                ValidateObjectFields(
                    diagnostics,
                    value.Fields,
                    parameter,
                    path + ".fields",
                    localKeys,
                    eventDefinition,
                    globalBlackboard,
                    references);
            }

            ValidateSemanticConstant(diagnostics, value, parameter, path, references);

            switch (value.Source)
            {
                case TriggerValueSource.Context:
                case TriggerValueSource.TemplateParameter:
                    if (string.IsNullOrWhiteSpace(value.Path))
                        AddError(diagnostics, "TRG1303", path + ".path", "必须填写引用路径。");
                    break;
                case TriggerValueSource.Payload:
                    ValidatePayloadValue(diagnostics, value, path, eventDefinition);
                    break;
                case TriggerValueSource.LocalBlackboard:
                    if (string.IsNullOrWhiteSpace(value.Path) || !localKeys.TryGetValue(value.Path, out var local))
                        AddError(diagnostics, "TRG1304", path + ".path", $"未知局部黑板 Key：{value.Path ?? string.Empty}。");
                    else
                    {
                        if (!IsTypeCompatible(local.Type, value.Type))
                            AddError(diagnostics, "TRG1309", path + ".type", $"局部黑板 Key“{value.Path}”的类型为 {local.Type}，当前值类型为 {value.Type}。");
                        if (TriggerParameterAccessRules.IsWrite(parameter.Access) && local.ReadOnly)
                            AddError(diagnostics, "TRG1314", path + ".path", $"局部黑板 Key“{value.Path}”为只读。");
                    }
                    break;
                case TriggerValueSource.GlobalBlackboard:
                    if (string.IsNullOrWhiteSpace(value.Path))
                        AddError(diagnostics, "TRG1305", path + ".path", "必须填写全局黑板 Key。");
                    else if (globalBlackboard != null)
                        ValidateGlobalBlackboardValue(diagnostics, value, parameter.Access, path, globalBlackboard);
                    break;
                case TriggerValueSource.Expression:
                    if (!TriggerAuthoringValueRefEditor.TryValidateExpression(value.Expression, out var expressionError))
                        AddError(diagnostics, "TRG1325", path + ".expression", expressionError);
                    break;
            }
        }

        private static void ValidateSemanticConstant(
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            TriggerValueRefData value,
            TriggerParameterDescriptor parameter,
            string path,
            TriggerAuthoringReferenceCatalog references)
        {
            if (value.Source != TriggerValueSource.Constant ||
                string.IsNullOrWhiteSpace(parameter.SemanticId) ||
                (parameter.SemanticPolicy & TriggerParameterSemanticPolicy.ValidateConstant) == 0 ||
                references == null ||
                !references.TryGetProvider(parameter.SemanticId, parameter.Type, out _))
                return;

            if (value.Type == TriggerValueType.Integer)
            {
                ValidateSemanticId(
                    diagnostics,
                    parameter,
                    path + ".integerValue",
                    value.IntegerValue,
                    references);
                return;
            }
            if (value.Type != TriggerValueType.IntegerList || value.IntegerListValue == null) return;
            for (var i = 0; i < value.IntegerListValue.Count; i++)
            {
                ValidateSemanticId(
                    diagnostics,
                    parameter,
                    path + ".integerListValue[" + i + "]",
                    value.IntegerListValue[i],
                    references);
            }
        }

        private static void ValidateSemanticId(
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            TriggerParameterDescriptor parameter,
            string path,
            long value,
            TriggerAuthoringReferenceCatalog references)
        {
            if (references.TryResolve(parameter.SemanticId, parameter.Type, value, out _) ||
                !references.IsOperational(parameter.SemanticId))
                return;
            AddError(
                diagnostics,
                "TRG1326",
                path,
                $"未找到语义引用 {parameter.SemanticId}：{value}。");
        }

        private static void ValidateObjectFields(
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            IReadOnlyList<TriggerArgumentData> fields,
            TriggerParameterDescriptor parameter,
            string path,
            IReadOnlyDictionary<string, BlackboardSymbol> localKeys,
            TriggerEventDefinitionData eventDefinition,
            TriggerGlobalBlackboardDescriptorCatalog globalBlackboard,
            TriggerAuthoringReferenceCatalog references)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            var fieldDescriptors = BuildFieldDescriptorMap(parameter);
            fields = fields ?? Array.Empty<TriggerArgumentData>();
            for (var i = 0; i < fields.Count; i++)
            {
                var field = fields[i];
                var fieldPath = $"{path}[{i}]";
                if (field == null || string.IsNullOrWhiteSpace(field.Name))
                {
                    AddError(diagnostics, "TRG1320", fieldPath + ".name", "必须填写对象字段名称。");
                    continue;
                }
                if (!names.Add(field.Name))
                    AddError(diagnostics, "TRG1321", fieldPath + ".name", $"对象字段重复：{field.Name}。");
                var fieldParameter = fieldDescriptors != null && fieldDescriptors.TryGetValue(field.Name, out var descriptor)
                    ? descriptor
                    : new TriggerParameterDescriptor(field.Name, TriggerValueType.None);
                if (fieldDescriptors != null && !fieldDescriptors.ContainsKey(field.Name))
                    AddWarning(diagnostics, "TRG1323", fieldPath + ".name",
                        $"未知对象字段“{field.Name}”会继续保留，但编辑 Schema 将忽略该字段。");
                ValidateValue(
                    diagnostics,
                    field.Value,
                    fieldParameter,
                    fieldPath + ".value",
                    localKeys,
                    eventDefinition,
                    globalBlackboard,
                    references);
            }

            if (fieldDescriptors == null) return;
            for (var i = 0; i < parameter.Fields.Count; i++)
            {
                var field = parameter.Fields[i];
                if (field == null || string.IsNullOrWhiteSpace(field.Name) || !field.Required) continue;
                if (!names.Contains(field.Name))
                    AddError(diagnostics, "TRG1322", path, $"缺少必填对象字段：{field.Name}。");
            }

            var requiredGroups = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < parameter.Fields.Count; i++)
            {
                var field = parameter.Fields[i];
                if (field != null && !string.IsNullOrEmpty(field.RequiredGroup))
                    requiredGroups.Add(field.RequiredGroup);
            }
            foreach (var group in requiredGroups)
            {
                var found = false;
                var choices = new List<string>();
                for (var i = 0; i < parameter.Fields.Count; i++)
                {
                    var field = parameter.Fields[i];
                    if (field == null || !string.Equals(field.RequiredGroup, group, StringComparison.Ordinal)) continue;
                    choices.Add(field.Name);
                    if (names.Contains(field.Name)) found = true;
                }
                if (!found)
                    AddError(diagnostics, "TRG1324", path,
                        $"对象字段组“{group}”至少需要设置一项：{string.Join(", ", choices)}。");
            }
        }

        private static Dictionary<string, TriggerParameterDescriptor> BuildFieldDescriptorMap(
            TriggerParameterDescriptor parameter)
        {
            if (parameter == null || parameter.Fields == null || parameter.Fields.Count == 0) return null;
            var result = new Dictionary<string, TriggerParameterDescriptor>(StringComparer.Ordinal);
            for (var i = 0; i < parameter.Fields.Count; i++)
            {
                var field = parameter.Fields[i];
                if (field == null || string.IsNullOrWhiteSpace(field.Name) || result.ContainsKey(field.Name)) continue;
                result.Add(field.Name, field);
            }
            return result;
        }

        private static void ValidatePayloadValue(
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            TriggerValueRefData value,
            string path,
            TriggerEventDefinitionData eventDefinition)
        {
            if (string.IsNullOrWhiteSpace(value.Path))
            {
                AddError(diagnostics, "TRG1303", path + ".path", "必须填写引用路径。");
                return;
            }
            if (eventDefinition == null) return;

            var fields = eventDefinition.PayloadFields;
            TriggerPayloadFieldData field = null;
            if (fields != null)
            {
                for (var i = 0; i < fields.Count; i++)
                {
                    var candidate = fields[i];
                    if (candidate != null && string.Equals(candidate.Path, value.Path, StringComparison.Ordinal))
                    {
                        field = candidate;
                        break;
                    }
                }
            }

            if (field == null)
            {
                AddError(diagnostics, "TRG1307", path + ".path", $"事件“{eventDefinition.Id}”中不存在 Payload 字段“{value.Path}”。");
                return;
            }
            if (!IsTypeCompatible(field.Type, value.Type))
                AddError(diagnostics, "TRG1308", path + ".type", $"Payload 字段“{value.Path}”的类型为 {field.Type}，当前值类型为 {value.Type}。");
        }

        private static void ValidateGlobalBlackboardValue(
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            TriggerValueRefData value,
            TriggerParameterAccess access,
            string path,
            TriggerGlobalBlackboardDescriptorCatalog catalog)
        {
            if (!catalog.TryGet(value.Path, out var key))
            {
                AddError(diagnostics, "TRG1310", path + ".path", $"未知全局黑板 Key：{value.Path}。");
                return;
            }
            if (!IsTypeCompatible(key.Type, value.Type))
                AddError(diagnostics, "TRG1313", path + ".type", $"全局黑板 Key“{value.Path}”的类型为 {key.Type}，当前值类型为 {value.Type}。");
            if (access == TriggerParameterAccess.Read && !key.CanRead)
                AddError(diagnostics, "TRG1311", path + ".path", $"全局黑板 Key“{value.Path}”不可读。");
            if (TriggerParameterAccessRules.IsWrite(access) && !key.CanWrite)
                AddError(diagnostics, "TRG1312", path + ".path", $"全局黑板 Key“{value.Path}”为只读。");
        }

        private static bool IsTypeCompatible(TriggerValueType expected, TriggerValueType actual)
        {
            return expected == TriggerValueType.None || expected == actual ||
                   (expected == TriggerValueType.Number && actual == TriggerValueType.Integer);
        }

        private static TriggerValueSourceMask ToMask(TriggerValueSource source)
        {
            return (TriggerValueSourceMask)(1 << (int)source);
        }

        private static string KindLabel(TriggerNodeKind kind)
        {
            return kind == TriggerNodeKind.Condition ? "条件" : "行为";
        }

        private static void AddError(
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            string code,
            string path,
            string message)
        {
            diagnostics.Add(new TriggerAuthoringDiagnostic(code, TriggerAuthoringDiagnosticSeverity.Error, path, message));
        }

        private static void AddWarning(
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            string code,
            string path,
            string message)
        {
            diagnostics.Add(new TriggerAuthoringDiagnostic(code, TriggerAuthoringDiagnosticSeverity.Warning, path, message));
        }
    }
}
