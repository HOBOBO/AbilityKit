using System;

namespace AbilityKit.Core.Numerics
{
    [Obsolete(NumericsDeprecation.Message)]
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
