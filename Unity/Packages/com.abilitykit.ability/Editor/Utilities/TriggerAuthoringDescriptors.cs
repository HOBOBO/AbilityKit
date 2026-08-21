using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config.Authoring;

namespace AbilityKit.Ability.Editor.Utilities
{
    internal sealed class TriggerParameterOption
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
    internal enum TriggerValueSourceMask
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

    internal sealed class TriggerParameterDescriptor
    {
        public TriggerParameterDescriptor(
            string name,
            TriggerValueType type,
            bool required = true,
            TriggerValueSourceMask allowedSources = TriggerValueSourceMask.All,
            TriggerParameterAccess access = TriggerParameterAccess.Read,
            string requiredGroup = null,
            params TriggerParameterOption[] options)
        {
            Name = name ?? string.Empty;
            Type = type;
            Required = required;
            AllowedSources = allowedSources;
            Access = access;
            RequiredGroup = requiredGroup ?? string.Empty;
            Options = options ?? Array.Empty<TriggerParameterOption>();
        }

        public string Name { get; }
        public TriggerValueType Type { get; }
        public bool Required { get; }
        public TriggerValueSourceMask AllowedSources { get; }
        public TriggerParameterAccess Access { get; }
        public string RequiredGroup { get; }
        public IReadOnlyList<TriggerParameterOption> Options { get; }
    }

    internal enum TriggerParameterAccess
    {
        Read = 0,
        Write = 1
    }

    internal sealed class TriggerTypeDescriptor
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

    internal sealed class TriggerTypeDescriptorCatalog
    {
        private readonly Dictionary<string, TriggerTypeDescriptor> _entries =
            new Dictionary<string, TriggerTypeDescriptor>(StringComparer.Ordinal);

        public void Register(TriggerTypeDescriptor descriptor)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            if (string.IsNullOrWhiteSpace(descriptor.Type))
                throw new ArgumentException("Descriptor type is required.", nameof(descriptor));

            _entries[BuildKey(descriptor.Kind, descriptor.Type)] = descriptor;
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

        public static TriggerTypeDescriptorCatalog CreateProjectDefaults()
        {
            var catalog = new TriggerTypeDescriptorCatalog();
            RegisterCompleteProjectTypes(catalog);
            return catalog;
        }

        private static void RegisterCompleteProjectTypes(TriggerTypeDescriptorCatalog catalog)
        {
            RegisterCompleteConditions(catalog);
            RegisterCompleteActions(catalog);
        }

        private static void RegisterCompleteConditions(TriggerTypeDescriptorCatalog catalog)
        {
            catalog.Register(new TriggerTypeDescriptor(TriggerNodeKind.Condition, "all", "All", "Condition/Composite", 1, -1));
            catalog.Register(new TriggerTypeDescriptor(TriggerNodeKind.Condition, "any", "Any", "Condition/Composite", 1, -1));
            catalog.Register(new TriggerTypeDescriptor(TriggerNodeKind.Condition, "not", "Not", "Condition/Composite", 1, 1));
            catalog.Register(Condition("always_true", "Always True", "Condition/Constant"));
            catalog.Register(Condition("always_false", "Always False", "Condition/Constant"));

            catalog.Register(Condition("arg_eq", "Equals", "Condition/Compare",
                Required("left", TriggerValueType.None), Required("right", TriggerValueType.None)));
            catalog.Register(Condition("arg_neq", "Not Equals", "Condition/Compare",
                Required("left", TriggerValueType.None), Required("right", TriggerValueType.None)));
            RegisterNumericComparison(catalog, "arg_gt", "Greater Than");
            RegisterNumericComparison(catalog, "arg_gte", "Greater Than Or Equal");
            RegisterNumericComparison(catalog, "arg_geq", "Greater Than Or Equal (Alias)");
            RegisterNumericComparison(catalog, "arg_lt", "Less Than");
            RegisterNumericComparison(catalog, "arg_lte", "Less Than Or Equal");
            RegisterNumericComparison(catalog, "arg_leq", "Less Than Or Equal (Alias)");

            RegisterNumericVariableComparison(catalog, "num_var_gt", "Numeric Variable Greater Than");
            RegisterNumericVariableComparison(catalog, "num_var_lt", "Numeric Variable Less Than");
            RegisterNumericVariableComparison(catalog, "num_var_eq", "Numeric Variable Equals");

            catalog.Register(Condition("has_buff", "Has Buff", "Condition/Combat",
                Required("buff_id", TriggerValueType.Integer),
                Optional("check_stack", TriggerValueType.Boolean),
                Choice("target_mode", false, Option(0, "Target"), Option(1, "Source"))));
            catalog.Register(Condition("health_percent", "Health Percent", "Condition/Combat",
                Required("threshold", TriggerValueType.Number),
                Choice("compare_type", false, Option(0, "Less Than"), Option(1, "Greater Than"))));
            catalog.Register(Condition("owner_matches_payload_source", "Owner Matches Payload Source", "Condition/Context"));
            catalog.Register(Condition("owner_matches_payload_target", "Owner Matches Payload Target", "Condition/Context"));
            catalog.Register(Condition("target_is_flying_projectile", "Target Is Flying Projectile", "Condition/Context"));
        }

        private static void RegisterNumericComparison(
            TriggerTypeDescriptorCatalog catalog,
            string type,
            string displayName)
        {
            catalog.Register(Condition(type, displayName, "Condition/Compare",
                Required("left", TriggerValueType.Number), Required("right", TriggerValueType.Number)));
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
            catalog.Register(new TriggerTypeDescriptor(TriggerNodeKind.Action, "seq", "Sequence", "Action/Flow", 1, -1));
            catalog.Register(Action("debug_log", "Debug Log", "Action/Debug",
                Required("message", TriggerValueType.String),
                Optional("dump_args", TriggerValueType.Boolean)));
            catalog.Register(Action("set_var", "Set Variable", "Action/Variable",
                Writable("target", TriggerValueType.None), Required("value", TriggerValueType.None)));
            catalog.Register(Action("set_num_var", "Set Numeric Variable", "Action/Variable",
                Writable("target", TriggerValueType.Number), Required("value", TriggerValueType.Number)));
            catalog.Register(Action("add_num_var", "Add Numeric Variable", "Action/Variable",
                Writable("target", TriggerValueType.Number), Required("value", TriggerValueType.Number)));
            catalog.Register(AuthoringOnlyAction("attr_effect_duration", "Attribute Effect Duration", "Action/Attribute",
                Required("attr", TriggerValueType.String),
                Required("op", TriggerValueType.String),
                Required("value", TriggerValueType.Number),
                Optional("source_id", TriggerValueType.Integer),
                Optional("duration", TriggerValueType.Number)));

            RegisterCombatActions(catalog);
            RegisterBuffAndShieldActions(catalog);
            RegisterResourceActions(catalog);
            RegisterSpawnAndSkillActions(catalog);
            RegisterMotionActions(catalog);
            RegisterPresentationAndGameplayActions(catalog);
        }

        private static void RegisterCombatActions(TriggerTypeDescriptorCatalog catalog)
        {
            catalog.Register(Action("give_damage", "Give Damage", "Action/Combat", WithTargets(
                OneOf("damage_amount", "damage_value", TriggerValueType.Number),
                OneOf("damage_amount", "source_attack_ratio", TriggerValueType.Number),
                DamageType("damage_type"),
                DamageReason("reason_kind"),
                Optional("reason_param", TriggerValueType.Integer),
                Choice("attribute_source", false, Option(0, "Attribution Actor"), Option(1, "Trigger Owner")))));
            catalog.Register(Action("adjust_damage_number", "Adjust Damage Number", "Action/Combat",
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
            catalog.Register(Action("take_damage", "Take Damage", "Action/Combat",
                Optional("rate", TriggerValueType.Number),
                Optional("reason_param", TriggerValueType.Integer)));
            catalog.Register(Action("heal", "Heal", "Action/Combat", WithTargets(
                Required("amount", TriggerValueType.Number),
                DamageType("heal_type"),
                DamageReason("reason_kind"),
                Optional("reason_param", TriggerValueType.Integer))));
        }

        private static void RegisterBuffAndShieldActions(TriggerTypeDescriptorCatalog catalog)
        {
            catalog.Register(Action("add_buff", "Add Buff", "Action/Buff", WithTargets(
                Required("buff_ids", TriggerValueType.IntegerList))));
            catalog.Register(Action("remove_buff", "Remove Buff", "Action/Buff", WithTargets(
                Optional("buff_id", TriggerValueType.Integer),
                Optional("source_actor_id", TriggerValueType.Integer),
                Optional("remove_all", TriggerValueType.Boolean),
                Optional("remove_slow", TriggerValueType.Boolean),
                Optional("reason", TriggerValueType.Integer))));

            catalog.Register(Action("add_shield", "Add Shield", "Action/Shield", WithTargets(
                Optional("shield_id", TriggerValueType.Integer),
                Required("shield_value", TriggerValueType.Number),
                Optional("absorb_ratio", TriggerValueType.Number),
                Optional("priority", TriggerValueType.Integer),
                Optional("damage_type_mask", TriggerValueType.Integer),
                Optional("duration_frames", TriggerValueType.Integer),
                Optional("duration_ms", TriggerValueType.Integer),
                Choice("stacking_policy", false,
                    Option(0, "Independent"), Option(1, "Merge Same Shield/Source"),
                    Option(2, "Refresh Same Shield/Source"), Option(3, "Replace Lower Priority")),
                Choice("consume_policy", false,
                    Option(0, "Priority Then Oldest"), Option(1, "Priority Then Newest"),
                    Option(2, "Oldest First"), Option(3, "Newest First")))));
            catalog.Register(Action("remove_shield", "Remove Shield", "Action/Shield", WithTargets(
                OneOf("shield_identity", "shield_id", TriggerValueType.Integer),
                OneOf("shield_identity", "instance_id", TriggerValueType.Integer),
                Optional("source_actor_id", TriggerValueType.Integer),
                Optional("remove_all", TriggerValueType.Boolean))));
        }

        private static void RegisterResourceActions(TriggerTypeDescriptorCatalog catalog)
        {
            catalog.Register(Action("modify_resource", "Modify Resource", "Action/Resource", WithTargets(
                Required("amount", TriggerValueType.Number),
                ResourceType("resource_type"),
                Optional("min", TriggerValueType.Number),
                Optional("max", TriggerValueType.Number))));
            catalog.Register(Action("consume_resource", "Consume Resource", "Action/Resource",
                Optional("amount", TriggerValueType.Number), ResourceType("resource_type")));
            catalog.Register(Action("convert_resource_to_heal", "Convert Resource To Heal", "Action/Resource", WithTargets(
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
            catalog.Register(Action("shoot_projectile", "Shoot Projectile", "Action/Projectile", WithTargets(
                Required("launcher_id", TriggerValueType.Integer),
                Required("projectile_id", TriggerValueType.Integer),
                Optional("continuous_process_id", TriggerValueType.Integer),
                Optional("track_target", TriggerValueType.Boolean))));
            catalog.Register(Action("remove_projectile", "Remove Projectile", "Action/Projectile"));

            catalog.Register(Action("spawn_summon", "Spawn Summon", "Action/Summon",
                Required("summon_id", TriggerValueType.Integer),
                Optional("position_mode", TriggerValueType.Integer),
                Optional("rotation_mode", TriggerValueType.Integer),
                Optional("interval_ms", TriggerValueType.Number),
                Optional("duration_ms", TriggerValueType.Number),
                Optional("total_count", TriggerValueType.Integer),
                Optional("query_template_id", TriggerValueType.Integer),
                Optional("target_mode", TriggerValueType.Integer)));
            catalog.Register(Action("remove_summon", "Remove Summon", "Action/Summon", WithTargets(
                Optional("summon_id", TriggerValueType.Integer),
                Optional("summon_actor_id", TriggerValueType.Integer),
                Optional("root_owner_actor_id", TriggerValueType.Integer),
                Optional("remove_all", TriggerValueType.Boolean),
                Optional("reason", TriggerValueType.Integer))));

            catalog.Register(Action("spawn_area", "Spawn Area", "Action/Area", WithTargets(
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
            catalog.Register(Action("remove_area", "Remove Area", "Action/Area", WithTargets(
                OneOf("area_identity", "area_id", TriggerValueType.Integer),
                OneOf("area_identity", "template_id", TriggerValueType.Integer),
                OneOf("area_identity", "owner_actor_id", TriggerValueType.Integer),
                Optional("remove_all", TriggerValueType.Boolean))));

            catalog.Register(Action("cancel_skill", "Cancel Skill", "Action/Skill", WithTargets(
                Choice("mode", false, Option(0, "Auto"), Option(1, "All"), Option(2, "Slot"), Option(3, "Skill Id")),
                Optional("skill_id", TriggerValueType.Integer),
                Optional("skill_slot", TriggerValueType.Integer),
                Optional("remove_all", TriggerValueType.Boolean))));
            catalog.Register(Action("start_cooldown", "Start Cooldown", "Action/Skill",
                Optional("skill_id", TriggerValueType.Integer),
                Optional("skill_slot", TriggerValueType.Integer),
                Required("cooldown_ms", TriggerValueType.Integer)));
            catalog.Register(Action("reset_cooldown", "Reset Cooldown", "Action/Skill", WithTargets(
                OneOf("skill_identity", "skill_id", TriggerValueType.Integer),
                OneOf("skill_identity", "skill_slot", TriggerValueType.Integer))));
        }

        private static void RegisterMotionActions(TriggerTypeDescriptorCatalog catalog)
        {
            catalog.Register(Action("blink", "Blink", "Action/Motion",
                Optional("distance", TriggerValueType.Number),
                Optional("direction_mode", TriggerValueType.Integer),
                Optional("priority", TriggerValueType.Integer),
                Optional("apply_to_caster", TriggerValueType.Boolean),
                Optional("pass_through_walls", TriggerValueType.Boolean)));
            catalog.Register(Action("dash", "Dash", "Action/Motion", WithContinuous(
                Optional("speed", TriggerValueType.Number),
                Optional("duration_ms", TriggerValueType.Number),
                Optional("direction_mode", TriggerValueType.Integer),
                Optional("priority", TriggerValueType.Integer),
                Optional("apply_to_caster", TriggerValueType.Boolean),
                Optional("hit_trigger_plan_id", TriggerValueType.Integer),
                Optional("motion_group_id", TriggerValueType.Integer),
                Optional("move_to_aim_position", TriggerValueType.Boolean),
                Optional("pass_through_walls", TriggerValueType.Boolean))));
            catalog.Register(Action("jump", "Jump", "Action/Motion", WithContinuous(
                Optional("height", TriggerValueType.Number),
                Optional("duration_ms", TriggerValueType.Number),
                Optional("priority", TriggerValueType.Integer),
                Optional("apply_to_caster", TriggerValueType.Boolean),
                Optional("motion_group_id", TriggerValueType.Integer),
                Optional("landing_trigger_ids", TriggerValueType.IntegerList))));
            catalog.Register(Action("pull", "Pull", "Action/Motion", WithTargets(WithContinuous(
                Optional("speed", TriggerValueType.Number),
                Optional("duration_ms", TriggerValueType.Number),
                Optional("direction_mode", TriggerValueType.Integer),
                Optional("target_distance", TriggerValueType.Number),
                Optional("priority", TriggerValueType.Integer),
                Optional("motion_group_id", TriggerValueType.Integer)))));
        }

        private static void RegisterPresentationAndGameplayActions(TriggerTypeDescriptorCatalog catalog)
        {
            catalog.Register(Action("play_presentation", "Play Presentation", "Action/Presentation",
                Required("template_id", TriggerValueType.Integer),
                Optional("target_mode", TriggerValueType.Integer),
                Optional("duration_ms", TriggerValueType.Integer),
                Optional("stop", TriggerValueType.Boolean),
                Optional("x", TriggerValueType.Number),
                Optional("y", TriggerValueType.Number),
                Optional("z", TriggerValueType.Number),
                Optional("scale", TriggerValueType.Number),
                Optional("radius", TriggerValueType.Number)));
            catalog.Register(Action("emit", "Emit Presentation Event", "Action/Presentation",
                Required("emitter_id", TriggerValueType.Integer)));

            catalog.Register(Action("set_gameplay_var", "Set Gameplay Variable", "Action/Gameplay",
                Required("key_id", TriggerValueType.Integer), Optional("value", TriggerValueType.Number)));
            catalog.Register(Action("add_gameplay_var", "Add Gameplay Variable", "Action/Gameplay",
                Required("key_id", TriggerValueType.Integer), Optional("delta", TriggerValueType.Number)));
            catalog.Register(Action("advance_gameplay_counter", "Advance Gameplay Counter", "Action/Gameplay",
                Required("key_id", TriggerValueType.Integer),
                Required("scope_payload_field_id", TriggerValueType.Integer),
                Required("threshold", TriggerValueType.Number),
                Optional("delta", TriggerValueType.Number),
                Optional("reset_value", TriggerValueType.Number),
                Required("trigger_id", TriggerValueType.Integer)));
            catalog.Register(Action("end_game", "End Game", "Action/Gameplay",
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
                TriggerValueSourceMask.LocalBlackboard | TriggerValueSourceMask.GlobalBlackboard;
            return new TriggerParameterDescriptor(
                name, type, true, variables, TriggerParameterAccess.Write);
        }

        private static TriggerParameterDescriptor OneOf(string group, string name, TriggerValueType type)
        {
            return new TriggerParameterDescriptor(
                name, type, false, TriggerValueSourceMask.All, TriggerParameterAccess.Read, group);
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
                Option(0, "None"), Option(1, "Physical"), Option(2, "Magic"), Option(4, "True"));
        }

        private static TriggerParameterDescriptor DamageReason(string name)
        {
            return Choice(name, false,
                Option(0, "None"), Option(1, "Skill"), Option(2, "Basic Attack"),
                Option(3, "Buff"), Option(4, "Item"), Option(5, "Environment"));
        }

        private static TriggerParameterDescriptor ResourceType(string name)
        {
            return Choice(name, false,
                Option(0, "None"), Option(1, "HP"), Option(2, "Mana"), Option(3, "Rage"),
                Option(4, "Energy"), Option(5, "Ammo"), Option(6, "Combo Point"));
        }

        private static TriggerParameterDescriptor[] WithTargets(params TriggerParameterDescriptor[] parameters)
        {
            return Append(parameters, new[]
            {
                Optional("query_template_id", TriggerValueType.Integer),
                Optional("target_actor_id", TriggerValueType.Integer),
                Optional("target_payload_field_id", TriggerValueType.Integer),
                Choice("target_source", false,
                    Option(3, "Context Target"), Option(4, "Self"), Option(2, "Explicit Actor"),
                    Option(1, "All Actors"), Option(5, "Same Team"), Option(6, "Enemy Team"),
                    Option(7, "Main Type"), Option(8, "Unit Subtype"), Option(1000, "Query Template")),
                Optional("target_source_param", TriggerValueType.Integer),
                Choice("target_filter", false,
                    Option(0, "None"), Option(0x0204, "Require Valid Id"),
                    Option(0x0205, "Require Position"), Option(0x0101, "Circle"),
                    Option(0x0102, "Sector"), Option(0x0301, "Exclude Caster"),
                    Option(0x0302, "Exclude Context Target"), Option(0x0201, "Whitelist"),
                    Option(0x0202, "Blacklist")),
                Optional("target_filter_param", TriggerValueType.Integer),
                Optional("target_radius", TriggerValueType.Number),
                Optional("target_half_angle_deg", TriggerValueType.Number),
                Choice("target_order", false,
                    Option(0, "None"), Option(0x2001, "Zero"), Option(0x2002, "Random"),
                    Option(0x2004, "Distance To Caster"), Option(0x2005, "Distance To Context Target")),
                Optional("target_order_param", TriggerValueType.Integer),
                Choice("target_select", false,
                    Option(0x1001, "Top K"), Option(0x1002, "Streaming Top K")),
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
                AddError(diagnostics, "TRG1000", "module", "Module is null.");
                return diagnostics;
            }

            if (string.IsNullOrWhiteSpace(module.ModuleId))
                AddError(diagnostics, "TRG1001", "module.moduleId", "ModuleId is required.");

            if (context.Events == null)
                AddWarning(diagnostics, "TRG1402", "module", "No Event Catalog is assigned; event and Payload validation is limited.");

            var moduleKeys = ValidateBlackboard(diagnostics, module.Blackboard, "module.blackboard");
            ValidateGroups(
                diagnostics,
                module,
                module.ConditionGroups,
                TriggerNodeKind.Condition,
                "module.conditionGroups",
                catalog,
                moduleKeys,
                context.GlobalBlackboard);
            ValidateGroups(
                diagnostics,
                module,
                module.ActionGroups,
                TriggerNodeKind.Action,
                "module.actionGroups",
                catalog,
                moduleKeys,
                context.GlobalBlackboard);
            var triggerIds = new HashSet<int>();
            var triggers = module.Triggers ?? new List<TriggerDefinitionData>();
            for (var i = 0; i < triggers.Count; i++)
            {
                var trigger = triggers[i];
                var path = $"module.triggers[{i}]";
                if (trigger == null)
                {
                    AddError(diagnostics, "TRG1002", path, "Trigger is null.");
                    continue;
                }

                if (trigger.Id <= 0)
                    AddError(diagnostics, "TRG1003", path + ".id", "Trigger Id must be greater than zero.");
                else if (!triggerIds.Add(trigger.Id))
                    AddError(diagnostics, "TRG1004", path + ".id", $"Duplicate Trigger Id: {trigger.Id}.");

                if (string.IsNullOrWhiteSpace(trigger.Event))
                    AddError(diagnostics, "TRG1005", path + ".event", "Event is required.");

                TriggerEventDefinitionData eventDefinition = null;
                if (!string.IsNullOrWhiteSpace(trigger.Event) && context.Events != null &&
                    !context.Events.TryResolve(trigger.Event, out eventDefinition))
                {
                    AddError(diagnostics, "TRG1400", path + ".event", $"Unknown event: {trigger.Event}.");
                }
                else if (eventDefinition != null && trigger.AllowExternal && !eventDefinition.AllowExternal)
                {
                    AddError(diagnostics, "TRG1401", path + ".allowExternal", $"Event '{trigger.Event}' does not allow external dispatch.");
                }

                var triggerKeys = new Dictionary<string, BlackboardSymbol>(moduleKeys, StringComparer.Ordinal);
                var declaredTriggerKeys = ValidateBlackboard(diagnostics, trigger.Blackboard, path + ".blackboard");
                foreach (var pair in declaredTriggerKeys) triggerKeys[pair.Key] = pair.Value;
                ValidateTemplateReference(diagnostics, trigger, path, context, eventDefinition, triggerKeys);
                if (trigger.Template == null || trigger.Condition != null)
                    ValidateResolvedNode(diagnostics, module, trigger.Condition, TriggerNodeKind.Condition, path + ".condition", catalog, triggerKeys, eventDefinition, context.GlobalBlackboard);
                if (trigger.Template == null || trigger.Actions != null)
                    ValidateResolvedNode(diagnostics, module, trigger.Actions, TriggerNodeKind.Action, path + ".actions", catalog, triggerKeys, eventDefinition, context.GlobalBlackboard);
            }

            return diagnostics;
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
                    AddError(diagnostics, "TRG1605", bindingPath + ".name", "Template binding name is required.");
                    continue;
                }
                if (!seen.Add(binding.Name))
                {
                    AddError(diagnostics, "TRG1606", bindingPath + ".name", $"Duplicate template binding: {binding.Name}.");
                    continue;
                }
                if (!parameters.TryGetValue(binding.Name, out var parameter))
                {
                    AddError(diagnostics, "TRG1605", bindingPath + ".name", $"Unknown template parameter: {binding.Name}.");
                    continue;
                }
                if (binding.Value == null)
                {
                    AddError(diagnostics, "TRG1607", bindingPath + ".value", "Template binding value is required.");
                    continue;
                }
                if (!TriggerAuthoringTemplateValidator.IsTypeCompatible(parameter.Type, binding.Value.Type))
                    AddError(diagnostics, "TRG1608", bindingPath + ".value.type", $"Template parameter '{parameter.Name}' expects {parameter.Type}, got {binding.Value.Type}.");
                var sourceMask = (TriggerTemplateValueSourceMask)(1 << (int)binding.Value.Source);
                if ((parameter.AllowedSources & sourceMask) == 0)
                    AddError(diagnostics, "TRG1609", bindingPath + ".value.source", $"Source {binding.Value.Source} is not allowed for template parameter '{parameter.Name}'.");
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
                    context.GlobalBlackboard);
            }

            foreach (var pair in parameters)
            {
                var parameter = pair.Value;
                if (!parameter.Required || parameter.HasDefault || seen.Contains(parameter.Name)) continue;
                AddError(
                    diagnostics,
                    "TRG1607",
                    path + ".template.bindings",
                    $"Required template parameter has no binding: {parameter.Name}.");
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
            TriggerGlobalBlackboardDescriptorCatalog globalBlackboard)
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
                    AddError(diagnostics, "TRG1500", groupPath, $"{kind} group is null.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(group.Id))
                    AddError(diagnostics, "TRG1501", groupPath + ".id", $"{kind} group id is required.");
                else if (!ids.Add(group.Id))
                {
                    duplicateIds.Add(group.Id);
                    AddError(diagnostics, "TRG1502", groupPath + ".id", $"Duplicate {kind} group id: {group.Id}.");
                }
                if (group.Root == null)
                    AddError(diagnostics, "TRG1503", groupPath + ".root", $"{kind} group root is required.");
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
                    globalBlackboard);
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
            TriggerGlobalBlackboardDescriptorCatalog globalBlackboard)
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
                    failure != null ? failure.Message : $"Unable to resolve {expectedKind} group reference.");
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
                globalBlackboard);
        }

        private static void ValidateReferenceShape(
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            TriggerNodeData node,
            TriggerNodeKind expectedKind,
            string path)
        {
            if (node == null) return;
            if (!string.IsNullOrWhiteSpace(node.GroupReference))
            {
                if (node.Kind != expectedKind)
                    AddError(diagnostics, "TRG1201", path + ".kind", $"Expected {expectedKind}, got {node.Kind}.");
                if (!string.IsNullOrWhiteSpace(node.Type) ||
                    node.Arguments != null && node.Arguments.Count > 0 ||
                    node.Children != null && node.Children.Count > 0)
                {
                    AddError(
                        diagnostics,
                        "TRG1507",
                        path,
                        "A group reference node cannot also contain a type, arguments, or children.");
                }
                return;
            }

            if (node.Children == null) return;
            for (var i = 0; i < node.Children.Count; i++)
                ValidateReferenceShape(diagnostics, node.Children[i], expectedKind, $"{path}.children[{i}]");
        }

        private sealed class BlackboardSymbol
        {
            public TriggerValueType Type;
            public bool ReadOnly;
        }

        private static Dictionary<string, BlackboardSymbol> ValidateBlackboard(
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            IReadOnlyList<TriggerBlackboardVariableData> variables,
            string path)
        {
            var keys = new Dictionary<string, BlackboardSymbol>(StringComparer.Ordinal);
            if (variables == null) return keys;
            for (var i = 0; i < variables.Count; i++)
            {
                var variable = variables[i];
                var itemPath = $"{path}[{i}]";
                if (variable == null || string.IsNullOrWhiteSpace(variable.Key))
                {
                    AddError(diagnostics, "TRG1100", itemPath + ".key", "Blackboard key is required.");
                    continue;
                }
                if (keys.ContainsKey(variable.Key))
                    AddError(diagnostics, "TRG1101", itemPath + ".key", $"Duplicate Blackboard key: {variable.Key}.");
                else
                    keys.Add(variable.Key, new BlackboardSymbol { Type = variable.Type, ReadOnly = variable.ReadOnly });
                if (variable.Type == TriggerValueType.None)
                    AddError(diagnostics, "TRG1102", itemPath + ".type", "Blackboard value type is required.");
            }
            return keys;
        }

        private static void ValidateNode(
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            TriggerNodeData node,
            TriggerNodeKind expectedKind,
            string path,
            TriggerTypeDescriptorCatalog catalog,
            IReadOnlyDictionary<string, BlackboardSymbol> localKeys,
            TriggerEventDefinitionData eventDefinition,
            TriggerGlobalBlackboardDescriptorCatalog globalBlackboard)
        {
            if (node == null)
            {
                if (expectedKind == TriggerNodeKind.Action)
                    AddError(diagnostics, "TRG1200", path, "Action root is required.");
                return;
            }
            if (node.Kind != expectedKind)
                AddError(diagnostics, "TRG1201", path + ".kind", $"Expected {expectedKind}, got {node.Kind}.");
            if (string.IsNullOrWhiteSpace(node.Type))
            {
                AddError(diagnostics, "TRG1202", path + ".type", "Node type is required.");
                return;
            }
            if (!catalog.TryGet(expectedKind, node.Type, out var descriptor))
            {
                AddError(diagnostics, "TRG1203", path + ".type", $"Unknown {expectedKind} type: {node.Type}.");
                return;
            }

            var children = node.Children ?? new List<TriggerNodeData>();
            if (children.Count < descriptor.MinChildren)
                AddError(diagnostics, "TRG1204", path + ".children", $"Node requires at least {descriptor.MinChildren} child nodes.");
            if (descriptor.MaxChildren >= 0 && children.Count > descriptor.MaxChildren)
                AddError(diagnostics, "TRG1205", path + ".children", $"Node allows at most {descriptor.MaxChildren} child nodes.");

            var arguments = new Dictionary<string, TriggerArgumentData>(StringComparer.Ordinal);
            var nodeArguments = node.Arguments ?? new List<TriggerArgumentData>();
            for (var i = 0; i < nodeArguments.Count; i++)
            {
                var argument = nodeArguments[i];
                var argumentPath = $"{path}.arguments[{i}]";
                if (argument == null || string.IsNullOrWhiteSpace(argument.Name))
                {
                    AddError(diagnostics, "TRG1210", argumentPath + ".name", "Argument name is required.");
                    continue;
                }
                if (arguments.ContainsKey(argument.Name))
                    AddError(diagnostics, "TRG1211", argumentPath + ".name", $"Duplicate argument: {argument.Name}.");
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
                    if (!known)
                        AddWarning(diagnostics, "TRG1214", argumentPath + ".name",
                            $"Unknown argument '{argument.Name}' is preserved but ignored by the authoring Schema.");
                }
            }

            for (var i = 0; i < descriptor.Parameters.Count; i++)
            {
                var parameter = descriptor.Parameters[i];
                if (!arguments.TryGetValue(parameter.Name, out var argument))
                {
                    if (parameter.Required)
                        AddError(diagnostics, "TRG1212", path + ".arguments", $"Required argument is missing: {parameter.Name}.");
                    continue;
                }
                ValidateValue(
                    diagnostics,
                    argument.Value,
                    parameter,
                    path + ".arguments." + parameter.Name,
                    localKeys,
                    eventDefinition,
                    globalBlackboard);
            }

            ValidateSetVariableTypes(diagnostics, node, arguments, path);

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
                        $"At least one argument is required for '{group}': {string.Join(", ", choices)}.");
            }

            for (var i = 0; i < children.Count; i++)
                ValidateNode(diagnostics, children[i], expectedKind, $"{path}.children[{i}]", catalog, localKeys, eventDefinition, globalBlackboard);
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
                    $"set_var target type must be numeric, Boolean, or String; got {targetType}.");
                return;
            }
            if (!IsSetVariableType(valueType))
            {
                AddError(diagnostics, "TRG1315", path + ".arguments.value.type",
                    $"set_var value type must be numeric, Boolean, or String; got {valueType}.");
                return;
            }
            if (!IsSetVariableTypeCompatible(targetType, valueType))
                AddError(diagnostics, "TRG1315", path + ".arguments.value.type",
                    $"set_var target type {targetType} does not match value type {valueType}.");
        }

        private static bool IsSetVariableType(TriggerValueType type)
        {
            return type == TriggerValueType.Integer || type == TriggerValueType.Number ||
                   type == TriggerValueType.Boolean || type == TriggerValueType.String;
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
            TriggerGlobalBlackboardDescriptorCatalog globalBlackboard)
        {
            if (value == null)
            {
                AddError(diagnostics, "TRG1300", path, "Value is required.");
                return;
            }
            if (parameter.Type != TriggerValueType.None && !IsTypeCompatible(parameter.Type, value.Type))
                AddError(diagnostics, "TRG1301", path + ".type", $"Expected {parameter.Type}, got {value.Type}.");
            if ((parameter.AllowedSources & ToMask(value.Source)) == 0)
                AddError(diagnostics, "TRG1302", path + ".source", $"Source {value.Source} is not allowed.");

            switch (value.Source)
            {
                case TriggerValueSource.Context:
                case TriggerValueSource.TemplateParameter:
                    if (string.IsNullOrWhiteSpace(value.Path))
                        AddError(diagnostics, "TRG1303", path + ".path", "Reference path is required.");
                    break;
                case TriggerValueSource.Payload:
                    ValidatePayloadValue(diagnostics, value, path, eventDefinition);
                    break;
                case TriggerValueSource.LocalBlackboard:
                    if (string.IsNullOrWhiteSpace(value.Path) || !localKeys.TryGetValue(value.Path, out var local))
                        AddError(diagnostics, "TRG1304", path + ".path", $"Unknown local Blackboard key: {value.Path ?? string.Empty}.");
                    else
                    {
                        if (!IsTypeCompatible(local.Type, value.Type))
                            AddError(diagnostics, "TRG1309", path + ".type", $"Local Blackboard key '{value.Path}' is {local.Type}, got {value.Type}.");
                        if (parameter.Access == TriggerParameterAccess.Write && local.ReadOnly)
                            AddError(diagnostics, "TRG1314", path + ".path", $"Local Blackboard key '{value.Path}' is read-only.");
                    }
                    break;
                case TriggerValueSource.GlobalBlackboard:
                    if (string.IsNullOrWhiteSpace(value.Path))
                        AddError(diagnostics, "TRG1305", path + ".path", "Global Blackboard key is required.");
                    else if (globalBlackboard != null)
                        ValidateGlobalBlackboardValue(diagnostics, value, parameter.Access, path, globalBlackboard);
                    break;
                case TriggerValueSource.Expression:
                    if (string.IsNullOrWhiteSpace(value.Expression))
                        AddError(diagnostics, "TRG1306", path + ".expression", "Expression is required.");
                    break;
            }
        }

        private static void ValidatePayloadValue(
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            TriggerValueRefData value,
            string path,
            TriggerEventDefinitionData eventDefinition)
        {
            if (string.IsNullOrWhiteSpace(value.Path))
            {
                AddError(diagnostics, "TRG1303", path + ".path", "Reference path is required.");
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
                AddError(diagnostics, "TRG1307", path + ".path", $"Event '{eventDefinition.Id}' has no Payload field '{value.Path}'.");
                return;
            }
            if (!IsTypeCompatible(field.Type, value.Type))
                AddError(diagnostics, "TRG1308", path + ".type", $"Payload field '{value.Path}' is {field.Type}, got {value.Type}.");
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
                AddError(diagnostics, "TRG1310", path + ".path", $"Unknown global Blackboard key: {value.Path}.");
                return;
            }
            if (!IsTypeCompatible(key.Type, value.Type))
                AddError(diagnostics, "TRG1313", path + ".type", $"Global Blackboard key '{value.Path}' is {key.Type}, got {value.Type}.");
            if (access == TriggerParameterAccess.Read && !key.CanRead)
                AddError(diagnostics, "TRG1311", path + ".path", $"Global Blackboard key '{value.Path}' is not readable.");
            if (access == TriggerParameterAccess.Write && !key.CanWrite)
                AddError(diagnostics, "TRG1312", path + ".path", $"Global Blackboard key '{value.Path}' is read-only.");
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
