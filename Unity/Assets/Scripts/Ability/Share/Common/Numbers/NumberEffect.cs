using System;

namespace AbilityKit.Ability.Share.Common.Numbers
{
    public sealed class NumberEffect
    {
        public readonly Entry[] Entries;

        public NumberEffect(params Entry[] entries)
        {
            Entries = entries ?? Array.Empty<Entry>();
        }

        public readonly struct Entry
        {
            public readonly NumberModifier Modifier;

            public Entry(NumberModifier modifier)
            {
                Modifier = modifier;
            }
        }
    }
}
