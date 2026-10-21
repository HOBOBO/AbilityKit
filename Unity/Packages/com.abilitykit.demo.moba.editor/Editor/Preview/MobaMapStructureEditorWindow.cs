using System;
using System.IO;
using Newtonsoft.Json;
using AbilityKit.Demo.Moba.Gameplay.Structures;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.Editor
{
    internal sealed class MobaMapStructureEditorWindow : EditorWindow
    {
        private const string SourceRelativePath = "LubanConfig/Moba/Production/Gameplay/map_structures.json";
        private const string RuntimeRelativePath = "Unity/Packages/com.abilitykit.demo.moba.runtime/Resources/moba/map_structures.json";
        private const string ConsoleRelativePath = "src/AbilityKit.Demo.Moba.Console/Configs/moba/map_structures.json";
        private MobaMapStructureResource _resource;
        private Vector2 _scroll;

        [MenuItem("Tools/AbilityKit/Demos/MOBA/Edit Map Structures", priority = 21)]
        private static void Open()
        {
            var window = GetWindow<MobaMapStructureEditorWindow>();
            window.titleContent = new GUIContent("Map Structures");
            window.Reload();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reload")) Reload();
            if (GUILayout.Button("Add Base")) AddStructure("Base");
            if (GUILayout.Button("Add Tower")) AddStructure("Tower");
            if (GUILayout.Button("Validate And Publish")) Publish();
            EditorGUILayout.EndHorizontal();

            if (_resource == null) return;
            _resource.MapId = EditorGUILayout.IntField("Map Id", _resource.MapId);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (var i = 0; i < _resource.Structures.Count; i++)
            {
                var item = _resource.Structures[i];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Structure {item.Id}", EditorStyles.boldLabel);
                if (GUILayout.Button("Remove", GUILayout.Width(72)))
                {
                    _resource.Structures.RemoveAt(i--);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    continue;
                }
                EditorGUILayout.EndHorizontal();
                item.Name = EditorGUILayout.TextField("Name", item.Name);
                item.Kind = EditorGUILayout.Popup("Kind", item.ParsedKind == MobaStructureKind.Tower ? 1 : 0, new[] { "Base", "Tower" }) == 0 ? "Base" : "Tower";
                item.TeamId = EditorGUILayout.IntField("Team Id", item.TeamId);
                item.AttributeTemplateId = EditorGUILayout.IntField("Attribute Template", item.AttributeTemplateId);
                item.ModelId = EditorGUILayout.IntField("Model Id", item.ModelId);
                var position = item.Position;
                if (position == null) item.Position = position = new AbilityKit.Demo.Moba.Share.Config.MapVector3DTO();
                var vector = EditorGUILayout.Vector3Field("Position", new Vector3(position.X, position.Y, position.Z));
                position.X = vector.x; position.Y = vector.y; position.Z = vector.z;
                item.YawDegrees = EditorGUILayout.FloatField("Yaw", item.YawDegrees);
                item.Radius = EditorGUILayout.FloatField("Radius", item.Radius);
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();
        }

        private void Reload()
        {
            var root = GetRepositoryRoot();
            var path = Path.Combine(root, SourceRelativePath.Replace('/', Path.DirectorySeparatorChar));
            _resource = MobaMapStructureResourceLoader.LoadResource(File.ReadAllText(path));
        }

        private void AddStructure(string kind)
        {
            var id = 1;
            for (var i = 0; i < _resource.Structures.Count; i++) id = Math.Max(id, _resource.Structures[i].Id + 1);
            _resource.Structures.Add(new MobaMapStructureDefinition
            {
                Id = id,
                Name = $"Team 1 {kind}",
                Kind = kind,
                TeamId = 1,
                AttributeTemplateId = 1001,
                Position = new AbilityKit.Demo.Moba.Share.Config.MapVector3DTO(),
                Radius = kind == "Base" ? 1.5f : 1f,
            });
        }

        private void Publish()
        {
            try
            {
                var root = GetRepositoryRoot();
                var serialized = JsonConvert.SerializeObject(_resource, Formatting.Indented) + Environment.NewLine;
                MobaMapStructureResourceLoader.LoadResource(serialized);
                var mapAsset = AssetDatabase.LoadAssetAtPath<TextAsset>("Packages/com.abilitykit.demo.moba.view.runtime/Resources/moba/battle_maps.json");
                if (mapAsset == null) throw new InvalidOperationException("Published battle_maps resource was not found.");
                var maps = JsonConvert.DeserializeObject<AbilityKit.Demo.Moba.Share.Config.BattleMapDTO[]>(mapAsset.text);
                var map = Array.Find(maps, item => item != null && item.Id == _resource.MapId);
                if (map == null) throw new InvalidOperationException($"Map id was not found. mapId={_resource.MapId}");
                var bounds = map.Bounds;
                foreach (var structure in _resource.Structures)
                {
                    var p = structure.Position;
                    if (p == null || Math.Abs(p.X - bounds.Center.X) > bounds.Size.X * 0.5f || Math.Abs(p.Z - bounds.Center.Z) > bounds.Size.Z * 0.5f)
                        throw new InvalidOperationException($"Structure is outside map bounds. structureId={structure.Id}");
                }
                Write(root, SourceRelativePath, serialized);
                Write(root, RuntimeRelativePath, serialized);
                Write(root, ConsoleRelativePath, serialized);
                AssetDatabase.ImportAsset("Packages/com.abilitykit.demo.moba.runtime/Resources/moba/map_structures.json", ImportAssetOptions.ForceUpdate);
                Debug.Log($"[MobaMapStructureEditorWindow] Published {_resource.Structures.Count} structure(s) for map {_resource.MapId}.");
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorUtility.DisplayDialog("MOBA Map Structures", error.Message, "OK");
            }
        }

        private static string GetRepositoryRoot()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            return Directory.GetParent(projectRoot ?? string.Empty)?.FullName
                ?? throw new InvalidOperationException("Repository root could not be resolved.");
        }

        private static void Write(string root, string relative, string value)
        {
            var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, value);
        }
    }
}
