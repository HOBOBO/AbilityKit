using System.Collections.Generic;
using AbilityKit.Configs;
using Sirenix.OdinInspector;
using AbilityKit.Triggering;

namespace AbilityKit.Ability.Editor
{
    [System.Serializable]
    [InlineProperty]
    [HideLabel]
    public sealed class ArgValueRefEditor
    {
        private static ArgValueKind _lastConstKind = ArgValueKind.Float;

        [LabelText("值来源")]
        [ValueDropdown(nameof(GetValueSourceOptions))]
        public ValueSourceKind Source = ValueSourceKind.Const;

        [LabelText("常量")]
        [ShowIf(nameof(IsConst))]
        public ArgRuntimeEntry ConstValue = new ArgRuntimeEntry();

        [LabelText("取值作用域")]
        [ShowIf(nameof(IsVar))]
        public VarScope FromScope = VarScope.Local;

        [LabelText("变量名")]
        [InfoBox("当前作用域下没有可用变量", InfoMessageType.Warning, VisibleIf = nameof(HasNoFromKeys))]
        [ShowIf(nameof(IsVar))]
        [ValueDropdown(nameof(GetFromKeyOptions))]
        [OnValueChanged(nameof(OnFromKeyChanged))]
        public string FromKey;

        private bool IsConst => Source == ValueSourceKind.Const;
        private bool IsVar => Source == ValueSourceKind.Var;

        [OnInspectorGUI]
        private void KeepLastConstKind()
        {
            if (!IsConst) return;
            if (ConstValue == null) ConstValue = new ArgRuntimeEntry();

            if (ConstValue.Kind == ArgValueKind.None)
            {
                ConstValue.Kind = _lastConstKind;
            }
            else
            {
                _lastConstKind = ConstValue.Kind;
            }
        }

        public ArgValueKind GetExpectedKind()
        {
            if (ConstValue == null) return ArgValueKind.None;
            return ConstValue.Kind;
        }

        public object GetConstBoxedValue()
        {
            return ConstValue != null ? ConstValue.GetBoxedValue() : null;
        }

        private IEnumerable<string> GetFromKeyOptions()
        {
            var expected = GetExpectedKind();
            return VarKeyDropdownUtil.BuildKeys(FromScope, VarKeyUsage.Read, expected);
        }

        private bool HasNoFromKeys()
        {
            if (!IsVar) return false;
            var keys = GetFromKeyOptions();
            if (keys == null) return true;
            using (var e = keys.GetEnumerator())
            {
                return !e.MoveNext();
            }
        }

        private void OnFromKeyChanged()
        {
            if (!IsVar) return;
            if (string.IsNullOrEmpty(FromKey)) return;
            VarKeyRecentUtil.Record(FromScope, FromKey);
        }

        private static IEnumerable<ValueDropdownItem<ValueSourceKind>> GetValueSourceOptions()
        {
            yield return new ValueDropdownItem<ValueSourceKind>("常量", ValueSourceKind.Const);
            yield return new ValueDropdownItem<ValueSourceKind>("变量引用", ValueSourceKind.Var);
        }
    }
}
