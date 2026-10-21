using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Minions;
using AbilityKit.Demo.Moba.Services.Map;
using AbilityKit.Demo.Moba.Share.Config;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.Editor
{
    internal sealed class MobaMinionRouteEditorWindow : EditorWindow
    {
        private const string MapJsonPath = "Packages/com.abilitykit.demo.moba.view.runtime/Resources/moba/battle_maps.json";
        private const string RouteJsonPath = "Packages/com.abilitykit.demo.moba.runtime/Resources/moba/minion_routes.json";
        private const string ConsoleRouteJsonPath = "../src/AbilityKit.Demo.Moba.Console/Configs/moba/minion_routes.json";
        private const string SourceRouteRelativePath = "LubanConfig/Moba/Production/Gameplay/minion_routes.json";
        private static readonly string[] TerminalPolicies = { "BaseDamageEvent", "Despawn", "Hold" };

        private RouteResource _resource;
        private BattleMapDTO[] _maps = Array.Empty<BattleMapDTO>();
        private int _mapIndex;
        private int _routeIndex;
        private Vector2 _scroll;

        [MenuItem("Tools/AbilityKit/Demos/MOBA/Edit Minion Routes", priority = 18)]
        private static void Open()
        {
            var window = GetWindow<MobaMinionRouteEditorWindow>();
            window.titleContent = new GUIContent("MOBA Minion Routes");
            window.minSize = new Vector2(760f, 480f);
            window.Reload();
            window.Show();
        }

        private void OnGUI()
        {
            if (_resource == null || _maps.Length == 0)
            {
                EditorGUILayout.HelpBox("Route or map resources could not be loaded.", MessageType.Error);
                if (GUILayout.Button("Reload")) Reload();
                return;
            }

            var mapLabels = new string[_maps.Length];
            for (var i = 0; i < _maps.Length; i++)
                mapLabels[i] = $"{_maps[i].Id}: {_maps[i].Name}";
            _mapIndex = EditorGUILayout.Popup("Validation Map", _mapIndex, mapLabels);
            _resource.MapId = _maps[_mapIndex].Id;

            _resource.Version = EditorGUILayout.IntField("Resource Version", _resource.Version);
            _resource.DefaultRouteId = EditorGUILayout.IntField("Default Route Id", _resource.DefaultRouteId);
            if (_resource.Schedule == null) _resource.Schedule = new WaveSchedule();
            _resource.Schedule.FirstWaveAtMs = EditorGUILayout.IntField("First Wave (ms)", _resource.Schedule.FirstWaveAtMs);
            _resource.Schedule.IntervalMs = EditorGUILayout.IntField("Wave Interval (ms)", _resource.Schedule.IntervalMs);
            _resource.Schedule.MaxWaves = EditorGUILayout.IntField("Max Waves (0 = unlimited)", _resource.Schedule.MaxWaves);
            _resource.Schedule.MaxActiveMinionsPerTeam = EditorGUILayout.IntField(
                "Max Active Minions Per Team", _resource.Schedule.MaxActiveMinionsPerTeam);
            if (_resource.Schedule.Composition == null) _resource.Schedule.Composition = Array.Empty<WaveComposition>();
            for (var i = 0; i < _resource.Schedule.Composition.Length; i++)
            {
                var entry = _resource.Schedule.Composition[i];
                EditorGUILayout.BeginHorizontal();
                entry.Archetype = EditorGUILayout.TextField(entry.Archetype, GUILayout.Width(90f));
                entry.Count = EditorGUILayout.IntField("Count", entry.Count, GUILayout.Width(100f));
                entry.SpawnIntervalMs = EditorGUILayout.IntField("Spawn ms", entry.SpawnIntervalMs, GUILayout.Width(140f));
                if (GUILayout.Button("-", GUILayout.Width(24f)))
                {
                    RemoveCompositionEntry(i);
                    EditorGUILayout.EndHorizontal();
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }
            if (GUILayout.Button("Add Composition Entry")) AddCompositionEntry();
            if (_resource.Routes == null || _resource.Routes.Length == 0)
            {
                EditorGUILayout.HelpBox("At least one route is required.", MessageType.Warning);
                if (GUILayout.Button("Add Route")) AddRoute();
                DrawExportButtons();
                return;
            }

            _routeIndex = Mathf.Clamp(_routeIndex, 0, _resource.Routes.Length - 1);
            var routeLabels = new string[_resource.Routes.Length];
            for (var i = 0; i < _resource.Routes.Length; i++)
                routeLabels[i] = $"Route {_resource.Routes[i].RouteId}";
            _routeIndex = EditorGUILayout.Popup("Route", _routeIndex, routeLabels);

            var route = _resource.Routes[_routeIndex];
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            route.RouteId = EditorGUILayout.IntField("Route Id", route.RouteId);
            route.AcceptanceRadius = EditorGUILayout.FloatField("Acceptance Radius", route.AcceptanceRadius);
            var policyIndex = Array.IndexOf(TerminalPolicies, route.TerminalPolicy);
            if (policyIndex < 0) policyIndex = 0;
            route.TerminalPolicy = TerminalPolicies[EditorGUILayout.Popup("Terminal Policy", policyIndex, TerminalPolicies)];

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Waypoints", EditorStyles.boldLabel);
            if (route.Waypoints == null) route.Waypoints = Array.Empty<RoutePoint>();
            for (var i = 0; i < route.Waypoints.Length; i++)
            {
                var point = route.Waypoints[i];
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(i.ToString(), GUILayout.Width(24f));
                point.X = EditorGUILayout.FloatField("X", point.X);
                point.Y = EditorGUILayout.FloatField("Y", point.Y);
                point.Z = EditorGUILayout.FloatField("Z", point.Z);
                if (GUILayout.Button("-", GUILayout.Width(24f)))
                {
                    RemoveWaypoint(route, i);
                    EditorGUILayout.EndHorizontal();
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("Add Waypoint")) AddWaypoint(route);
            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Route")) AddRoute();
            if (_resource.Routes.Length > 1 && GUILayout.Button("Remove Route")) RemoveRoute();
            EditorGUILayout.EndHorizontal();
            DrawExportButtons();
        }

        private void DrawExportButtons()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reload Resource")) Reload();
            if (GUILayout.Button("Validate And Export")) ValidateAndExport();
            EditorGUILayout.EndHorizontal();
        }

        private void Reload()
        {
            var mapAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(MapJsonPath);
            var sourcePath = GetSourceRoutePath();
            if (!File.Exists(sourcePath) || mapAsset == null)
            {
                _resource = null;
                _maps = Array.Empty<BattleMapDTO>();
                return;
            }

            var sourceJson = File.ReadAllText(sourcePath);
            var loadedResource = MobaMinionRouteResourceLoader.LoadResource(sourceJson);
            _resource = JsonConvert.DeserializeObject<RouteResource>(sourceJson);
            if (_resource == null) throw new InvalidOperationException("Route resource cannot be null.");
            _resource.Version = loadedResource.Version;
            _resource.MapId = loadedResource.MapId;
            _resource.DefaultRouteId = loadedResource.DefaultRouteId;
            if (_resource.Schedule == null)
            {
                _resource.Schedule = new WaveSchedule
                {
                    FirstWaveAtMs = loadedResource.Schedule.FirstWaveAtMs,
                    IntervalMs = loadedResource.Schedule.IntervalMs,
                    MaxWaves = loadedResource.Schedule.MaxWaves,
                    MaxActiveMinionsPerTeam = loadedResource.Schedule.MaxActiveMinionsPerTeam,
                    Composition = ConvertComposition(loadedResource.Schedule.Composition),
                };
            }
            else if (_resource.Schedule.MaxActiveMinionsPerTeam <= 0)
            {
                _resource.Schedule.MaxActiveMinionsPerTeam = loadedResource.Schedule.MaxActiveMinionsPerTeam;
            }
            if (_resource.Routes == null) _resource.Routes = Array.Empty<RouteDefinition>();
            _maps = JsonConvert.DeserializeObject<BattleMapDTO[]>(mapAsset.text) ?? Array.Empty<BattleMapDTO>();
            var resourceMapIndex = Array.FindIndex(_maps, map => map != null && map.Id == _resource.MapId);
            _mapIndex = resourceMapIndex >= 0 ? resourceMapIndex : Mathf.Clamp(_mapIndex, 0, Math.Max(0, _maps.Length - 1));
            _routeIndex = Mathf.Clamp(_routeIndex, 0, Math.Max(0, (_resource?.Routes?.Length ?? 1) - 1));
        }

        private void ValidateAndExport()
        {
            try
            {
                var map = new BattleMapMO(_maps[_mapIndex]);
                _resource.Version = MobaMinionRouteResourceLoader.CurrentVersion;
                _resource.MapId = map.Id;
                if (_resource.Routes.Length == 0)
                    throw new InvalidOperationException("At least one route is required.");
                if (_resource.DefaultRouteId <= 0) _resource.DefaultRouteId = _resource.Routes[0].RouteId;
                var serialized = JsonConvert.SerializeObject(_resource, Formatting.Indented);
                var resource = MobaMinionRouteResourceLoader.LoadResource(serialized);
                MobaMinionRouteMapValidator.ValidateResource(resource, map);

                var sourcePath = GetSourceRoutePath();
                WriteAtomically(sourcePath, serialized + Environment.NewLine);
                var projectRoot = GetUnityProjectRoot();
                var runtimePath = Path.Combine(projectRoot, RouteJsonPath.Replace('/', Path.DirectorySeparatorChar));
                var consolePath = Path.GetFullPath(Path.Combine(projectRoot, ConsoleRouteJsonPath.Replace('/', Path.DirectorySeparatorChar)));
                WriteAtomically(runtimePath, serialized + Environment.NewLine);
                WriteAtomically(consolePath, serialized + Environment.NewLine);
                AssetDatabase.ImportAsset(RouteJsonPath, ImportAssetOptions.ForceUpdate);
                Debug.Log($"[MobaMinionRouteEditorWindow] Published {_resource.Routes.Length} route(s) and wave schedule for map {map.Id}.");
                EditorUtility.DisplayDialog("MOBA Minion Routes", "Routes and wave schedule validated and published.", "OK");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("MOBA Minion Routes", exception.Message, "OK");
            }
        }

        private static string GetUnityProjectRoot() =>
            Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Unity project root could not be resolved.");

        private static string GetSourceRoutePath()
        {
            var repoRoot = Directory.GetParent(GetUnityProjectRoot())?.FullName
                ?? throw new InvalidOperationException("Repository root could not be resolved.");
            return Path.Combine(repoRoot, SourceRouteRelativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static void WriteAtomically(string path, string contents)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var temporaryPath = path + ".tmp";
            try
            {
                File.WriteAllText(temporaryPath, contents);
                if (File.Exists(path)) File.Replace(temporaryPath, path, null);
                else File.Move(temporaryPath, path);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        private void AddRoute()
        {
            var map = new BattleMapMO(_maps[_mapIndex]);
            MapSpawnPointMO teamOne = null;
            MapSpawnPointMO teamTwo = null;
            for (var i = 0; i < map.SpawnPoints.Count; i++)
            {
                if (teamOne == null && map.SpawnPoints[i].TeamId == 1) teamOne = map.SpawnPoints[i];
                if (teamTwo == null && map.SpawnPoints[i].TeamId == 2) teamTwo = map.SpawnPoints[i];
            }

            if (teamOne == null || teamTwo == null)
            {
                EditorUtility.DisplayDialog("MOBA Minion Routes", "The selected map requires team 1 and team 2 spawn points.", "OK");
                return;
            }

            var route = new RouteDefinition
            {
                RouteId = _resource.Routes.Length == 0 ? 1 : MaxRouteId() + 1,
                AcceptanceRadius = 0.5f,
                TerminalPolicy = "BaseDamageEvent",
                Waypoints = new[] { Point(teamOne.Position), Point(teamTwo.Position) }
            };
            Array.Resize(ref _resource.Routes, _resource.Routes.Length + 1);
            _resource.Routes[_resource.Routes.Length - 1] = route;
            if (_resource.DefaultRouteId <= 0) _resource.DefaultRouteId = route.RouteId;
            _routeIndex = _resource.Routes.Length - 1;
        }

        private static WaveComposition[] ConvertComposition(IReadOnlyList<MobaMinionCompositionEntry> source)
        {
            var result = new WaveComposition[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                var entry = source[i];
                result[i] = new WaveComposition
                {
                    Archetype = entry.Archetype.ToString(),
                    Count = entry.Count,
                    SpawnIntervalMs = entry.SpawnIntervalMs,
                };
            }
            return result;
        }

        private void AddCompositionEntry()
        {
            var entries = _resource.Schedule.Composition;
            Array.Resize(ref entries, entries.Length + 1);
            entries[entries.Length - 1] = new WaveComposition { Archetype = "Melee", Count = 1, SpawnIntervalMs = 250 };
            _resource.Schedule.Composition = entries;
        }

        private void RemoveCompositionEntry(int index)
        {
            var entries = new WaveComposition[_resource.Schedule.Composition.Length - 1];
            for (int source = 0, target = 0; source < _resource.Schedule.Composition.Length; source++)
                if (source != index) entries[target++] = _resource.Schedule.Composition[source];
            _resource.Schedule.Composition = entries;
        }

        private void RemoveRoute()
        {
            var routes = new RouteDefinition[_resource.Routes.Length - 1];
            for (int source = 0, target = 0; source < _resource.Routes.Length; source++)
                if (source != _routeIndex) routes[target++] = _resource.Routes[source];
            _resource.Routes = routes;
            _routeIndex = Mathf.Clamp(_routeIndex, 0, routes.Length - 1);
        }

        private static void AddWaypoint(RouteDefinition route)
        {
            var points = route.Waypoints ?? Array.Empty<RoutePoint>();
            var last = points.Length == 0 ? new RoutePoint() : points[points.Length - 1];
            Array.Resize(ref route.Waypoints, points.Length + 1);
            route.Waypoints[route.Waypoints.Length - 1] = new RoutePoint { X = last.X, Y = last.Y, Z = last.Z + 1f };
        }

        private static void RemoveWaypoint(RouteDefinition route, int index)
        {
            var points = new RoutePoint[route.Waypoints.Length - 1];
            for (int source = 0, target = 0; source < route.Waypoints.Length; source++)
                if (source != index) points[target++] = route.Waypoints[source];
            route.Waypoints = points;
        }

        private int MaxRouteId()
        {
            var max = 0;
            for (var i = 0; i < _resource.Routes.Length; i++) max = Math.Max(max, _resource.Routes[i].RouteId);
            return max;
        }

        private static RoutePoint Point(AbilityKit.Core.Mathematics.Vec3 value) =>
            new RoutePoint { X = value.X, Y = value.Y, Z = value.Z };

        [Serializable]
        private sealed class RouteResource
        {
            public int Version = MobaMinionRouteResourceLoader.CurrentVersion;
            public int MapId = 1;
            public int DefaultRouteId = 1;
            public WaveSchedule Schedule = new WaveSchedule();
            public RouteDefinition[] Routes = Array.Empty<RouteDefinition>();
        }

        [Serializable]
        private sealed class WaveSchedule
        {
            public int FirstWaveAtMs;
            public int IntervalMs = 30_000;
            public int MaxWaves;
            public int MaxActiveMinionsPerTeam = 30;
            public WaveComposition[] Composition =
            {
                new WaveComposition { Archetype = "Melee", Count = 3, SpawnIntervalMs = 250 },
                new WaveComposition { Archetype = "Ranged", Count = 3, SpawnIntervalMs = 250 },
            };
        }

        [Serializable]
        private sealed class WaveComposition
        {
            public string Archetype = "Melee";
            public int Count = 1;
            public int SpawnIntervalMs = 250;
        }

        [Serializable]
        private sealed class RouteDefinition
        {
            public int RouteId;
            public float AcceptanceRadius;
            public string TerminalPolicy = "BaseDamageEvent";
            public RoutePoint[] Waypoints = Array.Empty<RoutePoint>();
        }

        [Serializable]
        private sealed class RoutePoint
        {
            public float X;
            public float Y;
            public float Z;
        }
    }
}
