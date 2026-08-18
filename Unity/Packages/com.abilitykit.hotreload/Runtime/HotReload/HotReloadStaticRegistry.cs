#nullable enable
using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.HotReload
{
    /// <summary>Stores explicit static reset callbacks by stable identifier.</summary>
    public static class HotReloadStaticRegistry
    {
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<string, Action> Entries = new Dictionary<string, Action>(StringComparer.Ordinal);

        /// <summary>Registers or replaces one reset callback.</summary>
        public static void Register(string id, Action reset)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A non-empty reset id is required.", nameof(id));
            if (reset == null) throw new ArgumentNullException(nameof(reset));
            lock (SyncRoot)
            {
                Entries[id] = reset;
            }
        }

        /// <summary>Removes a reset callback without invoking it.</summary>
        public static bool Unregister(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            lock (SyncRoot)
            {
                return Entries.Remove(id);
            }
        }

        /// <summary>Removes all callback registrations without invoking them.</summary>
        public static void Clear()
        {
            lock (SyncRoot)
            {
                Entries.Clear();
            }
        }

        /// <summary>Runs every callback and aggregates all failures.</summary>
        public static void ResetAll()
        {
            Action[] resets;
            lock (SyncRoot)
            {
                resets = new Action[Entries.Count];
                Entries.Values.CopyTo(resets, 0);
            }

            List<Exception>? errors = null;
            for (var i = 0; i < resets.Length; i++)
            {
                try
                {
                    resets[i]();
                }
                catch (Exception e)
                {
                    if (errors == null)
                        errors = new List<Exception>();
                    errors.Add(e);
                }
            }

            if (errors != null)
                throw new AggregateException("One or more hot-reload static reset callbacks failed.", errors);
        }
    }
}
