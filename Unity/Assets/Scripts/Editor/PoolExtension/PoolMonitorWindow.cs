#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Ability.Share.Common.Pool;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.PoolExtension.Editor
{
    public sealed class PoolMonitorWindow : EditorWindow
    {
        private const string MenuPath = "Tools/AbilityKit/Debug/Pool Monitor";

        private readonly List<PoolDebugSnapshot> _rows = new List<PoolDebugSnapshot>(128);

        private Vector2 _scroll;
        private string _search;
        private bool _autoRefresh = true;
        private double _refreshInterval = 0.5;
        private double _nextRefreshTime;

        [MenuItem(MenuPath)]
        private static void Open()
        {
            GetWindow<PoolMonitorWindow>("Pool Monitor");
        }

        private void OnEnable()
        {
            _nextRefreshTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            if (!_autoRefresh) return;
            if (EditorApplication.timeSinceStartup < _nextRefreshTime) return;

            Refresh();
            _nextRefreshTime = EditorApplication.timeSinceStartup + Math.Max(0.1, _refreshInterval);
        }

        private void Refresh()
        {
            _rows.Clear();
            var snapshots = Pools.GetDebugSnapshots();
            if (snapshots == null) return;
            _rows.AddRange(snapshots);
            Repaint();
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _search = EditorGUILayout.TextField("Search", _search ?? string.Empty);

                if (GUILayout.Button("Refresh", GUILayout.Width(80)))
                {
                    Refresh();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _autoRefresh = EditorGUILayout.ToggleLeft("Auto Refresh", _autoRefresh, GUILayout.Width(110));
                _refreshInterval = EditorGUILayout.DoubleField("Interval(s)", _refreshInterval, GUILayout.Width(220));
            }

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Play Mode only.", MessageType.Info);
                return;
            }

            var filtered = Filter(_rows, _search);

            DrawHeader();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < filtered.Count; i++)
            {
                DrawRow(filtered[i]);
            }
            EditorGUILayout.EndScrollView();
        }

        private static List<PoolDebugSnapshot> Filter(List<PoolDebugSnapshot> rows, string search)
        {
            if (rows == null || rows.Count == 0) return rows;
            if (string.IsNullOrWhiteSpace(search)) return rows;

            search = search.Trim();
            return rows.Where(r =>
                (r.ElementType != null && r.ElementType.FullName != null && r.ElementType.FullName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                r.Key.Value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
            ).ToList();
        }

        private static void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Type", GUILayout.MinWidth(240));
                GUILayout.Label("Key", GUILayout.Width(120));
                GUILayout.Label("Active", GUILayout.Width(50));
                GUILayout.Label("Inactive", GUILayout.Width(60));
                GUILayout.Label("Created", GUILayout.Width(60));
                GUILayout.Label("Get", GUILayout.Width(50));
                GUILayout.Label("Release", GUILayout.Width(60));
                GUILayout.Label("Max", GUILayout.Width(50));
            }
        }

        private static void DrawRow(in PoolDebugSnapshot s)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(s.ElementType != null ? s.ElementType.FullName : "<null>", GUILayout.MinWidth(240));
                GUILayout.Label(s.Key.Value ?? string.Empty, GUILayout.Width(120));
                GUILayout.Label(s.Stats.ActiveCount.ToString(), GUILayout.Width(50));
                GUILayout.Label(s.Stats.InactiveCount.ToString(), GUILayout.Width(60));
                GUILayout.Label(s.Stats.CreatedTotal.ToString(), GUILayout.Width(60));
                GUILayout.Label(s.Stats.GetTotal.ToString(), GUILayout.Width(50));
                GUILayout.Label(s.Stats.ReleaseTotal.ToString(), GUILayout.Width(60));
                GUILayout.Label(s.MaxSize.ToString(), GUILayout.Width(50));
            }
        }
    }
}
#endif
