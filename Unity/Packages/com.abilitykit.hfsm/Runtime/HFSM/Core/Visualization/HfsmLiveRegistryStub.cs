// ============================================================================
// Visualization namespace stub for Core layer
// This stub allows Core layer code to compile without actual implementation
// The real implementation is in Unity layer (HfsmLiveRegistry.cs)
// ============================================================================

#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS || UNITY_SERVER
#define HFSM_UNITY
#endif

#if HFSM_UNITY
using System;
using System.Collections.Generic;

namespace UnityHFSM.Visualization
{
    /// <summary>
    /// Stub for LiveRegistry - auto-register is disabled in Core layer
    /// Real implementation is in Unity layer
    /// </summary>
    internal static class HfsmLiveRegistry
    {
        public static bool AutoRegisterEnabled = false;
        public static Predicate<object> AutoRegisterFilter;
        public static Func<object, string> AutoRegisterNameProvider;
        public static event Action Changed;

        public static void AutoRegister(object fsm) { }
        public static void Register(string name, object fsm) { }
        public static void Unregister(object fsm) { }
        public static IReadOnlyList<object> GetEntries() => Array.Empty<object>();
    }
}
#endif
