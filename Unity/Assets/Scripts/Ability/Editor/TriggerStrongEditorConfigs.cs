using System;
using System.Collections.Generic;
using AbilityKit.Ability.Configs;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Runtime;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace AbilityKit.Ability.Editor
{
    [Serializable]
    public abstract class ConditionEditorConfigBase
    {
        [ShowInInspector, ReadOnly, LabelText("类型")]
        public abstract string Type { get; }

        public string DisplayTitle => ToString();

        public abstract ConditionRuntimeConfigBase ToRuntimeStrong();

        protected virtual string GetTitleSuffix()
        {
            return null;
        }

        public override string ToString()
        {
            var name = StrongEditorConfigNameCache.GetDisplayName(GetType());
            var title = string.IsNullOrEmpty(name) ? Type : name;
            var suffix = GetTitleSuffix();
            if (!string.IsNullOrEmpty(suffix))
            {
                title += ": " + suffix;
            }
            title += "  [" + Type + "]";
            return title;
        }
    }

    [Serializable]
    [TriggerConditionType("all", "全部满足(AND)", "条件/复合", 0)]
    public sealed class AllConditionEditorConfig : ConditionEditorConfigBase
    {
        public override string Type => "all";

        [SerializeReference]
        [HideReferenceObjectPicker]
        [LabelText("子条件")]
        [ListDrawerSettings(Expanded = true, ListElementLabelName = "DisplayTitle", CustomAddFunction = nameof(AddChild))]
        public List<ConditionEditorConfigBase> Items = new List<ConditionEditorConfigBase>();

        private void AddChild()
        {
            StrongConfigTypeSelector.ShowAddConditionSelector(Items, null);
        }

        public override ConditionRuntimeConfigBase ToRuntimeStrong()
        {
            var list = new List<ConditionRuntimeConfigBase>(Items != null ? Items.Count : 0);
            if (Items != null)
            {
                for (int i = 0; i < Items.Count; i++)
                {
                    var c = Items[i];
                    if (c == null) continue;
                    var rt = c.ToRuntimeStrong();
                    if (rt != null) list.Add(rt);
                }
            }
            return new AllConditionConfig
            {
                Items = list
            };
        }
    }

    [Serializable]
    [TriggerConditionType("any", "任意满足(OR)", "条件/复合", 10)]
    public sealed class AnyConditionEditorConfig : ConditionEditorConfigBase
    {
        public override string Type => "any";

        [SerializeReference]
        [HideReferenceObjectPicker]
        [LabelText("子条件")]
        [ListDrawerSettings(Expanded = true, ListElementLabelName = "DisplayTitle", CustomAddFunction = nameof(AddChild))]
        public List<ConditionEditorConfigBase> Items = new List<ConditionEditorConfigBase>();

        private void AddChild()
        {
            StrongConfigTypeSelector.ShowAddConditionSelector(Items, null);
        }

        public override ConditionRuntimeConfigBase ToRuntimeStrong()
        {
            var list = new List<ConditionRuntimeConfigBase>(Items != null ? Items.Count : 0);
            if (Items != null)
            {
                for (int i = 0; i < Items.Count; i++)
                {
                    var c = Items[i];
                    if (c == null) continue;
                    var rt = c.ToRuntimeStrong();
                    if (rt != null) list.Add(rt);
                }
            }
            return new AnyConditionConfig
            {
                Items = list
            };
        }
    }

    [Serializable]
    [TriggerConditionType("not", "取反(NOT)", "条件/复合", 20)]
    public sealed class NotConditionEditorConfig : ConditionEditorConfigBase
    {
        public override string Type => "not";

        [SerializeReference]
        [HideReferenceObjectPicker]
        [LabelText("子条件")]
        [ListDrawerSettings(Expanded = true, ListElementLabelName = "DisplayTitle", CustomAddFunction = nameof(AddChild), DraggableItems = false)]
        public List<ConditionEditorConfigBase> Items = new List<ConditionEditorConfigBase>();

        private void AddChild()
        {
            if (Items == null) Items = new List<ConditionEditorConfigBase>();
            if (Items.Count > 0) return;
            StrongConfigTypeSelector.ShowAddConditionSelector(Items, null);
        }

        public override ConditionRuntimeConfigBase ToRuntimeStrong()
        {
            return new NotConditionConfig
            {
                Item = Items != null && Items.Count > 0 && Items[0] != null ? Items[0].ToRuntimeStrong() : null
            };
        }
    }

    internal static class StrongEditorTitleUtil
    {
        public static string FormatArg(ArgRuntimeEntry e)
        {
            if (e == null) return "null";

            switch (e.Kind)
            {
                case ArgValueKind.Int:
                    return e.IntValue.ToString();
                case ArgValueKind.Float:
                    return e.FloatValue.ToString("0.###");
                case ArgValueKind.Bool:
                    return e.BoolValue ? "true" : "false";
                case ArgValueKind.String:
                    return QuoteAndTruncate(e.StringValue, 32);
                case ArgValueKind.Object:
                    if (e.ObjectValue == null) return "null";
                    if (e.ObjectValue is UnityEngine.Object uo) return uo != null ? uo.name : "null";
                    return e.ObjectValue.ToString();
                default:
                    return "null";
            }
        }

        public static string QuoteAndTruncate(string s, int maxLen)
        {
            if (string.IsNullOrEmpty(s)) return "\"\"";
            if (maxLen > 3 && s.Length > maxLen)
            {
                s = s.Substring(0, maxLen - 3) + "...";
            }
            return "\"" + s + "\"";
        }
    }

    [Serializable]
    public abstract class ActionEditorConfigBase
    {
        [ShowInInspector, ReadOnly, LabelText("类型")]
        public abstract string Type { get; }

        public string DisplayTitle => ToString();

        public abstract ActionRuntimeConfigBase ToRuntimeStrong();

        protected virtual string GetTitleSuffix()
        {
            return null;
        }

        public override string ToString()
        {
            var name = StrongEditorConfigNameCache.GetDisplayName(GetType());
            var title = string.IsNullOrEmpty(name) ? Type : name;
            var suffix = GetTitleSuffix();
            if (!string.IsNullOrEmpty(suffix))
            {
                title += ": " + suffix;
            }
            title += "  [" + Type + "]";
            return title;
        }
    }

    [Serializable]
    [TriggerActionType("seq", "顺序组", "行为/流程", 0)]
    public sealed class SequenceActionEditorConfig : ActionEditorConfigBase
    {
        public override string Type => "seq";

        [SerializeReference]
        [HideReferenceObjectPicker]
        [LabelText("子行为")]
        [ListDrawerSettings(Expanded = true, ListElementLabelName = "DisplayTitle", CustomAddFunction = nameof(AddChild))]
        public List<ActionEditorConfigBase> Items = new List<ActionEditorConfigBase>();

        private void AddChild()
        {
            StrongConfigTypeSelector.ShowAddActionSelector(Items, null);
        }

        public override ActionRuntimeConfigBase ToRuntimeStrong()
        {
            var list = new List<ActionRuntimeConfigBase>(Items != null ? Items.Count : 0);
            if (Items != null)
            {
                for (int i = 0; i < Items.Count; i++)
                {
                    var a = Items[i];
                    if (a == null) continue;
                    var rt = a.ToRuntimeStrong();
                    if (rt != null) list.Add(rt);
                }
            }
            return new SequenceActionConfig
            {
                Items = list
            };
        }
    }

    internal static class StrongEditorConfigNameCache
    {
        private static readonly Dictionary<Type, string> Cache = new Dictionary<Type, string>();

        public static string GetDisplayName(Type t)
        {
            if (t == null) return null;

            if (Cache.TryGetValue(t, out var v)) return v;

            var c = (TriggerConditionTypeAttribute)Attribute.GetCustomAttribute(t, typeof(TriggerConditionTypeAttribute));
            if (c != null)
            {
                v = c.DisplayName;
                Cache[t] = v;
                return v;
            }

            var a = (TriggerActionTypeAttribute)Attribute.GetCustomAttribute(t, typeof(TriggerActionTypeAttribute));
            if (a != null)
            {
                v = a.DisplayName;
                Cache[t] = v;
                return v;
            }

            Cache[t] = null;
            return null;
        }
    }

    [Serializable]
    [TriggerConditionType("arg_eq", "参数等于", "条件/参数", 0)]
    public sealed class ArgEqConditionEditorConfig : ConditionEditorConfigBase
    {
        public override string Type => "arg_eq";

        [LabelText("参数名")]
        public string Key;

        [LabelText("期望值")]
        public ArgValueRefEditor Expected = new ArgValueRefEditor();

        protected override string GetTitleSuffix()
        {
            if (string.IsNullOrEmpty(Key)) return null;
            if (Expected != null && Expected.Source == ValueSourceKind.Var)
            {
                return Key + " == " + (Expected.FromScope == VarScope.Local ? "局部" : "全局") + ":" + (string.IsNullOrEmpty(Expected.FromKey) ? "<空>" : Expected.FromKey);
            }
            return Key + " == " + StrongEditorTitleUtil.FormatArg(Expected != null ? Expected.ConstValue : null);
        }

        public override ConditionRuntimeConfigBase ToRuntimeStrong()
        {
            return new ArgEqConditionConfig
            {
                Key = Key,
                ValueSource = Expected != null ? Expected.Source : ValueSourceKind.Const,
                ValueFromScope = Expected != null ? Expected.FromScope : VarScope.Local,
                ValueFromKey = Expected != null ? Expected.FromKey : null,
                Value = Expected != null && Expected.ConstValue != null ? Expected.ConstValue.Clone() : null
            };
        }
    }

    [Serializable]
    [TriggerConditionType("arg_gt", "参数大于", "条件/参数", 10)]
    public sealed class ArgGreaterThanConditionEditorConfig : ConditionEditorConfigBase
    {
        public override string Type => "arg_gt";

        [LabelText("参数名")]
        public string Key;

        [LabelText("阈值")]
        public ArgValueRefEditor ThresholdRef = new ArgValueRefEditor();

        protected override string GetTitleSuffix()
        {
            if (string.IsNullOrEmpty(Key)) return null;
            if (ThresholdRef != null && ThresholdRef.Source == ValueSourceKind.Var)
            {
                return Key + " > " + (ThresholdRef.FromScope == VarScope.Local ? "局部" : "全局") + ":" + (string.IsNullOrEmpty(ThresholdRef.FromKey) ? "<空>" : ThresholdRef.FromKey);
            }
            var constKind = ThresholdRef != null ? ThresholdRef.GetExpectedKind() : ArgValueKind.None;
            if (constKind == ArgValueKind.Float || constKind == ArgValueKind.Int)
            {
                var boxed = ThresholdRef != null ? ThresholdRef.GetConstBoxedValue() : null;
                if (boxed != null)
                {
                    try
                    {
                        return Key + " > " + System.Convert.ToDouble(boxed).ToString("0.###");
                    }
                    catch
                    {
                    }
                }
            }
            if (ThresholdRef != null && ThresholdRef.ConstValue != null && ThresholdRef.ConstValue.Kind != ArgValueKind.None)
            {
                return Key + " > " + StrongEditorTitleUtil.FormatArg(ThresholdRef.ConstValue);
            }
            return null;
        }

        public override ConditionRuntimeConfigBase ToRuntimeStrong()
        {
            return new ArgGreaterThanConditionConfig
            {
                Key = Key,
                ValueSource = ThresholdRef != null ? ThresholdRef.Source : ValueSourceKind.Const,
                ValueFromScope = ThresholdRef != null ? ThresholdRef.FromScope : VarScope.Local,
                ValueFromKey = ThresholdRef != null ? ThresholdRef.FromKey : null,
                ThresholdValue = ThresholdRef != null && ThresholdRef.ConstValue != null ? ThresholdRef.ConstValue.Clone() : null
            };
        }
    }

    [Serializable]
    [TriggerActionType("set_var", "设置变量", "行为/数据", 0)]
    public sealed class SetVarActionEditorConfig : ActionEditorConfigBase
    {
        public override string Type => "set_var";

        [LabelText("作用域")]
        [ValueDropdown(nameof(GetScopeOptions))]
        public VarScope Scope = VarScope.Local;

        [LabelText("值来源")]
        [ValueDropdown(nameof(GetValueSourceOptions))]
        public ValueSourceKind ValueSource = ValueSourceKind.Const;

        [LabelText("变量名")]
        [InfoBox("当前没有可写变量（可能未定义变量，或变量被标记为只读）", InfoMessageType.Warning, VisibleIf = nameof(HasNoAssignableKeys))]
        [ValueDropdown(nameof(GetAssignableVarKeyOptions))]
        public AbilityKit.Ability.Editor.ScopedVarKey AssignKey
        {
            get => new AbilityKit.Ability.Editor.ScopedVarKey(Scope, Key);
            set
            {
                Scope = value.Scope;
                Key = value.Key;
                VarKeyRecentUtil.Record(value.Scope, value.Key);
            }
        }

        [HideInInspector]
        public string Key;

        [LabelText("变量值")]
        [ShowIf(nameof(IsConstValue))]
        public ArgRuntimeEntry Value = new ArgRuntimeEntry();

        [LabelText("取值作用域")]
        [ShowIf(nameof(IsVarValue))]
        public VarScope ValueFromScope = VarScope.Local;

        [LabelText("取值变量名")]
        [ShowIf(nameof(IsVarValue))]
        [ValueDropdown(nameof(GetReadableVarKeys))]
        public string ValueFromKey;

        private IEnumerable<ValueDropdownItem<AbilityKit.Ability.Editor.ScopedVarKey>> GetAssignableVarKeyOptions()
        {
            var expected = ArgValueKind.None;
            if (ValueSource == ValueSourceKind.Const && Value != null)
            {
                expected = Value.Kind;
            }

            return VarKeyDropdownUtil.BuildScopedKeyOptions(VarKeyUsage.Assign, expected);
        }

        private bool HasNoAssignableKeys()
        {
            var keys = GetAssignableVarKeyOptions();
            if (keys == null) return true;
            using (var e = keys.GetEnumerator())
            {
                return !e.MoveNext();
            }
        }

        private IEnumerable<string> GetReadableVarKeys()
        {
            var expected = Configs.ArgValueKind.None;
            if (!string.IsNullOrEmpty(Key))
            {
                if (VarKeyTypeResolver.TryGetKind(Scope, Key, out var kind))
                {
                    expected = kind;
                }
            }

            return VarKeyDropdownUtil.BuildKeys(ValueFromScope, VarKeyUsage.Read, expected);
        }

        private static IEnumerable<ValueDropdownItem<VarScope>> GetScopeOptions()
        {
            yield return new ValueDropdownItem<VarScope>("局部", VarScope.Local);
            yield return new ValueDropdownItem<VarScope>("全局", VarScope.Global);
        }

        private static IEnumerable<ValueDropdownItem<ValueSourceKind>> GetValueSourceOptions()
        {
            yield return new ValueDropdownItem<ValueSourceKind>("常量", ValueSourceKind.Const);
            yield return new ValueDropdownItem<ValueSourceKind>("变量引用", ValueSourceKind.Var);
        }

        private static string GetScopeDisplayName(VarScope scope)
        {
            switch (scope)
            {
                case VarScope.Local: return "局部";
                case VarScope.Global: return "全局";
                default: return scope.ToString();
            }
        }

        private bool IsConstValue => ValueSource == ValueSourceKind.Const;
        private bool IsVarValue => ValueSource == ValueSourceKind.Var;

        protected override string GetTitleSuffix()
        {
            if (string.IsNullOrEmpty(Key)) return null;
            if (ValueSource == ValueSourceKind.Var)
            {
                return Key + " = " + GetScopeDisplayName(ValueFromScope) + ":" + (string.IsNullOrEmpty(ValueFromKey) ? "<空>" : ValueFromKey);
            }
            return Key + " = " + StrongEditorTitleUtil.FormatArg(Value);
        }

        public override ActionRuntimeConfigBase ToRuntimeStrong()
        {
            return new SetVarActionConfig
            {
                Scope = Scope,
                ValueSource = ValueSource,
                ValueFromScope = ValueFromScope,
                ValueFromKey = ValueFromKey,
                Key = Key,
                Value = Value != null ? Value.Clone() : null
            };
        }
    }

    [Serializable]
    [TriggerActionType("debug_log", "输出日志", "行为/调试", 0)]
    public sealed class DebugLogActionEditorConfig : ActionEditorConfigBase
    {
        public override string Type => "debug_log";

        [LabelText("日志内容")]
        [TextArea]
        public string Message;

        protected override string GetTitleSuffix()
        {
            return StrongEditorTitleUtil.QuoteAndTruncate(Message, 32);
        }

        public override ActionRuntimeConfigBase ToRuntimeStrong()
        {
            return new DebugLogActionConfig
            {
                Message = Message
            };
        }
    }

    [Serializable]
    [TriggerActionType("log_attacker", "输出攻击者名字", "行为/调试", 10)]
    public sealed class LogAttackerNameActionEditorConfig : ActionEditorConfigBase
    {
        public override string Type => "log_attacker";

        [LabelText("格式")]
        public string Format = "{0}发动了攻击";

        protected override string GetTitleSuffix()
        {
            return StrongEditorTitleUtil.QuoteAndTruncate(Format, 32);
        }

        public override ActionRuntimeConfigBase ToRuntimeStrong()
        {
            return new LogAttackerNameActionConfig
            {
                Format = Format
            };
        }
    }

    [Serializable]
    [TriggerActionType("effect_execute", "执行效果", "行为/效果", 0)]
    public sealed class ExecuteEffectActionEditorConfig : ActionEditorConfigBase
    {
        public override string Type => "effect_execute";

        [LabelText("效果Id")]
        public int EffectId;

        protected override string GetTitleSuffix()
        {
            return EffectId > 0 ? EffectId.ToString() : null;
        }

        public override ActionRuntimeConfigBase ToRuntimeStrong()
        {
            return new ExecuteEffectActionConfig
            {
                EffectId = EffectId
            };
        }
    }
}
