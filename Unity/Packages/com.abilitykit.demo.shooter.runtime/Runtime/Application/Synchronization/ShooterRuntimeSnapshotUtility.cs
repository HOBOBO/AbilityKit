using System;
using System.Collections.Generic;

namespace AbilityKit.Demo.Shooter.Runtime
{
    public static class ShooterRuntimeSnapshotUtility
    {
        public static int[] CopyAndSort(IReadOnlyCollection<int> ids)
        {
            if (ids == null) throw new ArgumentNullException(nameof(ids));

            var sorted = new int[ids.Count];
            CopyAndSort(ids, sorted);
            return sorted;
        }

        public static int CopyAndSort(IReadOnlyCollection<int> ids, int[] destination)
        {
            if (ids == null) throw new ArgumentNullException(nameof(ids));
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (destination.Length < ids.Count) throw new ArgumentException("Destination buffer is smaller than the source collection.", nameof(destination));

            var index = 0;
            foreach (var id in ids)
            {
                destination[index++] = id;
            }

            Array.Sort(destination, 0, index);
            return index;
        }

        public static int Quantize(float value)
        {
            // Promote the source float before scaling. Keeping the multiply in single
            // precision lets different JITs retain or round the intermediate value at
            // different points, which changes midpoint results across .NET and Mono.
            return (int)Math.Round((double)value * 10000d, MidpointRounding.ToEven);
        }
    }
}
