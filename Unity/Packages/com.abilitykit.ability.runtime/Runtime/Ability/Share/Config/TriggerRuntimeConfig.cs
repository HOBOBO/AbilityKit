using System;
using Sirenix.OdinInspector;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AbilityKit.Ability.Configs
{
    public enum ArgValueKind
    {
        None = 0,
        Int = 1,
        Float = 2,
        Bool = 3,
        String = 4,
        Object = 5
    }

    [Serializable]
    public sealed class ArgRuntimeEntry
    {
        public string Key;
        public ArgValueKind Kind;

        [ShowIf(nameof(IsInt))]
        public int IntValue;

        [ShowIf(nameof(IsFloat))]
        public float FloatValue;

        [ShowIf(nameof(IsBool))]
        public bool BoolValue;

        [ShowIf(nameof(IsString))]
        public string StringValue;

        [ShowIf(nameof(IsObject))]
        public Object ObjectValue;

        private bool IsInt => Kind == ArgValueKind.Int;
        private bool IsFloat => Kind == ArgValueKind.Float;
        private bool IsBool => Kind == ArgValueKind.Bool;
        private bool IsString => Kind == ArgValueKind.String;
        private bool IsObject => Kind == ArgValueKind.Object;

        public ArgRuntimeEntry Clone()
        {
            return new ArgRuntimeEntry
            {
                Key = Key,
                Kind = Kind,
                IntValue = IntValue,
                FloatValue = FloatValue,
                BoolValue = BoolValue,
                StringValue = StringValue,
                ObjectValue = ObjectValue
            };
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
