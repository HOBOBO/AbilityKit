using System;
using System.Collections.Generic;
using AbilityKit.Ability.Triggering.Definitions;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AbilityKit.Ability.Configs
{
    [Serializable]
    public sealed class TriggerRuntimeConfig
    {
        public string EventId;

        public List<ArgRuntimeEntry> LocalVars = new List<ArgRuntimeEntry>();

        [SerializeReference]
        public List<ConditionRuntimeConfigBase> ConditionsStrong = new List<ConditionRuntimeConfigBase>();

        [SerializeReference]
        public List<ActionRuntimeConfigBase> ActionsStrong = new List<ActionRuntimeConfigBase>();

        public TriggerDef ToTriggerDef()
        {
            var conditions = new List<ConditionDef>(ConditionsStrong != null ? ConditionsStrong.Count : 0);
            if (ConditionsStrong != null)
            {
                for (int i = 0; i < ConditionsStrong.Count; i++)
                {
                    var c = ConditionsStrong[i];
                    if (c == null) continue;
                    conditions.Add(c.ToConditionDef());
                }
            }

            var actions = new List<ActionDef>(ActionsStrong != null ? ActionsStrong.Count : 0);
            if (ActionsStrong != null)
            {
                for (int i = 0; i < ActionsStrong.Count; i++)
                {
                    var a = ActionsStrong[i];
                    if (a == null) continue;
                    actions.Add(a.ToActionDef());
                }
            }

            return new TriggerDef(EventId, conditions, actions);
        }
    }

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
        public UnityEngine.Object ObjectValue;

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
