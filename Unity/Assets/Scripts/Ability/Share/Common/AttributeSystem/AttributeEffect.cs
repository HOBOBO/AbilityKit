using System;

namespace AbilityKit.Ability.Share.Common.AttributeSystem
{
    public sealed class AttributeEffect
    {
        public readonly Entry[] Entries;

        public AttributeEffect(params Entry[] entries)
        {
            Entries = entries ?? Array.Empty<Entry>();
        }

        public readonly struct Entry
        {
            public readonly AttributeId Attribute;
            public readonly AttributeModifier Modifier;

            public Entry(AttributeId attribute, AttributeModifier modifier)
            {
                Attribute = attribute;
                Modifier = modifier;
            }
        }
    }
}
