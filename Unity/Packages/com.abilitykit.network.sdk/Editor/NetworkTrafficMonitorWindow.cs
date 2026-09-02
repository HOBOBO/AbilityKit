#if UNITY_EDITOR

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Observability;
using AbilityKit.Network.Sdk.Diagnostics;
using AbilityKit.Network.Sdk.Observability;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Network.Sdk.Editor
{
    public sealed class NetworkTrafficMonitorWindow : EditorWindow
    {
        private enum DirectionFilter { All, Inbound, Outbound }
        private enum DataView { Connections, Routes, Traffic }

        private readonly List<NetworkTrafficInspectionRow> _visible =
            new List<NetworkTrafficInspectionRow>();
        private readonly NetworkTrafficJsonExporter _exporter = new NetworkTrafficJsonExporter();

        private IReadOnlyList<NetworkTrafficInspectionRow> _snapshot =
            Array.Empty<NetworkTrafficInspectionRow>();
        private IReadOnlyList<NetworkClientDiagnosticsSnapshot> _diagnostics =
            Array.Empty<NetworkClientDiagnosticsSnapshot>();
        private DataView _dataView = DataView.Connections;
        private Vector2 _diagnosticsScroll;
        private Vector2 _listScroll;
        private Vector2 _detailScroll;
        private string _search = string.Empty;
        private string _roleFilter = string.Empty;
        private DirectionFilter _directionFilter;
        private bool _paused;
        private bool _followLatest = true;
        private bool _includeRawPayload;
        private bool _allowSensitiveRawPayload;
        private int _selectedIndex = -1;
        private double _nextRefreshAt;
        private GUIStyle? _monoStyle;
        private GUIStyle? _rowTitleStyle;
        private GUIStyle? _mutedStyle;

        [MenuItem("Window/AbilityKit/Network Diagnostics")]
        [MenuItem("Window/AbilityKit/Network Traffic Monitor")]
        public static void Open()
        {
            var window = GetWindow<NetworkTrafficMonitorWindow>();
            window.titleContent = new GUIContent("Network Diagnostics");
            window.minSize = new Vector2(760f, 420f);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
            RefreshSnapshot(force: true);
        }

        private void OnDisable() => EditorApplication.update -= OnEditorUpdate;

        private void OnEditorUpdate()
        {
            if (_paused || EditorApplication.timeSinceStartup < _nextRefreshAt) return;
            RefreshSnapshot(force: false);
            Repaint();
        }

        private void RefreshSnapshot(bool force)
        {
            _nextRefreshAt = EditorApplication.timeSinceStartup + 0.2d;
            _diagnostics = NetworkSdkDiagnosticsMonitor.Default.Snapshot();
            var monitor = NetworkTrafficMonitor.Default;
            if (!force && monitor.Count == _snapshot.Count && monitor.DroppedCount == 0) return;
            _snapshot = monitor.Inspect();
            RebuildVisible();
            if (_followLatest && _visible.Count > 0) _selectedIndex = _visible.Count - 1;
        }

        private void OnGUI()
        {
            EnsureStyles();
            _dataView = (DataView)GUILayout.Toolbar(
                (int)_dataView,
                new[] { "Connections", "Routes", "Traffic" },
                EditorStyles.toolbarButton);
            if (_dataView == DataView.Connections)
            {
                DrawConnections();
                return;
            }

            if (_dataView == DataView.Routes)
            {
                DrawRoutes();
                return;
            }

            DrawToolbar();
            DrawFilterBar();
            EditorGUILayout.BeginHorizontal();
            DrawTrafficList();
            DrawDetails();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawConnections()
        {
            _diagnosticsScroll = EditorGUILayout.BeginScrollView(_diagnosticsScroll);
            if (_diagnostics.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No SDK clients are registered in NetworkSdkDiagnosticsMonitor.Default.Hub.",
                    MessageType.Info);
            }

            for (var i = 0; i < _diagnostics.Count; i++)
            {
                var client = _diagnostics[i];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(client.Key.ToString(), EditorStyles.boldLabel);
                DrawField("Project", client.Key.ProjectId);
                DrawField("Role / Instance", $"{client.Key.Role} / {client.Key.InstanceId}");
                DrawField("Leases", client.LeaseCount.ToString(CultureInfo.InvariantCulture));
                if (!client.Connection.HasValue)
                {
                    EditorGUILayout.LabelField("Connection diagnostics are not supported.", _mutedStyle);
                    EditorGUILayout.EndVertical();
                    continue;
                }

                var connection = client.Connection.Value;
                DrawField("State", connection.State.ToString());
                DrawField("Endpoint", $"{connection.Host}:{connection.Port}");
                DrawField("Identity", $"{connection.ConnectionId} / generation {connection.Generation}");
                DrawField("Connected", connection.IsConnected.ToString());
                DrawField("Reconnect", FormatReconnect(connection));
                DrawField("Middleware", connection.PipelineMiddlewareCount.ToString(CultureInfo.InvariantCulture));
                if (connection.PacketRouter.HasValue)
                {
                    var router = connection.PacketRouter.Value;
                    DrawField("Router", $"dispatch {router.DispatchedCount}, handled {router.HandledCount}, " +
                        $"unknown {router.UnknownCount}, exceptions {router.ExceptionCount}, " +
                        $"boundary rejected {router.BoundaryRejectedCount}");
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawRoutes()
        {
            _diagnosticsScroll = EditorGUILayout.BeginScrollView(_diagnosticsScroll);
            if (_diagnostics.Count == 0)
            {
                EditorGUILayout.HelpBox("No SDK client routes are available.", MessageType.Info);
            }

            for (var clientIndex = 0; clientIndex < _diagnostics.Count; clientIndex++)
            {
                var client = _diagnostics[clientIndex];
                EditorGUILayout.LabelField(
                    $"{client.Key}  ({client.Routes.Count} routes)",
                    EditorStyles.boldLabel);
                for (var routeIndex = 0; routeIndex < client.Routes.Count; routeIndex++)
                {
                    DrawRoute(client.Routes[routeIndex]);
                }
                EditorGUILayout.Space(6f);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawRoute(NetworkRouteDiagnosticsSnapshot snapshot)
        {
            var route = snapshot.Route;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                $"Opcode {route.OpCode}  {route.Kind}",
                EditorStyles.boldLabel,
                GUILayout.MinWidth(180f));
            GUILayout.FlexibleSpace();
            GUILayout.Label(snapshot.MappingStatus.ToString(), EditorStyles.miniBoldLabel);
            EditorGUILayout.EndHorizontal();
            DrawField("Protocol", $"{snapshot.Direction?.ToString() ?? "-"} / {snapshot.PacketKind?.ToString() ?? "-"}");
            DrawField("Handlers", route.HandlerCount.ToString(CultureInfo.InvariantCulture));
            DrawField("Counters", $"dispatch {route.DispatchCount}, handled {route.HandledCount}, " +
                $"unknown {route.UnknownCount}, exceptions {route.ExceptionCount}");
            DrawField("Last Dispatch", route.LastDispatchUnixTimeMilliseconds == 0
                ? "Never"
                : DateTimeOffset.FromUnixTimeMilliseconds(route.LastDispatchUnixTimeMilliseconds)
                    .ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
            for (var i = 0; i < snapshot.Candidates.Count; i++)
            {
                var candidate = snapshot.Candidates[i];
                DrawField(
                    i == 0 ? "Catalog" : string.Empty,
                    $"{candidate.CatalogProjectId}/{candidate.CatalogId} :: {candidate.MessageId}  " +
                    $"{candidate.PayloadType} [{candidate.Codec}] sample={candidate.CaptureSampleRate:0.###}");
            }
            EditorGUILayout.EndVertical();
        }

        private static string FormatReconnect(NetworkConnectionDiagnosticsSnapshot connection)
        {
            if (connection.ReconnectExhausted)
                return $"Exhausted ({connection.ReconnectAttemptsStarted}/{connection.ReconnectMaxAttempts})";
            if (connection.ReconnectPending)
                return $"Pending attempt {connection.NextReconnectAttempt}, " +
                    $"remaining {connection.RemainingReconnectDelaySeconds:0.###}s";
            return $"Idle ({connection.ReconnectAttemptsStarted}/{connection.ReconnectMaxAttempts})";
        }

        private void DrawToolbar()
        {
            var monitor = NetworkTrafficMonitor.Default;
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(23f));

            var pause = GUILayout.Toggle(
                _paused,
                EditorGUIUtility.IconContent(_paused ? "PlayButton" : "PauseButton", _paused ? "Resume refresh" : "Pause refresh"),
                EditorStyles.toolbarButton,
                GUILayout.Width(30f));
            if (pause != _paused)
            {
                _paused = pause;
                if (!_paused) RefreshSnapshot(force: true);
            }

            _followLatest = GUILayout.Toggle(
                _followLatest,
                new GUIContent("Follow", "Select the newest matching packet"),
                EditorStyles.toolbarButton,
                GUILayout.Width(55f));

            GUILayout.Label(
                $"Events {monitor.Count}/{monitor.Capacity}   Dropped {monitor.DroppedCount}   " +
                $"Sampled {monitor.SamplingMetrics.GetSnapshot().SampledOut}",
                EditorStyles.miniLabel,
                GUILayout.MinWidth(240f));
            GUILayout.FlexibleSpace();

            _includeRawPayload = GUILayout.Toggle(
                _includeRawPayload,
                new GUIContent("Raw", "Include captured raw payload preview in JSON export"),
                EditorStyles.toolbarButton,
                GUILayout.Width(42f));
            using (new EditorGUI.DisabledScope(!_includeRawPayload))
            {
                _allowSensitiveRawPayload = GUILayout.Toggle(
                    _allowSensitiveRawPayload,
                    new GUIContent("Sensitive", "Allow raw export for catalog messages marked sensitive"),
                    EditorStyles.toolbarButton,
                    GUILayout.Width(65f));
            }

            if (GUILayout.Button(
                    EditorGUIUtility.IconContent("SaveAs", "Export filtered traffic as JSON"),
                    EditorStyles.toolbarButton,
                    GUILayout.Width(30f)))
                ExportVisible();

            if (GUILayout.Button(
                    EditorGUIUtility.IconContent("TreeEditor.Trash", "Clear captured traffic"),
                    EditorStyles.toolbarButton,
                    GUILayout.Width(30f)))
            {
                monitor.Clear();
                _snapshot = Array.Empty<NetworkTrafficInspectionRow>();
                _visible.Clear();
                _selectedIndex = -1;
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawFilterBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            var search = GUILayout.TextField(_search, EditorStyles.toolbarSearchField, GUILayout.MinWidth(180f));
            if (search != _search)
            {
                _search = search;
                RebuildVisible();
            }
            var role = GUILayout.TextField(_roleFilter, EditorStyles.toolbarSearchField, GUILayout.Width(120f));
            if (role != _roleFilter)
            {
                _roleFilter = role;
                RebuildVisible();
            }
            GUILayout.Label("Role", EditorStyles.miniLabel, GUILayout.Width(28f));
            var direction = (DirectionFilter)EditorGUILayout.EnumPopup(
                _directionFilter,
                EditorStyles.toolbarPopup,
                GUILayout.Width(82f));
            if (direction != _directionFilter)
            {
                _directionFilter = direction;
                RebuildVisible();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawTrafficList()
        {
            var width = Mathf.Clamp(position.width * 0.48f, 330f, 620f);
            EditorGUILayout.BeginVertical(GUILayout.Width(width), GUILayout.ExpandHeight(true));
            DrawColumnHeader();
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.ExpandHeight(true));
            if (_visible.Count == 0)
            {
                GUILayout.Space(24f);
                GUILayout.Label("No matching traffic", _mutedStyle);
            }
            for (var i = 0; i < _visible.Count; i++) DrawRow(i, _visible[i]);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private static void DrawColumnHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Time", EditorStyles.miniLabel, GUILayout.Width(82f));
            GUILayout.Label("Dir", EditorStyles.miniLabel, GUILayout.Width(34f));
            GUILayout.Label("Op", EditorStyles.miniLabel, GUILayout.Width(48f));
            GUILayout.Label("Message / Connection", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawRow(int index, NetworkTrafficInspectionRow row)
        {
            var rect = GUILayoutUtility.GetRect(0f, 42f, GUILayout.ExpandWidth(true));
            var selected = index == _selectedIndex;
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, selected
                    ? new Color(0.18f, 0.36f, 0.55f, 0.58f)
                    : new Color(1f, 1f, 1f, index % 2 == 0 ? 0.025f : 0.01f));
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3f, rect.height), ResolutionColor(row));
            }

            var traffic = row.Traffic;
            GUI.Label(new Rect(rect.x + 7f, rect.y + 4f, 78f, 18f),
                traffic.TimestampUtc.ToLocalTime().ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture), _mutedStyle);
            GUI.Label(new Rect(rect.x + 88f, rect.y + 4f, 30f, 18f),
                traffic.Direction == NetworkTrafficDirection.Inbound ? "IN" : "OUT", _rowTitleStyle);
            GUI.Label(new Rect(rect.x + 121f, rect.y + 4f, 46f, 18f), traffic.OpCode.ToString(), _monoStyle);
            GUI.Label(new Rect(rect.x + 170f, rect.y + 4f, rect.width - 176f, 18f),
                row.Message?.Id ?? (row.IsAmbiguous ? "Ambiguous" : "Unknown"), _rowTitleStyle);
            GUI.Label(new Rect(rect.x + 88f, rect.y + 23f, rect.width - 94f, 16f),
                $"{traffic.Role}  {traffic.ConnectionId}  seq {traffic.Sequence}  {traffic.PayloadLength} B", _mutedStyle);

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && rect.Contains(Event.current.mousePosition))
            {
                _selectedIndex = index;
                _followLatest = false;
                Event.current.Use();
            }
        }

        private void DrawDetails()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (_selectedIndex < 0 || _selectedIndex >= _visible.Count)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("Select a packet", _mutedStyle);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndVertical();
                return;
            }

            var row = _visible[_selectedIndex];
            var traffic = row.Traffic;
            _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);
            EditorGUILayout.LabelField(row.Message?.Id ?? "Unresolved packet", EditorStyles.boldLabel);
            DrawField("Catalog", traffic.CatalogId);
            DrawField("Connection", $"{traffic.ConnectionId} / generation {traffic.Generation}");
            DrawField("Role", traffic.Role);
            DrawField("Endpoint", traffic.Endpoint);
            DrawField("Transport", traffic.Transport);
            DrawField("Direction", traffic.Direction.ToString());
            DrawField("Opcode", traffic.OpCode.ToString(CultureInfo.InvariantCulture));
            DrawField("Sequence", traffic.Sequence.ToString(CultureInfo.InvariantCulture));
            DrawField("Flags", traffic.Flags.ToString());
            DrawField("Payload", $"{traffic.PayloadLength} bytes" + (traffic.IsPayloadPreviewTruncated ? " (preview truncated)" : string.Empty));
            if (row.Message != null)
            {
                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("Protocol", EditorStyles.boldLabel);
                DrawField("Payload Type", row.Message.PayloadType);
                DrawField("Codec", row.Message.Codec);
                DrawField("Reliability", row.Message.Reliability.ToString());
                DrawField("Schema", $"{row.Message.MinimumSchemaVersion}..{row.Message.MaximumSchemaVersion}");
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Decoded Payload", EditorStyles.boldLabel);
            var decoded = row.Decode.Success
                ? _exporter.FormatDecodedPayload(row)
                : row.Decode.Error;
            EditorGUILayout.SelectableLabel(decoded, _monoStyle, GUILayout.MinHeight(100f));
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private static void DrawField(string name, string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(name, EditorStyles.miniLabel, GUILayout.Width(92f));
            EditorGUILayout.SelectableLabel(value ?? string.Empty, EditorStyles.label, GUILayout.Height(18f));
            EditorGUILayout.EndHorizontal();
        }

        private void RebuildVisible()
        {
            NetworkTrafficInspectionRow? selected =
                _selectedIndex >= 0 && _selectedIndex < _visible.Count ? _visible[_selectedIndex] : null;
            _visible.Clear();
            for (var i = 0; i < _snapshot.Count; i++)
            {
                var row = _snapshot[i];
                if (Matches(row)) _visible.Add(row);
            }

            _selectedIndex = selected == null ? -1 : _visible.IndexOf(selected);
            if (_followLatest && _visible.Count > 0) _selectedIndex = _visible.Count - 1;
        }

        private bool Matches(NetworkTrafficInspectionRow row)
        {
            var traffic = row.Traffic;
            if (_directionFilter == DirectionFilter.Inbound && traffic.Direction != NetworkTrafficDirection.Inbound) return false;
            if (_directionFilter == DirectionFilter.Outbound && traffic.Direction != NetworkTrafficDirection.Outbound) return false;
            if (!string.IsNullOrWhiteSpace(_roleFilter) && !Contains(traffic.Role, _roleFilter)) return false;
            if (string.IsNullOrWhiteSpace(_search)) return true;
            return Contains(row.Message?.Id, _search) || Contains(row.Message?.PayloadType, _search) ||
                   Contains(traffic.CatalogId, _search) || Contains(traffic.ConnectionId, _search) ||
                   Contains(traffic.Endpoint, _search) || Contains(traffic.OpCode.ToString(), _search);
        }

        private void ExportVisible()
        {
            if (_visible.Count == 0) return;
            if (_includeRawPayload && _allowSensitiveRawPayload &&
                !EditorUtility.DisplayDialog(
                    "Export Sensitive Raw Payload",
                    "Raw payload bytes may contain credentials or personal data and cannot be redacted before decoding.",
                    "Export", "Cancel"))
                return;

            var path = EditorUtility.SaveFilePanel(
                "Export Network Traffic", string.Empty,
                $"network-traffic-{DateTime.Now:yyyyMMdd-HHmmss}.json", "json");
            if (string.IsNullOrWhiteSpace(path)) return;

            try
            {
                var json = _exporter.Export(
                    _visible,
                    new NetworkTrafficExportOptions
                    {
                        IncludeDecodedPayload = true,
                        IncludeRawPayloadPreview = _includeRawPayload,
                        AllowSensitiveRawPayloadPreview = _allowSensitiveRawPayload,
                        PrettyPrint = true
                    });
                File.WriteAllText(path, json, new UTF8Encoding(false));
                EditorUtility.RevealInFinder(path);
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("Network Traffic Export Failed", exception.Message, "OK");
            }
        }

        private void EnsureStyles()
        {
            _monoStyle ??= new GUIStyle(EditorStyles.label) { font = EditorStyles.textArea.font, wordWrap = true };
            _rowTitleStyle ??= new GUIStyle(EditorStyles.miniBoldLabel) { clipping = TextClipping.Clip };
            _mutedStyle ??= new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = EditorGUIUtility.isProSkin
                    ? new Color(0.68f, 0.7f, 0.73f)
                    : new Color(0.32f, 0.34f, 0.37f) },
                clipping = TextClipping.Clip
            };
        }

        private static Color ResolutionColor(NetworkTrafficInspectionRow row)
        {
            if (row.Decode.Success) return new Color(0.25f, 0.72f, 0.42f);
            if (row.IsKnown) return new Color(0.88f, 0.62f, 0.2f);
            return new Color(0.8f, 0.28f, 0.28f);
        }

        private static bool Contains(string? value, string search) =>
            value?.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}

#endif
