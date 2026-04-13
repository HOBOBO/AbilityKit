using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Runtime;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AbilityKit.Ability.Editor
{
    [Serializable]
    [TriggerActionType(TriggerActionTypes.SetVar, "设置变量", "行为/数据", 0)]
    public sealed class SetVarActionEditorConfig : ActionEditorConfigBase
    {
        public override string Type => TriggerActionTypes.SetVar;

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
        public ArgRuntimeEntryCore Value = new ArgRuntimeEntryCore();

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
            var expected = ArgValueKind.None;
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

        public override ActionConfigBase ToRuntimeConfig()
        {
            return new SetVarActionConfig
            {
                Scope = Scope,
                ValueSource = ValueSource,
                ValueFromScope = ValueFromScope,
                ValueFromKey = ValueFromKey,
                Key = Key,
                Value = Value
            };
        }
    }
}
