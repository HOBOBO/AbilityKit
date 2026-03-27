// Auto-define HFSM_UNITY based on Unity platform defines
#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS || UNITY_SERVER || UNITY_SERVER
#define HFSM_UNITY
#endif

using System;

namespace UnityHFSM.Graph
{
    /// <summary>
    /// Logging interface for HFSM.
    /// </summary>
    public static class HfsmLog
    {
        public static void Log(string message)
        {
#if HFSM_UNITY
            UnityEngine.Debug.Log($"[HFSM] {message}");
#else
            Console.WriteLine($"[HFSM] {message}");
#endif
        }

        public static void LogWarning(string message)
        {
#if HFSM_UNITY
            UnityEngine.Debug.LogWarning($"[HFSM] {message}");
#else
            Console.WriteLine($"[HFSM Warning] {message}");
#endif
        }

        public static void LogError(string message)
        {
#if HFSM_UNITY
            UnityEngine.Debug.LogError($"[HFSM] {message}");
#else
            Console.WriteLine($"[HFSM Error] {message}");
#endif
        }
    }
}
