using System;

namespace AbilityKit.Ability.Share.Common.AttributeSystem
{
    public sealed class AttributeDef
    {
        public AttributeId Id;
        public string Name;
        public string Group;
        public float DefaultBaseValue;
        public IAttributeFormula Formula;
        public IAttributeConstraint Constraint;

        public AttributeDef(string name, string group = null, float defaultBaseValue = 0f, IAttributeFormula formula = null, IAttributeConstraint constraint = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Group = group;
            DefaultBaseValue = defaultBaseValue;
            Formula = formula;
            Constraint = constraint;
        }
    }
}
