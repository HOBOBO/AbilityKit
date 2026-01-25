using System;
using System.Collections.Generic;
using AbilityKit.Ability.Server;
using AbilityKit.Game.Test.FrameSync;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Editor.FrameSync
{
    public sealed class FrameSyncTestWindow : EditorWindow
    {
        private FrameSyncTestHarness _harness;

        private Vector2 _scrollFrames;
        private Vector2 _scrollInputs;
        private Vector2 _scrollLogs;

        private int _stepCount = 1;

        private int _submitOpCode = 1;
        private string _submitPayload = "hello";

        private string _logFilter = "";
        private string _frameFilter = "";
        private string _inputFilter = "";

        private int _selectedFrameIndex = -1;

        [MenuItem("Tools/AbilityKit/FrameSync Test")]
        public static void Open()
        {
            GetWindow<FrameSyncTestWindow>("FrameSync Test");
        }

        private void OnFocus()
        {
            ResolveHarness();
        }

        private void OnHierarchyChange()
        {
            ResolveHarness();
            Repaint();
        }

        private void ResolveHarness()
        {
            if (_harness != null) return;
            _harness = FindFirstObjectByType<FrameSyncTestHarness>();
        }

        private void OnGUI()
        {
            ResolveHarness();

            DrawToolbar();
            GUILayout.Space(6);

            if (_harness == null)
            {
                EditorGUILayout.HelpBox("No FrameSyncTestHarness found in scene.", MessageType.Info);
                if (GUILayout.Button("Create Harness GameObject", GUILayout.Height(28)))
                {
                    var go = new GameObject("FrameSyncTestHarness");
                    _harness = go.AddComponent<FrameSyncTestHarness>();
                    Selection.activeObject = go;
                }
                return;
            }

            DrawSessionControls();
            GUILayout.Space(6);

            DrawTickControls();
            GUILayout.Space(6);

            DrawSubmitInput();
            GUILayout.Space(6);

            DrawDataViews();
        }

        private void DrawToolbar()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label(_harness == null ? "Harness: <none>" : $"Harness: {_harness.name}");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton))
            {
                _harness = null;
                ResolveHarness();
            }
            GUILayout.EndHorizontal();
        }

        private void DrawSessionControls()
        {
            GUILayout.Label("Session", EditorStyles.boldLabel);

            GUILayout.BeginHorizontal();
            GUI.enabled = _harness != null && !_harness.HasSession;
            if (GUILayout.Button("Start", GUILayout.Height(24))) _harness.StartSession();
            GUI.enabled = _harness != null && _harness.HasSession;
            if (GUILayout.Button("Stop", GUILayout.Height(24))) _harness.StopSession();
            GUI.enabled = _harness != null && _harness.HasSession;
            if (GUILayout.Button("Connect", GUILayout.Height(24))) _harness.Connect();
            if (GUILayout.Button("Disconnect", GUILayout.Height(24))) _harness.Disconnect();
            if (GUILayout.Button("Join", GUILayout.Height(24))) _harness.Join();
            if (GUILayout.Button("Leave", GUILayout.Height(24))) _harness.Leave();
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUI.enabled = _harness != null && _harness.HasSession;
            if (GUILayout.Button("CreateWorld (InitOp=0)", GUILayout.Height(24)))
            {
                _harness.CreateWorld(initOpCode: 0, initPayload: null);
            }
            if (GUILayout.Button("Clear Buffers", GUILayout.Height(24)))
            {
                _harness.ClearBuffers();
                _selectedFrameIndex = -1;
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Label($"State: session={_harness.HasSession}, paused={_harness.Paused}, lastFrame={_harness.LastFrame}");
        }

        private void DrawTickControls()
        {
            GUILayout.Label("Tick", EditorStyles.boldLabel);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_harness.Paused ? "Resume" : "Pause", GUILayout.Height(24))) _harness.TogglePause();

            GUI.enabled = _harness.HasSession;
            if (GUILayout.Button("Tick Once", GUILayout.Height(24))) _harness.TickOnce();
            _stepCount = Mathf.Max(1, EditorGUILayout.IntField(_stepCount, GUILayout.Width(60)));
            if (GUILayout.Button("Tick N", GUILayout.Height(24))) _harness.TickFrames(_stepCount);
            GUI.enabled = true;

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            _harness.AutoTick = EditorGUILayout.ToggleLeft("AutoTick", _harness.AutoTick, GUILayout.Width(90));
            var fd = EditorGUILayout.FloatField("FixedDelta", _harness.FixedDelta);
            _harness.FixedDelta = fd;
            GUILayout.EndHorizontal();
        }

        private void DrawSubmitInput()
        {
            GUILayout.Label("Submit Input", EditorStyles.boldLabel);

            GUILayout.BeginHorizontal();
            _submitOpCode = EditorGUILayout.IntField("Op", _submitOpCode);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            _submitPayload = EditorGUILayout.TextField("Payload", _submitPayload);
            GUILayout.EndHorizontal();

            GUI.enabled = _harness.HasSession;
            if (GUILayout.Button("Submit", GUILayout.Height(24)))
            {
                _harness.SubmitInputString(_submitOpCode, _submitPayload);
            }
            GUI.enabled = true;
        }

        private void DrawDataViews()
        {
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.Width(position.width * 0.45f));
            DrawFramesView();
            GUILayout.EndVertical();

            GUILayout.BeginVertical(GUILayout.Width(position.width * 0.55f));
            DrawInputsAndLogsView();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }

        private void DrawFramesView()
        {
            GUILayout.Label("Frames", EditorStyles.boldLabel);

            _frameFilter = EditorGUILayout.TextField("Filter", _frameFilter);

            var frames = _harness.Frames;
            _scrollFrames = EditorGUILayout.BeginScrollView(_scrollFrames, GUILayout.Height(260));
            for (int i = 0; i < frames.Count; i++)
            {
                var p = frames[i];
                var line = $"#{p.Frame.Value} inputs={(p.Inputs?.Count ?? 0)} snapshot={(p.Snapshot.HasValue ? p.Snapshot.Value.OpCode.ToString() : "null")}";
                if (!PassFilter(line, _frameFilter)) continue;

                var selected = i == _selectedFrameIndex;
                if (GUILayout.Toggle(selected, line, "Button"))
                {
                    _selectedFrameIndex = i;
                }
            }
            EditorGUILayout.EndScrollView();

            if (_selectedFrameIndex >= 0 && _selectedFrameIndex < frames.Count)
            {
                var p = frames[_selectedFrameIndex];
                GUILayout.Space(4);
                GUILayout.Label($"Selected: frame={p.Frame.Value}");
                if (p.Snapshot.HasValue)
                {
                    GUILayout.Label($"Snapshot: op={p.Snapshot.Value.OpCode}, bytes={(p.Snapshot.Value.Payload?.Length ?? 0)}");
                }
                else
                {
                    GUILayout.Label("Snapshot: null");
                }
            }
        }

        private void DrawInputsAndLogsView()
        {
            GUILayout.Label("Inputs", EditorStyles.boldLabel);

            _inputFilter = EditorGUILayout.TextField("Filter", _inputFilter);

            var pending = _harness.GetPendingSubmittedInputs();
            _scrollInputs = EditorGUILayout.BeginScrollView(_scrollInputs, GUILayout.Height(140));
            for (int i = 0; i < pending.Count; i++)
            {
                var cmd = pending[i];
                var line = $"PENDING frame={cmd.Frame.Value} op={cmd.OpCode} bytes={(cmd.Payload?.Length ?? 0)}";
                if (!PassFilter(line, _inputFilter)) continue;
                GUILayout.Label(line);
            }
            EditorGUILayout.EndScrollView();

            GUILayout.Space(6);
            GUILayout.Label("Logs", EditorStyles.boldLabel);

            _logFilter = EditorGUILayout.TextField("Filter", _logFilter);

            var logs = _harness.Logs;
            _scrollLogs = EditorGUILayout.BeginScrollView(_scrollLogs, GUILayout.Height(220));
            for (int i = Mathf.Max(0, logs.Count - 400); i < logs.Count; i++)
            {
                var line = logs[i];
                if (!PassFilter(line, _logFilter)) continue;
                GUILayout.Label(line);
            }
            EditorGUILayout.EndScrollView();
        }

        private static bool PassFilter(string line, string filter)
        {
            if (string.IsNullOrEmpty(filter)) return true;
            if (line == null) return false;
            return line.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
