#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace AbilityKit.Pipeline.Editor
{
    /// <summary>
    /// Editor 端管线诊断初始化。仅订阅运行时观测钩子，不替换业务 Registry 或 TraceRecorder。
    /// </summary>
    [UnityEditor.InitializeOnLoad]
    public static class PipelineEditorInitializer
    {
        private static bool _installed;

        static PipelineEditorInitializer()
        {
            Install();
            AssemblyReloadEvents.beforeAssemblyReload -= Uninstall;
            AssemblyReloadEvents.beforeAssemblyReload += Uninstall;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EditorInitialize()
        {
            Install();
        }

        internal static bool IsInstalled => _installed;

        internal static void Install()
        {
            if (_installed) return;
            var registry = EditorPipelineRegistry.Instance;
            var state = PipelineDebuggerUserState.instance;
            registry.IsCaptureEnabled = state.CaptureEnabled;
            registry.ConfigureStorage(state.HistoryCapacity, state.TraceCapacity);
            registry.Initialize();
            PipelineDebugHooks.OnRunStartedDetailed += registry.CaptureRunStarted;
            PipelineDebugHooks.OnTrace += registry.CaptureTrace;
            PipelineDebugHooks.OnRunEnded += registry.CaptureRunEnded;
            _installed = true;
        }

        internal static void Uninstall()
        {
            if (!_installed) return;
            var registry = EditorPipelineRegistry.Instance;
            PipelineDebugHooks.OnRunStartedDetailed -= registry.CaptureRunStarted;
            PipelineDebugHooks.OnTrace -= registry.CaptureTrace;
            PipelineDebugHooks.OnRunEnded -= registry.CaptureRunEnded;
            registry.Shutdown();
            _installed = false;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode)
            {
                EditorPipelineRegistry.Instance.MarkActiveRunsEnded();
            }
        }
    }
}

#endif
