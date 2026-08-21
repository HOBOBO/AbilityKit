#if UNITY_EDITOR

#nullable enable

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Pipeline.Editor
{
    [Serializable]
    public sealed class PipelineDebugPhaseSnapshot
    {
        [SerializeField] private string _path = string.Empty;
        [SerializeField] private string _nodeKey = string.Empty;
        [SerializeField] private string _phaseId = string.Empty;
        [SerializeField] private string _phaseType = string.Empty;
        [SerializeField] private string _nodeKind = string.Empty;
        [SerializeField] private string _summary = string.Empty;
        [SerializeField] private string _executionState = string.Empty;
        [SerializeField] private int _selectedChildIndex = -1;
        [SerializeField] private List<string> _conditionResults = new List<string>();
        [SerializeField] private Vector2 _position;
        [SerializeField] private bool _hasPosition;
        [SerializeField] private int _depth;

        internal PipelineDebugPhaseSnapshot(
            string path,
            PipelinePhaseDebugNode node,
            int depth,
            PipelinePhaseDebugState? state,
            bool hasPosition,
            Vector2 position)
        {
            _path = path;
            _nodeKey = node.NodeKey;
            _phaseId = node.PhaseId.ToString();
            _phaseType = node.PhaseType;
            _nodeKind = node.Kind.ToString();
            _summary = node.Summary;
            _executionState = state?.State.ToString() ?? EPipelineDebugExecutionState.Pending.ToString();
            _selectedChildIndex = state?.SelectedChildIndex ?? -1;
            _conditionResults.Clear();
            if (state != null)
            {
                for (int i = 0; i < state.ChildConditions.Count; i++)
                {
                    _conditionResults.Add(state.ChildConditions[i].ToString());
                }
            }
            _hasPosition = hasPosition;
            _position = position;
            _depth = depth;
        }

        public string Path => _path;
        public string NodeKey => _nodeKey;
        public string PhaseId => _phaseId;
        public string PhaseType => _phaseType;
        public string NodeKind => _nodeKind;
        public string Summary => _summary;
        public string ExecutionState => _executionState;
        public int SelectedChildIndex => _selectedChildIndex;
        public IReadOnlyList<string> ConditionResults => _conditionResults;
        public Vector2 Position => _position;
        public bool HasPosition => _hasPosition;
        public int Depth => _depth;
    }

    [Serializable]
    public sealed class PipelineDebugEdgeSnapshot
    {
        [SerializeField] private string _sourceNodeKey = string.Empty;
        [SerializeField] private string _targetNodeKey = string.Empty;
        [SerializeField] private string _kind = string.Empty;
        [SerializeField] private string _label = string.Empty;
        [SerializeField] private int _childIndex = -1;

        internal PipelineDebugEdgeSnapshot(PipelinePhaseDebugEdge edge)
        {
            _sourceNodeKey = edge.SourceNodeKey;
            _targetNodeKey = edge.TargetNodeKey;
            _kind = edge.Kind.ToString();
            _label = edge.Label;
            _childIndex = edge.ChildIndex;
        }

        public string SourceNodeKey => _sourceNodeKey;
        public string TargetNodeKey => _targetNodeKey;
        public string Kind => _kind;
        public string Label => _label;
        public int ChildIndex => _childIndex;
    }

    [Serializable]
    public sealed class PipelineDebugTraceSnapshot
    {
        [SerializeField] private int _sequence;
        [SerializeField] private double _relativeSeconds;
        [SerializeField] private string _utcTime = string.Empty;
        [SerializeField] private string _eventType = string.Empty;
        [SerializeField] private string _state = string.Empty;
        [SerializeField] private string _phaseId = string.Empty;
        [SerializeField] private string _message = string.Empty;

        internal PipelineDebugTraceSnapshot(PipelineTraceEvent item, DateTime startedAtUtc)
        {
            _sequence = item.Seq;
            _relativeSeconds = Math.Max(0d, (item.UtcTime - startedAtUtc).TotalSeconds);
            _utcTime = item.UtcTime.ToString("O");
            _eventType = item.Type.ToString();
            _state = item.State.ToString();
            _phaseId = item.PhaseId.ToString();
            _message = item.Message ?? string.Empty;
        }

        public int Sequence => _sequence;
        public double RelativeSeconds => _relativeSeconds;
        public string UtcTime => _utcTime;
        public string EventType => _eventType;
        public string State => _state;
        public string PhaseId => _phaseId;
        public string Message => _message;
    }

    [Serializable]
    public sealed class PipelineDebugValueSnapshot
    {
        [SerializeField] private string _name = string.Empty;
        [SerializeField] private string _value = string.Empty;

        internal PipelineDebugValueSnapshot(string name, string value)
        {
            _name = name;
            _value = value;
        }

        public string Name => _name;
        public string Value => _value;
    }

    /// <summary>
    /// Explicit, immutable capture of one debugger run. It never owns live runtime objects.
    /// </summary>
    public sealed class PipelineDebugSessionAsset : ScriptableObject
    {
        [SerializeField] private int _formatVersion = 3;
        [SerializeField] private int _runId;
        [SerializeField] private string _ownerName = string.Empty;
        [SerializeField] private string _state = string.Empty;
        [SerializeField] private string _currentPhase = string.Empty;
        [SerializeField] private string _pipelineType = string.Empty;
        [SerializeField] private string _configType = string.Empty;
        [SerializeField] private string _contextType = string.Empty;
        [SerializeField] private string _startedAtUtc = string.Empty;
        [SerializeField] private string _endedAtUtc = string.Empty;
        [SerializeField] private double _elapsedSeconds;
        [SerializeField] private double _wallDurationSeconds;
        [SerializeField] private string _structureId = string.Empty;
        [SerializeField] private string _layoutSource = string.Empty;
        [SerializeField] private List<PipelineDebugPhaseSnapshot> _phases = new List<PipelineDebugPhaseSnapshot>();
        [SerializeField] private List<PipelineDebugEdgeSnapshot> _edges = new List<PipelineDebugEdgeSnapshot>();
        [SerializeField] private List<PipelineDebugTraceSnapshot> _trace = new List<PipelineDebugTraceSnapshot>();
        [SerializeField] private List<PipelineDebugValueSnapshot> _initialContext = new List<PipelineDebugValueSnapshot>();
        [SerializeField] private List<PipelineDebugValueSnapshot> _context = new List<PipelineDebugValueSnapshot>();

        public int FormatVersion => _formatVersion;
        public int RunId => _runId;
        public string OwnerName => _ownerName;
        public string State => _state;
        public string CurrentPhase => _currentPhase;
        public string PipelineType => _pipelineType;
        public string ConfigType => _configType;
        public string ContextType => _contextType;
        public string StartedAtUtc => _startedAtUtc;
        public string EndedAtUtc => _endedAtUtc;
        public double ElapsedSeconds => _elapsedSeconds;
        public double WallDurationSeconds => _wallDurationSeconds;
        public string StructureId => _structureId;
        public string LayoutSource => _layoutSource;
        public IReadOnlyList<PipelineDebugPhaseSnapshot> Phases => _phases;
        public IReadOnlyList<PipelineDebugEdgeSnapshot> Edges => _edges;
        public IReadOnlyList<PipelineDebugTraceSnapshot> Trace => _trace;
        public IReadOnlyList<PipelineDebugValueSnapshot> InitialContext => _initialContext;
        public IReadOnlyList<PipelineDebugValueSnapshot> Context => _context;

        internal void Capture(
            EditorPipelineRegistry.DebugEntry entry,
            IReadOnlyList<PipelineTraceEvent> trace)
        {
            _formatVersion = 3;
            _runId = entry.OwnerId;
            _ownerName = entry.OwnerName;
            _state = entry.IsPaused ? "Executing (Paused)" : entry.LastState.ToString();
            _currentPhase = entry.LastPhaseId.ToString();
            _pipelineType = entry.PipelineType;
            _configType = entry.ConfigType;
            _contextType = entry.ContextType;
            _startedAtUtc = entry.RegisteredAtUtc.ToString("O");
            _endedAtUtc = entry.EndedAtUtc?.ToString("O") ?? string.Empty;
            _elapsedSeconds = entry.ElapsedTime;
            _wallDurationSeconds = entry.WallDurationSeconds;
            _structureId = entry.Graph.StructureId;
            _layoutSource = entry.GraphLayout?.SourceName ?? "Auto";

            _phases.Clear();
            for (int i = 0; i < entry.PhaseTree.Count; i++)
            {
                CapturePhase(entry, entry.PhaseTree[i], i.ToString(), 0);
            }

            _edges.Clear();
            for (int i = 0; i < entry.Graph.Edges.Count; i++)
            {
                _edges.Add(new PipelineDebugEdgeSnapshot(entry.Graph.Edges[i]));
            }

            _trace.Clear();
            for (int i = 0; i < trace.Count; i++)
            {
                _trace.Add(new PipelineDebugTraceSnapshot(trace[i], entry.RegisteredAtUtc));
            }

            _context.Clear();
            _initialContext.Clear();
            for (int i = 0; i < entry.InitialContextValues.Count; i++)
            {
                var value = entry.InitialContextValues[i];
                _initialContext.Add(new PipelineDebugValueSnapshot(value.Name, value.Value));
            }
            for (int i = 0; i < entry.ContextValues.Count; i++)
            {
                var value = entry.ContextValues[i];
                _context.Add(new PipelineDebugValueSnapshot(value.Name, value.Value));
            }
        }

        private void CapturePhase(
            EditorPipelineRegistry.DebugEntry entry,
            PipelinePhaseDebugNode node,
            string path,
            int depth)
        {
            PipelinePhaseDebugState? state = FindPhaseState(entry.PhaseStates, node.NodeKey);
            bool hasPosition = TryFindLayout(entry.GraphLayout, node.NodeKey, out Vector2 position);
            _phases.Add(new PipelineDebugPhaseSnapshot(path, node, depth, state, hasPosition, position));
            for (int i = 0; i < node.Children.Count; i++)
            {
                CapturePhase(entry, node.Children[i], path + "/" + i, depth + 1);
            }
        }

        private static PipelinePhaseDebugState? FindPhaseState(
            IReadOnlyList<PipelinePhaseDebugState> states,
            string nodeKey)
        {
            for (int i = 0; i < states.Count; i++)
            {
                if (states[i].NodeKey == nodeKey) return states[i];
            }
            return null;
        }

        private static bool TryFindLayout(
            PipelineDebugGraphLayout? layout,
            string nodeKey,
            out Vector2 position)
        {
            if (layout != null)
            {
                for (int i = 0; i < layout.Nodes.Count; i++)
                {
                    if (layout.Nodes[i].NodeKey != nodeKey) continue;
                    position = new Vector2(layout.Nodes[i].X, layout.Nodes[i].Y);
                    return true;
                }
            }
            position = default;
            return false;
        }
    }

    [CustomEditor(typeof(PipelineDebugSessionAsset))]
    internal sealed class PipelineDebugSessionAssetEditor : UnityEditor.Editor
    {
        private bool _showPhases = true;
        private bool _showTrace = true;
        private bool _showContext = true;
        private Vector2 _scroll;

        public override void OnInspectorGUI()
        {
            var session = (PipelineDebugSessionAsset)target;
            EditorGUILayout.HelpBox(
                "Read-only diagnostic snapshot. This asset contains copied values only and never references the live run.",
                MessageType.Info);

            DrawValue("Run", session.OwnerName + "  #" + session.RunId);
            DrawValue("State", session.State);
            DrawValue("Current phase", session.CurrentPhase);
            DrawValue("Pipeline", session.PipelineType);
            DrawValue("Config", session.ConfigType);
            DrawValue("Context", session.ContextType);
            DrawValue("Started UTC", session.StartedAtUtc);
            DrawValue("Ended UTC", string.IsNullOrEmpty(session.EndedAtUtc) ? "Running at capture time" : session.EndedAtUtc);
            DrawValue("Elapsed", session.ElapsedSeconds.ToString("0.000") + " s");
            DrawValue("Wall duration", session.WallDurationSeconds.ToString("0.000") + " s");
            DrawValue("Structure", session.StructureId);
            DrawValue("Layout", session.LayoutSource);

            EditorGUILayout.Space(6f);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            _showPhases = EditorGUILayout.Foldout(_showPhases, "Phases (" + session.Phases.Count + ")", true);
            if (_showPhases)
            {
                for (int i = 0; i < session.Phases.Count; i++)
                {
                    var phase = session.Phases[i];
                    EditorGUILayout.LabelField(
                        new string(' ', phase.Depth * 4) + phase.PhaseId,
                        phase.NodeKind + "  |  " + phase.ExecutionState);
                }
            }

            EditorGUILayout.LabelField("Edges", session.Edges.Count.ToString());

            _showTrace = EditorGUILayout.Foldout(_showTrace, "Trace (" + session.Trace.Count + ")", true);
            if (_showTrace)
            {
                for (int i = 0; i < session.Trace.Count; i++)
                {
                    var item = session.Trace[i];
                    EditorGUILayout.LabelField(
                        "+" + item.RelativeSeconds.ToString("0.000") + "  " + item.EventType,
                        item.PhaseId + "  " + item.Message);
                }
            }

            _showContext = EditorGUILayout.Foldout(_showContext, "Context (" + session.Context.Count + ")", true);
            if (_showContext)
            {
                for (int i = 0; i < session.Context.Count; i++)
                {
                    string initial = FindValue(session.InitialContext, session.Context[i].Name);
                    string current = session.Context[i].Value;
                    DrawValue(
                        session.Context[i].Name,
                        initial == current ? current : initial + "  ->  " + current);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private static void DrawValue(string key, string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(key, GUILayout.Width(112f));
            EditorGUILayout.SelectableLabel(value ?? string.Empty, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            EditorGUILayout.EndHorizontal();
        }

        private static string FindValue(IReadOnlyList<PipelineDebugValueSnapshot> values, string name)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i].Name == name) return values[i].Value;
            }
            return "<not captured>";
        }
    }
}

#endif
