using System;
using System.Collections.Generic;
using AbilityKit.Ability.Triggering;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AbilityKit.Ability.Configs
{
    [CreateAssetMenu(menuName = "AbilityKit/Global Vars", fileName = "GlobalVars")]
    public sealed class GlobalVarsSO : ScriptableObject
    {
        [InfoBox("@GetValidationMessage()", InfoMessageType.Error, VisibleIf = nameof(HasValidationError))]
        [PropertyOrder(-20)]
        [ButtonGroup("Add", Order = -10)]
        private void AddInt() => AddEntry(ArgValueKind.Int);

        [ButtonGroup("Add", Order = -10)]
        private void AddFloat() => AddEntry(ArgValueKind.Float);

        [ButtonGroup("Add", Order = -10)]
        private void AddBool() => AddEntry(ArgValueKind.Bool);

        [ButtonGroup("Add", Order = -10)]
        private void AddString() => AddEntry(ArgValueKind.String);

        [ButtonGroup("Add", Order = -10)]
        private void AddObject() => AddEntry(ArgValueKind.Object);

        [ListDrawerSettings(Expanded = true)]
        public List<GlobalVarEntry> Vars = new List<GlobalVarEntry>();

        public void ApplyToGlobalStore()
        {
            if (Vars == null) return;

            for (int i = 0; i < Vars.Count; i++)
            {
                var e = Vars[i];
                if (e == null || string.IsNullOrEmpty(e.Key)) continue;
                GlobalVarStore.Set(e.Key, e.GetBoxedValue());
            }
        }

        [Button("Apply To GlobalVarStore")]
        private void ApplyToGlobalStoreButton()
        {
            ApplyToGlobalStore();
        }

        [Button("Clear GlobalVarStore")]
        private void ClearGlobalStoreButton()
        {
            GlobalVarStore.Clear();
        }

        private void AddEntry(ArgValueKind kind)
        {
            if (Vars == null) Vars = new List<GlobalVarEntry>();
            Vars.Add(new GlobalVarEntry { Kind = kind });
        }

        private bool HasValidationError()
        {
            if (Vars == null || Vars.Count == 0) return false;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < Vars.Count; i++)
            {
                var e = Vars[i];
                if (e == null) continue;
                if (string.IsNullOrEmpty(e.Key)) return true;
                if (!seen.Add(e.Key)) return true;
            }

            return false;
        }

        private string GetValidationMessage()
        {
            if (Vars == null || Vars.Count == 0) return string.Empty;

            var empty = 0;
            var duplicates = new HashSet<string>(StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < Vars.Count; i++)
            {
                var e = Vars[i];
                if (e == null) continue;
                if (string.IsNullOrEmpty(e.Key))
                {
                    empty++;
                    continue;
                }
                if (!seen.Add(e.Key))
                {
                    duplicates.Add(e.Key);
                }
            }

            var parts = new List<string>();
            if (empty > 0) parts.Add("瀛樺湪绌?Key: " + empty);
            if (duplicates.Count > 0) parts.Add("Key 閲嶅: " + string.Join(", ", duplicates));
            return string.Join("\n", parts);
        }
    }

    [Serializable]
    [InlineProperty]
    [HideLabel]
    public sealed class GlobalVarEntry
    {
        [HorizontalGroup("Row", Width = 220)]
        [GUIColor(nameof(GetKeyColor))]
        public string Key;

        [HorizontalGroup("Row", Width = 110)]
        public ArgValueKind Kind;

        [HorizontalGroup("Row", Width = 56)]
        [LabelText("只读")]
        public bool ReadOnly;

        [HorizontalGroup("Row")]
        [ShowIf(nameof(IsInt))]
        public int IntValue;

        [HorizontalGroup("Row")]
        [ShowIf(nameof(IsFloat))]
        public float FloatValue;

        [HorizontalGroup("Row")]
        [ShowIf(nameof(IsBool))]
        public bool BoolValue;

        [HorizontalGroup("Row")]
        [ShowIf(nameof(IsString))]
        public string StringValue;

        [HorizontalGroup("Row")]
        [ShowIf(nameof(IsObject))]
        public UnityEngine.Object ObjectValue;

        private bool IsInt => Kind == ArgValueKind.Int;
        private bool IsFloat => Kind == ArgValueKind.Float;
        private bool IsBool => Kind == ArgValueKind.Bool;
        private bool IsString => Kind == ArgValueKind.String;
        private bool IsObject => Kind == ArgValueKind.Object;

        private Color GetKeyColor()
        {
            return string.IsNullOrEmpty(Key) ? new Color(1f, 0.6f, 0.6f) : Color.white;
        }

        public object GetBoxedValue()
        {
            switch (Kind)
            {
                case ArgValueKind.Int:
                    return IntValue;
                case ArgValueKind.Float:
                    return FloatValue;
                case ArgValueKind.Bool:
                    return BoolValue;
                case ArgValueKind.String:
                    return StringValue;
                case ArgValueKind.Object:
                    return ObjectValue;
                default:
                    return null;
            }
        }
    }
}
