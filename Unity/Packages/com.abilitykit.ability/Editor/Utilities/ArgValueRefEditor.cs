using System.Collections.Generic;
using AbilityKit.Ability.Config.Authoring;
using AbilityKit.Triggering.Variables.Numeric.Expression;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Utilities
{
    internal readonly struct TriggerAuthoringExpressionReference
    {
        public TriggerAuthoringExpressionReference(
            string expression,
            string label,
            TriggerValueType type)
        {
            Expression = expression ?? string.Empty;
            Label = string.IsNullOrWhiteSpace(label) ? Expression : label;
            Type = type;
        }

        public string Expression { get; }
        public string Label { get; }
        public TriggerValueType Type { get; }
    }

    internal static class TriggerAuthoringExpressionFunctions
    {
        public static readonly string[] Names =
        {
            "插入函数...",
            "绝对值 abs",
            "最小值 min",
            "最大值 max",
            "限制范围 clamp",
            "向下取整 floor",
            "向上取整 ceil",
            "四舍五入 round",
            "幂 pow",
            "平方根 sqrt",
            "线性插值 lerp"
        };

        public static readonly string[] Snippets =
        {
            string.Empty,
            "abs()",
            "min(, )",
            "max(, )",
            "clamp(, , )",
            "floor()",
            "ceil()",
            "round()",
            "pow(, )",
            "sqrt()",
            "lerp(, , )"
        };
    }

    internal sealed class TriggerAuthoringValueRefEditorContext
    {
        public TriggerAuthoringModuleData Module;
        public TriggerDefinitionData Trigger;
        public TriggerEventDescriptorCatalog Events;
        public TriggerGlobalBlackboardDescriptorCatalog GlobalBlackboard;
        public IReadOnlyList<TriggerAuthoringTemplateParameterData> TemplateParameters;
        public TriggerAuthoringValueSourceCatalog ValueSources = TriggerAuthoringValueSourceCatalog.CreateForProject(null);
        public TriggerAuthoringReferenceCatalog References = TriggerAuthoringReferenceCatalog.CreateForProject(null);
        public System.Action<string, System.Action> ApplyChange;
        // Kept for callers that provide temporary context fields without a project extension.
        public IReadOnlyList<TriggerPayloadFieldData> ContextFields = System.Array.Empty<TriggerPayloadFieldData>();

        public TriggerEventDefinitionData ResolveEventDefinition()
        {
            if (Trigger == null || Events == null) return null;
            Events.TryResolve(Trigger.Event, out var definition);
            return definition;
        }
    }

    internal readonly struct TriggerAuthoringValuePathOption
    {
        public TriggerAuthoringValuePathOption(
            TriggerValueSource source,
            string path,
            TriggerValueType type,
            string label,
            bool canRead = true,
            bool canWrite = true,
            string unscopedPath = null)
        {
            Source = source;
            Path = path ?? string.Empty;
            Type = type;
            Label = string.IsNullOrWhiteSpace(label) ? Path : label;
            CanRead = canRead;
            CanWrite = canWrite;
            UnscopedPath = unscopedPath ?? Path;
        }

        public TriggerValueSource Source { get; }
        public string Path { get; }
        public TriggerValueType Type { get; }
        public string Label { get; }
        public bool CanRead { get; }
        public bool CanWrite { get; }
        public string UnscopedPath { get; }

        public bool MatchesPath(string path)
        {
            if (string.Equals(Path, path, System.StringComparison.Ordinal)) return true;
            if (Source != TriggerValueSource.LocalBlackboard ||
                TriggerAuthoringLocalBlackboardPath.HasExplicitScope(path))
                return false;
            return TriggerAuthoringLocalBlackboardPath.TryParse(path, out _, out var key) &&
                   string.Equals(UnscopedPath, key, System.StringComparison.Ordinal);
        }
    }

    internal enum TriggerAuthoringLocalBlackboardScope
    {
        Any = 0,
        Module = 1,
        Trigger = 2
    }

    internal static class TriggerAuthoringLocalBlackboardPath
    {
        private const string ModulePrefix = "module:";
        private const string TriggerPrefix = "trigger:";

        public static string Format(TriggerAuthoringLocalBlackboardScope scope, string key)
        {
            key = key ?? string.Empty;
            switch (scope)
            {
                case TriggerAuthoringLocalBlackboardScope.Module: return ModulePrefix + key;
                case TriggerAuthoringLocalBlackboardScope.Trigger: return TriggerPrefix + key;
                default: return key;
            }
        }

        public static bool TryParse(string path, out TriggerAuthoringLocalBlackboardScope scope, out string key)
        {
            scope = TriggerAuthoringLocalBlackboardScope.Any;
            key = path ?? string.Empty;
            if (key.StartsWith(ModulePrefix, System.StringComparison.Ordinal))
            {
                scope = TriggerAuthoringLocalBlackboardScope.Module;
                key = key.Substring(ModulePrefix.Length);
            }
            else if (key.StartsWith(TriggerPrefix, System.StringComparison.Ordinal))
            {
                scope = TriggerAuthoringLocalBlackboardScope.Trigger;
                key = key.Substring(TriggerPrefix.Length);
            }
            return !string.IsNullOrWhiteSpace(key);
        }

        public static bool HasExplicitScope(string path)
        {
            return !string.IsNullOrEmpty(path) &&
                   (path.StartsWith(ModulePrefix, System.StringComparison.Ordinal) ||
                    path.StartsWith(TriggerPrefix, System.StringComparison.Ordinal));
        }
    }

    internal static class TriggerAuthoringEditorLabels
    {
        public static string Source(TriggerValueSource source)
        {
            switch (source)
            {
                case TriggerValueSource.Constant: return "常量";
                case TriggerValueSource.Payload: return "事件参数";
                case TriggerValueSource.Context: return "运行上下文";
                case TriggerValueSource.LocalBlackboard: return "局部黑板";
                case TriggerValueSource.GlobalBlackboard: return "全局黑板";
                case TriggerValueSource.TemplateParameter: return "模板参数";
                case TriggerValueSource.Expression: return "表达式";
                default: return source.ToString();
            }
        }

        public static string ValueType(TriggerValueType type)
        {
            switch (type)
            {
                case TriggerValueType.None: return "自动推断";
                case TriggerValueType.Integer: return "整数";
                case TriggerValueType.Number: return "数值";
                case TriggerValueType.Boolean: return "布尔值";
                case TriggerValueType.String: return "文本";
                case TriggerValueType.Entity: return "实体";
                case TriggerValueType.ObjectId: return "对象 ID";
                case TriggerValueType.IntegerList: return "整数列表";
                case TriggerValueType.Vector3: return "三维向量";
                case TriggerValueType.Object: return "对象";
                default: return type.ToString();
            }
        }

        public static string ModuleKind(TriggerModuleKind kind)
        {
            switch (kind)
            {
                case TriggerModuleKind.Ability: return "技能";
                case TriggerModuleKind.Buff: return "增益效果";
                case TriggerModuleKind.Passive: return "被动效果";
                case TriggerModuleKind.Projectile: return "投射物";
                case TriggerModuleKind.Summon: return "召唤物";
                case TriggerModuleKind.Custom: return "自定义";
                default: return kind.ToString();
            }
        }

        public static string Node(string type, string fallback)
        {
            switch (type)
            {
                case "all": return "全部满足";
                case "any": return "任一满足";
                case "not": return "结果取反";
                case "seq": return "顺序执行";
                case "always_true": return "始终满足";
                case "always_false": return "始终不满足";
                case "arg_eq": return "参数等于";
                case "arg_neq": return "参数不等于";
                case "arg_gt": return "参数大于";
                case "arg_gte":
                case "arg_geq": return "参数大于或等于";
                case "arg_lt": return "参数小于";
                case "arg_lte":
                case "arg_leq": return "参数小于或等于";
                case "num_var_gt": return "数值变量大于";
                case "num_var_lt": return "数值变量小于";
                case "num_var_eq": return "数值变量等于";
                case "has_buff": return "拥有增益效果";
                case "health_percent": return "生命值百分比";
                case "owner_matches_payload_source": return "所有者匹配事件来源";
                case "owner_matches_payload_target": return "所有者匹配事件目标";
                case "target_is_flying_projectile": return "目标是飞行投射物";
                case "debug_log": return "输出调试日志";
                case "set_var": return "设置变量";
                case "set_num_var": return "设置数值变量";
                case "add_num_var": return "增加数值变量";
                case "attr_effect_duration": return "添加限时属性效果";
                case "give_damage": return "造成伤害";
                case "adjust_damage_number": return "调整伤害数值";
                case "take_damage": return "承受伤害";
                case "heal": return "治疗";
                case "add_buff": return "添加增益效果";
                case "remove_buff": return "移除增益效果";
                case "add_shield": return "添加护盾";
                case "remove_shield": return "移除护盾";
                case "modify_resource": return "修改资源";
                case "consume_resource": return "消耗资源";
                case "convert_resource_to_heal": return "将资源转为治疗";
                case "shoot_projectile": return "发射投射物";
                case "remove_projectile": return "移除投射物";
                case "spawn_summon": return "生成召唤物";
                case "remove_summon": return "移除召唤物";
                case "spawn_area": return "生成区域";
                case "remove_area": return "移除区域";
                case "cancel_skill": return "取消技能";
                case "start_cooldown": return "开始冷却";
                case "reset_cooldown": return "重置冷却";
                case "blink": return "闪现";
                case "dash": return "冲刺";
                case "jump": return "跳跃";
                case "pull": return "拉拽";
                case "play_presentation": return "播放表现";
                case "emit": return "发送表现事件";
                case "set_gameplay_var": return "设置玩法变量";
                case "add_gameplay_var": return "增加玩法变量";
                case "advance_gameplay_counter": return "推进玩法计数器";
                case "end_game": return "结束游戏";
                default: return string.IsNullOrWhiteSpace(fallback) ? type ?? "未选择类型" : fallback;
            }
        }

        public static string Parameter(string name)
        {
            switch (name)
            {
                case "left": return "左值";
                case "right": return "右值";
                case "value": return "值";
                case "variable": return "变量";
                case "target": return "目标";
                case "message": return "消息";
                case "threshold": return "阈值";
                case "amount": return "数量";
                case "delta": return "增量";
                case "rate": return "倍率";
                case "duration": return "持续时间";
                case "priority": return "优先级";
                case "reason": return "原因";
                case "mode": return "模式";
                case "options": return "选项";
                case "buff_id": return "增益效果 ID";
                case "buff_ids": return "增益效果 ID 列表";
                case "skill_id": return "技能 ID";
                case "skill_slot": return "技能槽位";
                case "projectile_id": return "投射物 ID";
                case "summon_id": return "召唤物 ID";
                case "area_id": return "区域 ID";
                case "source_id": return "来源 ID";
                case "trigger_id": return "触发器 ID";
                case "damage_type": return "伤害类型";
                case "heal_type": return "治疗类型";
                case "resource_type": return "资源类型";
                case "duration_ms": return "持续时间（毫秒）";
                case "duration_frames": return "持续帧数";
                case "interval_ms": return "间隔（毫秒）";
                case "cooldown_ms": return "冷却时间（毫秒）";
                case "count": return "重复次数";
                case "max_iterations": return "最大迭代次数";
                case "collection": return "集合";
                case "item": return "当前元素";
                case "weight": return "权重";
                case "schedule_mode": return "调度模式";
                case "max_executions": return "最大执行次数";
                case "can_be_interrupted": return "允许中断";
                case "remove_all": return "全部移除";
                case "check_stack": return "检查层数";
                case "target_mode": return "目标模式";
                case "direction_mode": return "方向模式";
                case "position_mode": return "位置模式";
                case "dump_args": return "输出全部参数";
                case "absorb_ratio": return "吸收比例";
                case "actor_id": return "实体 ID";
                case "apply_to_caster": return "应用于施法者";
                case "area_identity": return "区域标识";
                case "attr": return "属性";
                case "attribute_source": return "属性来源";
                case "collision_layer_mask": return "碰撞层遮罩";
                case "compare_type": return "比较方式";
                case "consume_policy": return "消耗策略";
                case "continuous_process_id": return "持续过程 ID";
                case "continuous_tag_template_id": return "持续标签模板 ID";
                case "damage_amount": return "伤害数值";
                case "damage_value": return "伤害值";
                case "damage_modifier": return "伤害修正";
                case "damage_type_mask": return "伤害类型遮罩";
                case "distance": return "距离";
                case "emitter_id": return "发送者 ID";
                case "filter": return "筛选规则";
                case "filter_param": return "筛选参数";
                case "half_angle_deg": return "半角（度）";
                case "heal_ratio": return "治疗比例";
                case "height": return "高度";
                case "hit_trigger_plan_id": return "命中触发计划 ID";
                case "interval_trigger_ids": return "间隔触发器 ID 列表";
                case "instance_id": return "实例 ID";
                case "key_id": return "变量键 ID";
                case "landing_trigger_ids": return "落地触发器 ID 列表";
                case "launcher_id": return "发射器 ID";
                case "max": return "最大值";
                case "max_count": return "最大数量";
                case "min": return "最小值";
                case "motion_group_id": return "位移分组 ID";
                case "move_to_aim_position": return "移动到瞄准位置";
                case "number_slot": return "数值槽位";
                case "offset_x": return "X 轴偏移";
                case "offset_y": return "Y 轴偏移";
                case "offset_z": return "Z 轴偏移";
                case "op": return "运算方式";
                case "order": return "排序方式";
                case "order_param": return "排序参数";
                case "owner_actor_id": return "所有者实体 ID";
                case "out_of_combat_seconds": return "脱战时间（秒）";
                case "pass_through_walls": return "允许穿墙";
                case "payload_field_id": return "事件参数字段 ID";
                case "query_template_id": return "查询模板 ID";
                case "radius": return "半径";
                case "reason_id": return "原因 ID";
                case "reason_param": return "原因参数";
                case "repeat_target_decay_factor": return "重复目标衰减系数";
                case "remove_slow": return "移除减速效果";
                case "require_skill_runtime": return "要求技能运行时";
                case "reset_value": return "重置值";
                case "root_owner_actor_id": return "根所有者实体 ID";
                case "rotation_mode": return "旋转模式";
                case "scale": return "缩放";
                case "scope_payload_field_id": return "作用域事件字段 ID";
                case "select": return "选择方式";
                case "self": return "包含自身";
                case "shield_id": return "护盾 ID";
                case "shield_identity": return "护盾标识";
                case "shield_value": return "护盾值";
                case "skill_identity": return "技能标识";
                case "skip_first_hit": return "跳过首次命中";
                case "source": return "来源";
                case "source_actor_id": return "来源实体 ID";
                case "source_attack_ratio": return "来源攻击力比例";
                case "source_param": return "来源参数";
                case "speed": return "速度";
                case "stacking_policy": return "叠加策略";
                case "stay_interval_frames": return "停留触发间隔帧数";
                case "stop": return "停止播放";
                case "summon_actor_id": return "召唤物实体 ID";
                case "target_actor_id": return "目标实体 ID";
                case "target_distance": return "目标距离";
                case "target_filter": return "目标筛选规则";
                case "target_filter_param": return "目标筛选参数";
                case "target_half_angle_deg": return "目标半角（度）";
                case "target_hit_count_key_base": return "目标命中计数键基值";
                case "target_max_count": return "目标最大数量";
                case "target_missing_hp_ratio_coefficient": return "目标已损生命比例系数";
                case "target_order": return "目标排序方式";
                case "target_order_param": return "目标排序参数";
                case "target_payload_field_id": return "目标事件字段 ID";
                case "target_radius": return "目标半径";
                case "target_select": return "目标选择方式";
                case "target_self": return "目标包含自身";
                case "target_source": return "目标来源";
                case "target_source_param": return "目标来源参数";
                case "template_id": return "模板 ID";
                case "total_count": return "总数量";
                case "track_target": return "追踪目标";
                case "trigger_ids": return "触发器 ID 列表";
                case "win_team_id": return "获胜队伍 ID";
                case "x": return "X 轴数值";
                case "y": return "Y 轴数值";
                case "z": return "Z 轴数值";
                default: return string.IsNullOrWhiteSpace(name) ? "未命名参数" : name;
            }
        }
    }

    internal sealed class TriggerAuthoringReferenceDropdown : AdvancedDropdown
    {
        private readonly IReadOnlyList<TriggerAuthoringReferenceOption> _options;
        private readonly System.Action<TriggerAuthoringReferenceOption> _selected;

        public TriggerAuthoringReferenceDropdown(
            AdvancedDropdownState state,
            IReadOnlyList<TriggerAuthoringReferenceOption> options,
            System.Action<TriggerAuthoringReferenceOption> selected)
            : base(state)
        {
            _options = options ?? System.Array.Empty<TriggerAuthoringReferenceOption>();
            _selected = selected;
            minimumSize = new Vector2(360f, 320f);
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            var root = new AdvancedDropdownItem("选择配置引用");
            var groups = new Dictionary<string, AdvancedDropdownItem>(System.StringComparer.Ordinal);
            var added = 0;
            for (var i = 0; i < _options.Count; i++)
            {
                var option = _options[i];
                if (option == null) continue;
                var parent = GetOrCreateGroup(root, groups, option.Group);
                parent.AddChild(new AdvancedDropdownItem(option.Label) { id = i + 1 });
                added++;
            }
            if (added == 0)
                root.AddChild(new AdvancedDropdownItem("没有可用配置") { enabled = false });
            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            var index = item.id - 1;
            if (index >= 0 && index < _options.Count)
                _selected?.Invoke(_options[index]);
        }

        private static AdvancedDropdownItem GetOrCreateGroup(
            AdvancedDropdownItem root,
            IDictionary<string, AdvancedDropdownItem> groups,
            string group)
        {
            if (string.IsNullOrWhiteSpace(group)) return root;
            var parts = group.Split('/');
            var parent = root;
            var path = string.Empty;
            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i]?.Trim();
                if (string.IsNullOrEmpty(part)) continue;
                path = path.Length == 0 ? part : path + "/" + part;
                if (!groups.TryGetValue(path, out var child))
                {
                    child = new AdvancedDropdownItem(part);
                    groups.Add(path, child);
                    parent.AddChild(child);
                }
                parent = child;
            }
            return parent;
        }
    }

    internal static class TriggerAuthoringValueRefEditor
    {
        public static void Draw(
            TriggerValueRefData value,
            TriggerParameterDescriptor parameter,
            TriggerAuthoringValueRefEditorContext context)
        {
            if (value == null) throw new System.ArgumentNullException(nameof(value));
            context = context ?? new TriggerAuthoringValueRefEditorContext();

            var allowed = parameter != null ? parameter.AllowedSources : TriggerValueSourceMask.All;
            DrawSource(value, allowed);
            var expectedType = parameter != null ? parameter.Type : TriggerValueType.None;
            DrawType(value, expectedType);
            var effectiveType = expectedType != TriggerValueType.None ? expectedType : value.Type;
            var access = parameter != null ? parameter.Access : TriggerParameterAccess.Read;

            switch (value.Source)
            {
                case TriggerValueSource.Constant:
                    DrawConstant(value, effectiveType, parameter, context);
                    break;
                case TriggerValueSource.Payload:
                    DrawPathPopup(value, CollectPathOptions(TriggerValueSource.Payload, effectiveType, access, context), "事件参数");
                    break;
                case TriggerValueSource.Context:
                    DrawPathPopup(value, CollectPathOptions(TriggerValueSource.Context, effectiveType, access, context), "运行上下文", true);
                    break;
                case TriggerValueSource.LocalBlackboard:
                    DrawPathPopup(value, CollectPathOptions(TriggerValueSource.LocalBlackboard, effectiveType, access, context), "局部黑板");
                    break;
                case TriggerValueSource.GlobalBlackboard:
                    DrawPathPopup(value, CollectPathOptions(TriggerValueSource.GlobalBlackboard, effectiveType, access, context), "全局黑板");
                    break;
                case TriggerValueSource.TemplateParameter:
                    DrawPathPopup(value, CollectPathOptions(TriggerValueSource.TemplateParameter, effectiveType, access, context), "模板参数");
                    break;
                case TriggerValueSource.Expression:
                    DrawExpression(value, context);
                    break;
                default:
                    value.Path = EditorGUILayout.TextField("路径", value.Path);
                    break;
            }
        }

        public static List<TriggerAuthoringValuePathOption> CollectPathOptions(
            TriggerValueSource source,
            TriggerValueType expectedType,
            TriggerParameterAccess access,
            TriggerAuthoringValueRefEditorContext context)
        {
            var result = new List<TriggerAuthoringValuePathOption>();
            context = context ?? new TriggerAuthoringValueRefEditorContext();
            var write = TriggerParameterAccessRules.IsWrite(access);

            switch (source)
            {
                case TriggerValueSource.Payload:
                    var eventDefinition = context.ResolveEventDefinition();
                    var fields = eventDefinition != null ? eventDefinition.PayloadFields : null;
                    if (fields != null)
                    {
                        for (var i = 0; i < fields.Count; i++)
                        {
                            var field = fields[i];
                            if (field == null || !TypeMatches(expectedType, field.Type)) continue;
                            result.Add(new TriggerAuthoringValuePathOption(
                                source,
                                field.Path,
                                field.Type,
                                "事件参数/" + (string.IsNullOrWhiteSpace(field.DisplayName) ? field.Path : field.DisplayName)));
                        }
                    }
                    break;
                case TriggerValueSource.Context:
                    AddRegisteredValueSourceOptions(result, context.ValueSources, expectedType, write);
                    AddFieldOptions(result, source, context.ContextFields, expectedType, "运行上下文");
                    break;
                case TriggerValueSource.LocalBlackboard:
                    AddLocalBlackboardOptions(
                        result,
                        context.Trigger != null ? context.Trigger.Blackboard : null,
                        expectedType,
                        write,
                        "触发器局部变量",
                        TriggerAuthoringLocalBlackboardScope.Trigger);
                    AddLocalBlackboardOptions(
                        result,
                        context.Module != null ? context.Module.Blackboard : null,
                        expectedType,
                        write,
                        "模块局部变量",
                        TriggerAuthoringLocalBlackboardScope.Module);
                    break;
                case TriggerValueSource.GlobalBlackboard:
                    var keys = context.GlobalBlackboard != null ? context.GlobalBlackboard.Definitions : null;
                    if (keys != null)
                    {
                        for (var i = 0; i < keys.Count; i++)
                        {
                            var key = keys[i];
                            if (key == null || !TypeMatches(expectedType, key.Type)) continue;
                            if (write && !key.CanWrite || !write && !key.CanRead) continue;
                            var domain = string.IsNullOrWhiteSpace(key.Domain) ? "global" : key.Domain;
                            result.Add(new TriggerAuthoringValuePathOption(
                                source,
                                key.Key,
                                key.Type,
                                "全局黑板/" + domain + "/" + (string.IsNullOrWhiteSpace(key.DisplayName) ? key.Key : key.DisplayName),
                                key.CanRead,
                                key.CanWrite));
                        }
                    }
                    break;
                case TriggerValueSource.TemplateParameter:
                    var parameters = context.TemplateParameters;
                    if (parameters != null)
                    {
                        for (var i = 0; i < parameters.Count; i++)
                        {
                            var parameter = parameters[i];
                            if (parameter == null || string.IsNullOrWhiteSpace(parameter.Name) ||
                                !TypeMatches(expectedType, parameter.Type))
                                continue;
                            result.Add(new TriggerAuthoringValuePathOption(
                                source,
                                parameter.Name,
                                parameter.Type,
                                "模板参数/" + parameter.Name));
                        }
                    }
                    break;
            }

            return result;
        }

        public static List<TriggerAuthoringValuePathOption> CollectReadableNumberPathOptions(
            TriggerAuthoringValueRefEditorContext context)
        {
            return CollectValueReferenceOptions(
                TriggerValueType.Number,
                TriggerParameterAccess.Read,
                TriggerValueSourceMask.Payload |
                TriggerValueSourceMask.Context |
                TriggerValueSourceMask.LocalBlackboard |
                TriggerValueSourceMask.GlobalBlackboard |
                TriggerValueSourceMask.TemplateParameter,
                context);
        }

        public static List<TriggerAuthoringValuePathOption> CollectValueReferenceOptions(
            TriggerValueType expectedType,
            TriggerParameterAccess access,
            TriggerValueSourceMask allowedSources,
            TriggerAuthoringValueRefEditorContext context)
        {
            var result = new List<TriggerAuthoringValuePathOption>();
            var sources = GetAllowedSources(allowedSources);
            for (var i = 0; i < sources.Count; i++)
            {
                var source = sources[i];
                if (source == TriggerValueSource.Constant ||
                    source == TriggerValueSource.Expression)
                {
                    continue;
                }

                result.AddRange(CollectPathOptions(source, expectedType, access, context));
            }

            result.Sort((left, right) =>
            {
                var sourceCompare = left.Source.CompareTo(right.Source);
                return sourceCompare != 0
                    ? sourceCompare
                    : string.Compare(left.Label, right.Label, System.StringComparison.Ordinal);
            });
            return result;
        }

        public static bool TypeMatches(TriggerValueType expected, TriggerValueType actual)
        {
            return expected == TriggerValueType.None || expected == actual ||
                   expected == TriggerValueType.Number && actual == TriggerValueType.Integer;
        }

        public static TriggerValueRefData CreateDefaultValue(TriggerParameterDescriptor parameter)
        {
            if (parameter == null) return CreateDefaultValue(TriggerValueType.Number);
            var value = CreateDefaultValue(parameter.Type == TriggerValueType.None
                ? TriggerValueType.Number
                : parameter.Type);
            if (parameter.Type == TriggerValueType.Object)
                AddDefaultObjectFields(value.Fields, parameter.Fields);
            return value;
        }

        public static TriggerValueRefData CreateDefaultValue(TriggerValueType type)
        {
            return new TriggerValueRefData
            {
                Source = TriggerValueSource.Constant,
                Type = type
            };
        }

        public static List<long> ParseIntegerList(string value)
        {
            var result = new List<long>();
            if (string.IsNullOrWhiteSpace(value)) return result;
            var parts = value.Split(',');
            for (var i = 0; i < parts.Length; i++)
            {
                if (long.TryParse(parts[i].Trim(), out var parsed)) result.Add(parsed);
            }
            return result;
        }

        private static void DrawSource(TriggerValueRefData value, TriggerValueSourceMask allowed)
        {
            var sources = GetAllowedSources(allowed);
            var sourceNames = new List<string>(sources.Count + 1);
            var selectedSource = -1;
            for (var i = 0; i < sources.Count; i++)
            {
                sourceNames.Add(GetSourceName(sources[i]));
                if (sources[i] == value.Source) selectedSource = i;
            }
            if (selectedSource < 0)
            {
                sourceNames.Add(GetSourceName(value.Source) + "  [当前不可用]");
                selectedSource = sourceNames.Count - 1;
            }

            var nextSource = EditorGUILayout.Popup("数据来源", selectedSource, sourceNames.ToArray());
            if (nextSource != selectedSource && nextSource < sources.Count)
                value.Source = sources[nextSource];
        }

        private static void DrawType(TriggerValueRefData value, TriggerValueType expectedType)
        {
            if (expectedType == TriggerValueType.None)
            {
                var values = (TriggerValueType[])System.Enum.GetValues(typeof(TriggerValueType));
                var names = new string[values.Length];
                var selected = 0;
                for (var i = 0; i < values.Length; i++)
                {
                    names[i] = TriggerAuthoringEditorLabels.ValueType(values[i]);
                    if (values[i] == value.Type) selected = i;
                }
                value.Type = values[EditorGUILayout.Popup("数据类型", selected, names)];
            }
            else
            {
                value.Type = expectedType;
                EditorGUILayout.LabelField("数据类型", TriggerAuthoringEditorLabels.ValueType(expectedType));
            }
        }

        private static void DrawConstant(
            TriggerValueRefData value,
            TriggerValueType type,
            TriggerParameterDescriptor parameter,
            TriggerAuthoringValueRefEditorContext context)
        {
            switch (type)
            {
                case TriggerValueType.Integer:
                    if (parameter != null && parameter.Options.Count > 0)
                    {
                        DrawIntegerChoice(value, parameter.Options);
                        break;
                    }
                    if (parameter != null &&
                        context?.References != null &&
                        context.References.TryGetProvider(parameter.SemanticId, type, out _))
                    {
                        DrawIntegerReference(value, parameter, context);
                        break;
                    }
                    value.IntegerValue = EditorGUILayout.LongField("值", value.IntegerValue);
                    break;
                case TriggerValueType.Entity:
                case TriggerValueType.ObjectId:
                    value.IntegerValue = EditorGUILayout.LongField("值", value.IntegerValue);
                    break;
                case TriggerValueType.Number:
                    value.NumberValue = EditorGUILayout.DoubleField("值", value.NumberValue);
                    break;
                case TriggerValueType.Boolean:
                    value.BooleanValue = EditorGUILayout.Toggle("值", value.BooleanValue);
                    break;
                case TriggerValueType.String:
                    value.StringValue = EditorGUILayout.TextField("值", value.StringValue);
                    break;
                case TriggerValueType.IntegerList:
                    if (parameter != null &&
                        context?.References != null &&
                        context.References.TryGetProvider(parameter.SemanticId, type, out _))
                    {
                        DrawIntegerReferenceList(value, parameter, context);
                        break;
                    }
                    var current = value.IntegerListValue != null ? string.Join(",", value.IntegerListValue) : string.Empty;
                    var next = EditorGUILayout.TextField("值列表", current);
                    if (!string.Equals(current, next, System.StringComparison.Ordinal))
                        value.IntegerListValue = ParseIntegerList(next);
                    break;
                case TriggerValueType.Vector3:
                    value.Vector3Value = value.Vector3Value ?? new TriggerVector3Data();
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("值", GUILayout.Width(EditorGUIUtility.labelWidth - 4f));
                    value.Vector3Value.X = EditorGUILayout.DoubleField(value.Vector3Value.X);
                    value.Vector3Value.Y = EditorGUILayout.DoubleField(value.Vector3Value.Y);
                    value.Vector3Value.Z = EditorGUILayout.DoubleField(value.Vector3Value.Z);
                    EditorGUILayout.EndHorizontal();
                    break;
                case TriggerValueType.Object:
                    DrawObjectFields(value, parameter, context);
                    break;
                default:
                    EditorGUILayout.HelpBox("请选择数据类型。", MessageType.Info);
                    break;
            }
        }

        private static void DrawObjectFields(
            TriggerValueRefData value,
            TriggerParameterDescriptor parameter,
            TriggerAuthoringValueRefEditorContext context)
        {
            value.Fields = value.Fields ?? new List<TriggerArgumentData>();
            if (parameter != null && parameter.Fields.Count > 0)
            {
                DrawObjectSchemaFields(value, parameter, context);
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("对象字段", EditorStyles.miniBoldLabel);
            for (var i = 0; i < value.Fields.Count; i++)
            {
                var index = i;
                var field = value.Fields[i] ?? (value.Fields[i] = new TriggerArgumentData());
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                field.Name = EditorGUILayout.TextField(field.Name);
                var remove = GUILayout.Button("x", EditorStyles.miniButton, GUILayout.Width(22f));
                EditorGUILayout.EndHorizontal();
                field.Value = field.Value ?? new TriggerValueRefData();
                Draw(field.Value, null, context);
                EditorGUILayout.EndVertical();
                if (remove)
                {
                    value.Fields.RemoveAt(index);
                    i--;
                }
            }

            if (GUILayout.Button("添加字段", EditorStyles.miniButton))
                value.Fields.Add(new TriggerArgumentData
                {
                    Name = CreateUniqueFieldName(value.Fields),
                    Value = new TriggerValueRefData
                    {
                        Source = TriggerValueSource.Constant,
                        Type = TriggerValueType.Number
                    }
                });
            EditorGUILayout.EndVertical();
        }

        private static void DrawObjectSchemaFields(
            TriggerValueRefData value,
            TriggerParameterDescriptor parameter,
            TriggerAuthoringValueRefEditorContext context)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("对象字段", EditorStyles.miniBoldLabel);

            for (var i = 0; i < parameter.Fields.Count; i++)
            {
                var fieldParameter = parameter.Fields[i];
                if (fieldParameter == null || string.IsNullOrWhiteSpace(fieldParameter.Name)) continue;
                var field = TriggerAuthoringArgumentPathResolver.FindField(value.Fields, fieldParameter.Name);
                if (field == null)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label(fieldParameter.Name, fieldParameter.Required ? EditorStyles.miniBoldLabel : EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("添加", EditorStyles.miniButton, GUILayout.Width(42f)))
                    {
                        value.Fields.Add(new TriggerArgumentData
                        {
                            Name = fieldParameter.Name,
                            Value = CreateDefaultValue(fieldParameter)
                        });
                    }
                    EditorGUILayout.EndHorizontal();
                    continue;
                }

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(fieldParameter.Name, EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                if (!fieldParameter.Required && GUILayout.Button("x", EditorStyles.miniButton, GUILayout.Width(22f)))
                {
                    value.Fields.Remove(field);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    continue;
                }
                EditorGUILayout.EndHorizontal();
                field.Value = field.Value ?? CreateDefaultValue(fieldParameter);
                Draw(field.Value, fieldParameter, context);
                EditorGUILayout.EndVertical();
            }

            for (var i = 0; i < value.Fields.Count; i++)
            {
                var field = value.Fields[i];
                if (field == null || HasFieldParameter(parameter.Fields, field.Name)) continue;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                field.Name = EditorGUILayout.TextField(field.Name);
                var remove = GUILayout.Button("x", EditorStyles.miniButton, GUILayout.Width(22f));
                EditorGUILayout.EndHorizontal();
                field.Value = field.Value ?? CreateDefaultValue(TriggerValueType.Number);
                Draw(field.Value, null, context);
                EditorGUILayout.EndVertical();
                if (remove)
                {
                    value.Fields.RemoveAt(i);
                    i--;
                }
            }

            if (GUILayout.Button("添加扩展字段", EditorStyles.miniButton))
                value.Fields.Add(new TriggerArgumentData
                {
                    Name = CreateUniqueFieldName(value.Fields),
                    Value = CreateDefaultValue(TriggerValueType.Number)
                });
            EditorGUILayout.EndVertical();
        }

        private static void AddDefaultObjectFields(
            ICollection<TriggerArgumentData> output,
            IReadOnlyList<TriggerParameterDescriptor> fields)
        {
            if (output == null || fields == null) return;
            var createdGroups = new HashSet<string>(System.StringComparer.Ordinal);
            for (var i = 0; i < fields.Count; i++)
            {
                var field = fields[i];
                if (field == null) continue;
                if (field.Required ||
                    !string.IsNullOrEmpty(field.RequiredGroup) && createdGroups.Add(field.RequiredGroup))
                {
                    output.Add(new TriggerArgumentData
                    {
                        Name = field.Name,
                        Value = CreateDefaultValue(field)
                    });
                }
            }
        }

        private static bool HasFieldParameter(IReadOnlyList<TriggerParameterDescriptor> fields, string name)
        {
            if (fields == null) return false;
            for (var i = 0; i < fields.Count; i++)
            {
                var field = fields[i];
                if (field != null && string.Equals(field.Name, name, System.StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static string CreateUniqueFieldName(IReadOnlyList<TriggerArgumentData> fields)
        {
            var suffix = 1;
            var name = "field";
            while (ContainsFieldName(fields, name))
            {
                suffix++;
                name = "field" + suffix;
            }
            return name;
        }

        private static bool ContainsFieldName(IReadOnlyList<TriggerArgumentData> fields, string name)
        {
            if (fields == null) return false;
            for (var i = 0; i < fields.Count; i++)
            {
                var field = fields[i];
                if (field != null && string.Equals(field.Name, name, System.StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static void DrawIntegerChoice(
            TriggerValueRefData value,
            IReadOnlyList<TriggerParameterOption> options)
        {
            var names = new List<string>(options.Count + 1);
            var selected = -1;
            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                names.Add(option.DisplayName + "  [" + option.Value + "]");
                if (option.Value == value.IntegerValue) selected = i;
            }
            if (selected < 0)
            {
                names.Add(value.IntegerValue + "  [当前不可用]");
                selected = names.Count - 1;
            }

            var next = EditorGUILayout.Popup("值", selected, names.ToArray());
            if (next != selected && next < options.Count)
                value.IntegerValue = options[next].Value;
        }

        private static void DrawIntegerReference(
            TriggerValueRefData value,
            TriggerParameterDescriptor parameter,
            TriggerAuthoringValueRefEditorContext context)
        {
            EditorGUILayout.BeginHorizontal();
            value.IntegerValue = EditorGUILayout.LongField("值", value.IntegerValue);
            if (GUILayout.Button("选择", EditorStyles.miniButton, GUILayout.Width(52f)))
            {
                var activator = GUILayoutUtility.GetLastRect();
                var options = context.References.GetOptions(parameter.SemanticId, parameter.Type);
                new TriggerAuthoringReferenceDropdown(
                    new AdvancedDropdownState(),
                    options,
                    option => ApplyReferenceSelection(value, parameter, context, option))
                    .Show(activator);
            }
            EditorGUILayout.EndHorizontal();

            if (context.References.TryResolve(
                    parameter.SemanticId,
                    parameter.Type,
                    value.IntegerValue,
                    out var resolved))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("已解析", resolved.Label, EditorStyles.miniLabel);
                DrawLocateReferenceButton(context.References, parameter, value.IntegerValue);
                EditorGUILayout.EndHorizontal();
                if (!string.IsNullOrWhiteSpace(resolved.Description))
                    EditorGUILayout.LabelField("说明", resolved.Description, EditorStyles.wordWrappedMiniLabel);
            }
            else if (context.References.IsOperational(parameter.SemanticId))
            {
                EditorGUILayout.HelpBox(
                    "未找到 " + parameter.SemanticId + " 引用 [" + value.IntegerValue + "]，原始 ID 将继续保留。",
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "引用目录暂不可用，当前按普通整数 ID 编辑并保留原始值。",
                    MessageType.Info);
            }
        }

        private static void ApplyReferenceSelection(
            TriggerValueRefData value,
            TriggerParameterDescriptor parameter,
            TriggerAuthoringValueRefEditorContext context,
            TriggerAuthoringReferenceOption option)
        {
            if (option == null || value.IntegerValue == option.Value) return;
            System.Action apply = () => value.IntegerValue = option.Value;
            if (context.ApplyChange != null)
                context.ApplyChange("选择" + parameter.SemanticId + "引用", apply);
            else
            {
                apply();
                GUI.changed = true;
            }
        }

        private static void DrawIntegerReferenceList(
            TriggerValueRefData value,
            TriggerParameterDescriptor parameter,
            TriggerAuthoringValueRefEditorContext context)
        {
            value.IntegerListValue = value.IntegerListValue ?? new List<long>();
            var current = string.Join(",", value.IntegerListValue);
            var next = EditorGUILayout.TextField("值列表", current);
            if (!string.Equals(current, next, System.StringComparison.Ordinal))
                value.IntegerListValue = ParseIntegerList(next);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(EditorGUIUtility.labelWidth);
            if (GUILayout.Button("添加配置", EditorStyles.miniButton))
            {
                var activator = GUILayoutUtility.GetLastRect();
                var options = context.References.GetOptions(parameter.SemanticId, parameter.Type);
                new TriggerAuthoringReferenceDropdown(
                    new AdvancedDropdownState(),
                    options,
                    option => ApplyReferenceListSelection(value, parameter, context, option))
                    .Show(activator);
            }
            using (new EditorGUI.DisabledScope(value.IntegerListValue.Count == 0))
            {
                if (GUILayout.Button("清空", EditorStyles.miniButton, GUILayout.Width(44f)))
                    value.IntegerListValue.Clear();
            }
            EditorGUILayout.EndHorizontal();

            for (var i = 0; i < value.IntegerListValue.Count; i++)
            {
                var index = i;
                var id = value.IntegerListValue[i];
                var resolved = context.References.TryResolve(
                    parameter.SemanticId,
                    parameter.Type,
                    id,
                    out var option);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(EditorGUIUtility.labelWidth);
                GUILayout.Label(
                    resolved ? option.Label : "缺失引用  [" + id + "]",
                    resolved ? EditorStyles.miniLabel : EditorStyles.miniBoldLabel);
                if (resolved)
                    DrawLocateReferenceButton(context.References, parameter, id);
                if (GUILayout.Button("x", EditorStyles.miniButton, GUILayout.Width(22f)))
                {
                    value.IntegerListValue.RemoveAt(index);
                    i--;
                }
                EditorGUILayout.EndHorizontal();
            }
            if (!context.References.IsOperational(parameter.SemanticId))
                EditorGUILayout.HelpBox(
                    "引用目录暂不可用，当前按普通整数列表编辑并保留原始值。",
                    MessageType.Info);
        }

        private static void ApplyReferenceListSelection(
            TriggerValueRefData value,
            TriggerParameterDescriptor parameter,
            TriggerAuthoringValueRefEditorContext context,
            TriggerAuthoringReferenceOption option)
        {
            if (option == null) return;
            value.IntegerListValue = value.IntegerListValue ?? new List<long>();
            if (value.IntegerListValue.Contains(option.Value)) return;
            System.Action apply = () => value.IntegerListValue.Add(option.Value);
            if (context.ApplyChange != null)
                context.ApplyChange("添加" + parameter.SemanticId + "引用", apply);
            else
            {
                apply();
                GUI.changed = true;
            }
        }

        private static void DrawLocateReferenceButton(
            TriggerAuthoringReferenceCatalog references,
            TriggerParameterDescriptor parameter,
            long value)
        {
            if (references == null || parameter == null ||
                !references.CanLocate(parameter.SemanticId, parameter.Type))
                return;
            var content = EditorGUIUtility.IconContent("d_ViewToolZoom");
            content.tooltip = "定位配置资源";
            if (!GUILayout.Button(content, EditorStyles.miniButton, GUILayout.Width(24f))) return;
            if (!references.TryGetTarget(parameter.SemanticId, parameter.Type, value, out var target)) return;
            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
        }

        private static void DrawPathPopup(
            TriggerValueRefData value,
            List<TriggerAuthoringValuePathOption> options,
            string label,
            bool allowManualPath = false)
        {
            options = options ?? new List<TriggerAuthoringValuePathOption>();
            var names = new List<string> { "<未选择>" };
            var selected = 0;
            for (var i = 0; i < options.Count; i++)
            {
                names.Add(options[i].Label + "  [" + options[i].Path + ", " + TriggerAuthoringEditorLabels.ValueType(options[i].Type) + "]");
                if (string.Equals(options[i].Path, value.Path, System.StringComparison.Ordinal)) selected = i + 1;
                else if (selected == 0 && options[i].MatchesPath(value.Path)) selected = i + 1;
            }
            if (selected == 0 && !string.IsNullOrWhiteSpace(value.Path))
            {
                names.Add(value.Path + "  [当前不可用]");
                selected = names.Count - 1;
            }

            var next = EditorGUILayout.Popup(label, selected, names.ToArray());
            if (next == 0)
            {
                value.Path = string.Empty;
            }
            else if (next <= options.Count)
            {
                var option = options[next - 1];
                value.Path = option.Path;
                value.Type = option.Type;
            }

            if (allowManualPath)
                value.Path = EditorGUILayout.TextField(label + "路径", value.Path);
        }

        private static void AddFieldOptions(
            ICollection<TriggerAuthoringValuePathOption> output,
            TriggerValueSource source,
            IReadOnlyList<TriggerPayloadFieldData> fields,
            TriggerValueType expectedType,
            string prefix)
        {
            if (fields == null) return;
            for (var i = 0; i < fields.Count; i++)
            {
                var field = fields[i];
                if (field == null || string.IsNullOrWhiteSpace(field.Path) || !TypeMatches(expectedType, field.Type)) continue;
                output.Add(new TriggerAuthoringValuePathOption(
                    source,
                    field.Path,
                    field.Type,
                    prefix + "/" + (string.IsNullOrWhiteSpace(field.DisplayName) ? field.Path : field.DisplayName)));
            }
        }

        private static void AddLocalBlackboardOptions(
            ICollection<TriggerAuthoringValuePathOption> output,
            IReadOnlyList<TriggerBlackboardVariableData> variables,
            TriggerValueType expectedType,
            bool write,
            string scope,
            TriggerAuthoringLocalBlackboardScope localScope)
        {
            if (variables == null) return;
            for (var i = 0; i < variables.Count; i++)
            {
                var variable = variables[i];
                if (variable == null || string.IsNullOrWhiteSpace(variable.Key)) continue;
                if (write && variable.ReadOnly || !TypeMatches(expectedType, variable.Type)) continue;
                output.Add(new TriggerAuthoringValuePathOption(
                    TriggerValueSource.LocalBlackboard,
                    TriggerAuthoringLocalBlackboardPath.Format(localScope, variable.Key),
                    variable.Type,
                    scope + "/" + variable.Key,
                    true,
                    !variable.ReadOnly,
                    variable.Key));
            }
        }

        private static List<TriggerValueSource> GetAllowedSources(TriggerValueSourceMask mask)
        {
            var result = new List<TriggerValueSource>();
            foreach (TriggerValueSource source in System.Enum.GetValues(typeof(TriggerValueSource)))
            {
                var sourceMask = (TriggerValueSourceMask)(1 << (int)source);
                if ((mask & sourceMask) != 0) result.Add(source);
            }
            if (result.Count == 0) result.Add(TriggerValueSource.Constant);
            return result;
        }

        private static string GetSourceName(TriggerValueSource source)
        {
            return TriggerAuthoringEditorLabels.Source(source);
        }

        public static List<TriggerAuthoringExpressionReference> CollectExpressionReferences(
            TriggerAuthoringValueRefEditorContext context)
        {
            context = context ?? new TriggerAuthoringValueRefEditorContext();
            var result = new List<TriggerAuthoringExpressionReference>();

            var eventDefinition = context.ResolveEventDefinition();
            AddExpressionFields(result, eventDefinition != null ? eventDefinition.PayloadFields : null, "payload.", "事件参数/");

            var valueSources = context.ValueSources != null ? context.ValueSources.Definitions : null;
            if (valueSources != null)
            {
                for (var i = 0; i < valueSources.Count; i++)
                {
                    var source = valueSources[i];
                    if (source == null || !IsNumericType(source.Type)) continue;
                    var expressionName = GetContextExpressionName(source);
                    if (string.IsNullOrWhiteSpace(expressionName)) continue;
                    AddExpressionReference(result, expressionName, "运行上下文/" + source.DisplayName, source.Type);
                }
            }

            AddExpressionBlackboard(result, context.Trigger != null ? context.Trigger.Blackboard : null, "trigger.", "触发器变量/");
            AddExpressionBlackboard(result, context.Module != null ? context.Module.Blackboard : null, "module.", "模块变量/");

            var globals = context.GlobalBlackboard != null ? context.GlobalBlackboard.Definitions : null;
            if (globals != null)
            {
                for (var i = 0; i < globals.Count; i++)
                {
                    var variable = globals[i];
                    if (variable == null || !variable.CanRead || !IsNumericType(variable.Type) || string.IsNullOrWhiteSpace(variable.Key)) continue;
                    AddExpressionReference(result, "global." + variable.Key, "全局黑板/" + variable.DisplayName, variable.Type);
                }
            }

            result.Sort((left, right) => string.Compare(left.Label, right.Label, System.StringComparison.Ordinal));
            return result;
        }

        public static bool TryValidateExpression(string expression, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(expression))
            {
                error = "必须填写表达式。";
                return false;
            }
            if (!NumericExpressionCompiler.TryCompile(expression, out var program) || program == null)
            {
                error = "表达式语法无效。请检查括号、运算符和变量名。";
                return false;
            }

            var stack = 0;
            var tokens = program.Tokens;
            for (var i = 0; i < tokens.Length; i++)
            {
                var token = tokens[i];
                switch (token.Kind)
                {
                    case NumericRpnTokenKind.Number:
                    case NumericRpnTokenKind.Var:
                        stack++;
                        break;
                    case NumericRpnTokenKind.Add:
                    case NumericRpnTokenKind.Sub:
                    case NumericRpnTokenKind.Mul:
                    case NumericRpnTokenKind.Div:
                        if (stack < 2)
                        {
                            error = "表达式中的二元运算符缺少操作数。";
                            return false;
                        }
                        stack--;
                        break;
                    case NumericRpnTokenKind.Func:
                        if (!DefaultNumericRpnFunctionRegistry.Instance.TryGet(token.FuncName, out var function) || function == null)
                        {
                            error = "未知公式函数：" + token.FuncName;
                            return false;
                        }
                        if (function.ArgCount != token.FuncArgCount)
                        {
                            error = $"函数 {token.FuncName} 需要 {function.ArgCount} 个参数，当前为 {token.FuncArgCount} 个。";
                            return false;
                        }
                        if (stack < token.FuncArgCount)
                        {
                            error = "公式函数缺少参数：" + token.FuncName;
                            return false;
                        }
                        stack = stack - token.FuncArgCount + 1;
                        break;
                }
            }
            if (stack != 1)
            {
                error = "表达式必须最终计算出一个数值。";
                return false;
            }
            return true;
        }

        private static void AddRegisteredValueSourceOptions(
            ICollection<TriggerAuthoringValuePathOption> output,
            TriggerAuthoringValueSourceCatalog catalog,
            TriggerValueType expectedType,
            bool write)
        {
            var definitions = catalog != null ? catalog.Definitions : null;
            if (definitions == null) return;
            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition == null || !TypeMatches(expectedType, definition.Type)) continue;
                if (write && !definition.CanWrite) continue;
                output.Add(new TriggerAuthoringValuePathOption(
                    TriggerValueSource.Context,
                    definition.Path,
                    definition.Type,
                    "运行上下文/" + definition.DisplayName,
                    true,
                    definition.CanWrite));
            }
        }

        private static void DrawExpression(
            TriggerValueRefData value,
            TriggerAuthoringValueRefEditorContext context)
        {
            EditorGUILayout.LabelField("数值公式", EditorStyles.miniBoldLabel);
            value.Expression = EditorGUILayout.TextArea(
                value.Expression ?? string.Empty,
                GUILayout.MinHeight(EditorGUIUtility.singleLineHeight * 2f));

            var references = CollectExpressionReferences(context);
            var referenceNames = new string[references.Count + 1];
            referenceNames[0] = "插入变量...";
            for (var i = 0; i < references.Count; i++)
                referenceNames[i + 1] = references[i].Label + "  [" + references[i].Expression + "]";
            var selectedReference = EditorGUILayout.Popup("引用", 0, referenceNames);
            if (selectedReference > 0)
                value.Expression = AppendExpressionToken(value.Expression, references[selectedReference - 1].Expression);

            var functionNames = TriggerAuthoringExpressionFunctions.Names;
            var selectedFunction = EditorGUILayout.Popup("函数", 0, functionNames);
            if (selectedFunction > 0)
                value.Expression = AppendExpressionToken(value.Expression, TriggerAuthoringExpressionFunctions.Snippets[selectedFunction]);

            if (TryValidateExpression(value.Expression, out var error))
                EditorGUILayout.HelpBox("公式语法有效。运行时将编译为 RPN。", MessageType.Info);
            else if (!string.IsNullOrWhiteSpace(value.Expression))
                EditorGUILayout.HelpBox(error, MessageType.Error);
        }

        private static string AppendExpressionToken(string expression, string token)
        {
            expression = expression ?? string.Empty;
            if (string.IsNullOrWhiteSpace(expression)) return token ?? string.Empty;
            return expression.TrimEnd() + " " + (token ?? string.Empty);
        }

        private static void AddExpressionFields(
            ICollection<TriggerAuthoringExpressionReference> output,
            IReadOnlyList<TriggerPayloadFieldData> fields,
            string prefix,
            string labelPrefix)
        {
            if (fields == null) return;
            for (var i = 0; i < fields.Count; i++)
            {
                var field = fields[i];
                if (field == null || !IsNumericType(field.Type) || string.IsNullOrWhiteSpace(field.Path)) continue;
                AddExpressionReference(
                    output,
                    prefix + field.Path,
                    labelPrefix + (string.IsNullOrWhiteSpace(field.DisplayName) ? field.Path : field.DisplayName),
                    field.Type);
            }
        }

        private static void AddExpressionBlackboard(
            ICollection<TriggerAuthoringExpressionReference> output,
            IReadOnlyList<TriggerBlackboardVariableData> variables,
            string prefix,
            string labelPrefix)
        {
            if (variables == null) return;
            for (var i = 0; i < variables.Count; i++)
            {
                var variable = variables[i];
                if (variable == null || !IsNumericType(variable.Type) || string.IsNullOrWhiteSpace(variable.Key)) continue;
                AddExpressionReference(output, prefix + variable.Key, labelPrefix + variable.Key, variable.Type);
            }
        }

        private static void AddExpressionReference(
            ICollection<TriggerAuthoringExpressionReference> output,
            string expression,
            string label,
            TriggerValueType type)
        {
            if (output == null || string.IsNullOrWhiteSpace(expression)) return;
            output.Add(new TriggerAuthoringExpressionReference(expression, label, type));
        }

        private static string GetContextExpressionName(TriggerAuthoringValueSourceDescriptor descriptor)
        {
            if (descriptor == null) return string.Empty;
            if (!string.IsNullOrWhiteSpace(descriptor.ExpressionName)) return descriptor.ExpressionName;
            var path = descriptor.Path ?? string.Empty;
            var separator = path.IndexOf(':');
            return separator > 0 && separator < path.Length - 1
                ? path.Substring(0, separator) + "." + path.Substring(separator + 1)
                : "context." + path;
        }

        private static bool IsNumericType(TriggerValueType type)
        {
            return type == TriggerValueType.Integer ||
                   type == TriggerValueType.Number ||
                   type == TriggerValueType.Entity ||
                   type == TriggerValueType.ObjectId;
        }
    }
}
