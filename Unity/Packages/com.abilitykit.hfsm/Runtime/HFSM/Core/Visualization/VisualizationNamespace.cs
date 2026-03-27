using System;
using System.Collections.Generic;

// Stub namespace for Core layer compatibility
// Visualization types are only available in Unity layer (wrapped in UNITY_EDITOR)
// This stub is internal so it's only visible within AbilityKit.HFSM.Core
// and not exposed to referencing assemblies like AbilityKit.HFSM.
// When AbilityKit.HFSM references both Core and Unity, the public
// HfsmLiveRegistry from Unity takes precedence.
namespace UnityHFSM.Visualization
{
    internal static class HfsmLiveRegistry
    {
        public static bool AutoRegisterEnabled = false;
        public static Predicate<object> AutoRegisterFilter;
        public static Func<object, string> AutoRegisterNameProvider;
        public static event Action Changed;

        public static void AutoRegister(object fsm) { }
        public static void Register(string name, object fsm) { }
        public static void Unregister(object fsm) { }
        public static IReadOnlyList<Entry> GetEntries() => Array.Empty<Entry>();

        public sealed class Entry
        {
            public Entry(string name, object fsm) { }
        }
    }
}
