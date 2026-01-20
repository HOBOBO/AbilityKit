using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.HotReload
{
    public static class HotReloadStaticRegistry
    {
        private readonly struct Entry
        {
            public readonly string Id;
            public readonly Action Reset;

            public Entry(string id, Action reset)
            {
                Id = id;
                Reset = reset;
            }
        }

        private static readonly List<Entry> Entries = new List<Entry>(64);

        public static void Register(string id, Action reset)
        {
            if (reset == null) throw new ArgumentNullException(nameof(reset));
            Entries.Add(new Entry(id, reset));
        }

        public static void Clear()
        {
            Entries.Clear();
        }

        public static void ResetAll()
        {
            for (var i = 0; i < Entries.Count; i++)
            {
                try { Entries[i].Reset?.Invoke(); }
                catch { }
            }
        }
    }
}
