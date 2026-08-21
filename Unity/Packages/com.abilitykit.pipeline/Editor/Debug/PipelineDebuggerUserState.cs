#if UNITY_EDITOR

#nullable enable

using UnityEditor;
using UnityEngine;

namespace AbilityKit.Pipeline.Editor
{
    [FilePath("UserSettings/AbilityKit/PipelineDebuggerState.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class PipelineDebuggerUserState : ScriptableSingleton<PipelineDebuggerUserState>
    {
        [SerializeField] private bool _captureEnabled = true;
        [SerializeField] private bool _followLatest = true;
        [SerializeField] private bool _relativeTraceTime = true;
        [SerializeField] private bool _confirmInterrupt = true;
        [SerializeField] private bool _showOnlyChangedContext;
        [SerializeField] private bool _showPhaseGraph = true;
        [SerializeField] private int _runFilter;
        [SerializeField] private int _detailTab;
        [SerializeField] private int _traceFilter;
        [SerializeField] private string _runSearch = string.Empty;
        [SerializeField] private string _traceSearch = string.Empty;
        [SerializeField] private string _contextSearch = string.Empty;
        [SerializeField] private float _runPaneWidth = 300f;
        [SerializeField] private float _refreshIntervalSeconds = 0.1f;
        [SerializeField] private int _historyCapacity = 128;
        [SerializeField] private int _traceCapacity = 2048;

        public bool CaptureEnabled { get => _captureEnabled; set => _captureEnabled = value; }
        public bool FollowLatest { get => _followLatest; set => _followLatest = value; }
        public bool RelativeTraceTime { get => _relativeTraceTime; set => _relativeTraceTime = value; }
        public bool ConfirmInterrupt { get => _confirmInterrupt; set => _confirmInterrupt = value; }
        public bool ShowOnlyChangedContext { get => _showOnlyChangedContext; set => _showOnlyChangedContext = value; }
        public bool ShowPhaseGraph { get => _showPhaseGraph; set => _showPhaseGraph = value; }
        public int RunFilter { get => _runFilter; set => _runFilter = value; }
        public int DetailTab { get => _detailTab; set => _detailTab = value; }
        public int TraceFilter { get => _traceFilter; set => _traceFilter = value; }
        public string RunSearch { get => _runSearch; set => _runSearch = value ?? string.Empty; }
        public string TraceSearch { get => _traceSearch; set => _traceSearch = value ?? string.Empty; }
        public string ContextSearch { get => _contextSearch; set => _contextSearch = value ?? string.Empty; }
        public float RunPaneWidth { get => _runPaneWidth; set => _runPaneWidth = Mathf.Clamp(value, 220f, 520f); }
        public float RefreshIntervalSeconds { get => _refreshIntervalSeconds; set => _refreshIntervalSeconds = Mathf.Clamp(value, 0.05f, 1f); }
        public int HistoryCapacity { get => _historyCapacity; set => _historyCapacity = Mathf.Clamp(value, 0, 2048); }
        public int TraceCapacity { get => _traceCapacity; set => _traceCapacity = Mathf.Clamp(value, 64, 32768); }

        public void SaveNow()
        {
            Save(true);
        }
    }
}

#endif
