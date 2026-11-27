using System.Collections.Generic;


namespace AbilityKit.HFSM.Extension
{

    public sealed class ActionBehaviourSnapshot
    {
        public ActionBehaviourSnapshot(
            string kind,
            int integerValue = 0,
            float floatValue = 0f,
            bool booleanValue = false,
            IReadOnlyList<ActionBehaviourSnapshot> children = null)
        {
            Kind = kind ?? string.Empty;
            IntegerValue = integerValue;
            FloatValue = floatValue;
            BooleanValue = booleanValue;
            Children = children ?? System.Array.Empty<ActionBehaviourSnapshot>();
        }

        public string Kind { get; }

        public int IntegerValue { get; }

        public float FloatValue { get; }

        public bool BooleanValue { get; }

        public IReadOnlyList<ActionBehaviourSnapshot> Children { get; }
    }
}
