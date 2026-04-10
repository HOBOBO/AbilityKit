using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace AbilityKit.GameplayTags.Editor
{
    public sealed class GameplayTagManagerWindow : EditorWindow
    {
        private const string DatabasePathPrefKey = "AbilityKit.GameplayTags.DatabasePath";
        private const string DefaultAssetPath = "Assets/GameplayTagDatabase.asset";
        private const string DefaultJsonPath = "Assets/GameplayTagDatabase.json";

        private string _exportPath;
        private string _jsonExportPath;

        [SerializeField] private TreeViewState _treeState;
        [SerializeField] private MultiColumnHeaderState _headerState;

        private GameplayTagDatabase _db;
        private GameplayTagTreeView _tree;
        private SearchField _search;

        [MenuItem("Tools/Gameplay Tags/Manager")]
        public static void Open()
        {
            var w = GetWindow<GameplayTagManagerWindow>();
            w.titleContent = new GUIContent("Gameplay Tags");
            w.Show();
        }

        private void OnEnable()
        {
            _search ??= new SearchField();
            EnsureDatabase();
            EnsureTree();

            _exportPath = GameplayTagLibExporter.GetOutputPath();
            _jsonExportPath = EditorPrefs.GetString("AbilityKit.GameplayTags.JsonExportPath", DefaultJsonPath);

            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        }

        private void OnUndoRedoPerformed()
        {
            if (_db == null || _tree == null) return;
            GameplayTagLibExporter.Export(_db);
            _tree.Reload();
            Repaint();
        }

        private void EnsureDatabase()
        {
            if (_db != null) return;

            var path = EditorPrefs.GetString(DatabasePathPrefKey, DefaultAssetPath);
            if (string.IsNullOrWhiteSpace(path)) path = DefaultAssetPath;

            _db = AssetDatabase.LoadAssetAtPath<GameplayTagDatabase>(path);
            if (_db != null) return;

            _db = CreateInstance<GameplayTagDatabase>();
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? "Assets");
            AssetDatabase.CreateAsset(_db, path);
            AssetDatabase.SaveAssets();

            EditorPrefs.SetString(DatabasePathPrefKey, path);
        }

        private void SetDatabase(GameplayTagDatabase db)
        {
            if (db == null) return;
            _db = db;
            var p = AssetDatabase.GetAssetPath(db);
            if (!string.IsNullOrEmpty(p)) EditorPrefs.SetString(DatabasePathPrefKey, p);

            EnsureTree();
            _tree.Reload();
            Repaint();
        }

        private void CreateAndSelectDatabase()
        {
            var picked = EditorUtility.SaveFilePanelInProject(
                "Create GameplayTagDatabase",
                "GameplayTagDatabase",
                "asset",
                "Choose where to create GameplayTagDatabase.asset"
            );

            if (string.IsNullOrEmpty(picked)) return;

            var existing = AssetDatabase.LoadAssetAtPath<GameplayTagDatabase>(picked);
            if (existing != null)
            {
                SetDatabase(existing);
                return;
            }

            var db = CreateInstance<GameplayTagDatabase>();
            var dir = Path.GetDirectoryName(picked);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            AssetDatabase.CreateAsset(db, picked);
            AssetDatabase.SaveAssets();
            SetDatabase(db);
        }

        private void EnsureTree()
        {
            _treeState ??= new TreeViewState();

            var columns = GameplayTagTreeView.CreateDefaultColumns();
            _headerState ??= new MultiColumnHeaderState(columns);
            var header = new MultiColumnHeader(_headerState);

            _tree = new GameplayTagTreeView(_treeState, header, _db);
            _tree.OnDatabaseChanged += () =>
            {
                EditorUtility.SetDirty(_db);
                AssetDatabase.SaveAssets();
                GameplayTagLibExporter.Export(_db);
                _tree.Reload();
                Repaint();
            };
            _tree.Reload();
        }

        private void OnGUI()
        {
            if (_db == null) EnsureDatabase();
            if (_tree == null) EnsureTree();

            DrawToolbar();
            DrawExportToolbar();
            DrawJsonToolbar();
            DrawTreeView();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("DB", GUILayout.Width(20));

                var nextDb = (GameplayTagDatabase)EditorGUILayout.ObjectField(
                    _db,
                    typeof(GameplayTagDatabase),
                    false,
                    GUILayout.MinWidth(180)
                );
                if (nextDb != null && nextDb != _db)
                {
                    SetDatabase(nextDb);
                }

                if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(45)))
                {
                    CreateAndSelectDatabase();
                }

                if (GUILayout.Button("Ping", EditorStyles.toolbarButton, GUILayout.Width(45)))
                {
                    if (_db != null)
                    {
                        EditorGUIUtility.PingObject(_db);
                        Selection.activeObject = _db;
                    }
                }

                GUILayout.FlexibleSpace();
            }
        }

        private void DrawExportToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Export CS", GUILayout.Width(70));
                _exportPath = GUILayout.TextField(_exportPath ?? string.Empty, EditorStyles.toolbarTextField, GUILayout.MinWidth(200));

                if (GUILayout.Button("Browse", EditorStyles.toolbarButton, GUILayout.Width(60)))
                {
                    var picked = EditorUtility.SaveFilePanelInProject(
                        "GameplayTagLib output",
                        "GameplayTagLib",
                        "cs",
                        "Choose where to generate GameplayTagLib.cs",
                        Path.GetDirectoryName(string.IsNullOrEmpty(_exportPath) ? GameplayTagLibExporter.DefaultOutputPath : _exportPath)
                    );

                    if (!string.IsNullOrEmpty(picked))
                    {
                        _exportPath = picked;
                        GameplayTagLibExporter.SetOutputPath(_exportPath);
                        GameplayTagLibExporter.Export(_db, _exportPath);
                    }
                }

                if (GUILayout.Button("Ping", EditorStyles.toolbarButton, GUILayout.Width(45)))
                {
                    var p = string.IsNullOrEmpty(_exportPath) ? GameplayTagLibExporter.GetOutputPath() : _exportPath;
                    var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(p);
                    if (asset != null)
                    {
                        EditorGUIUtility.PingObject(asset);
                        Selection.activeObject = asset;
                    }
                }

                if (GUILayout.Button("Export", EditorStyles.toolbarButton, GUILayout.Width(60)))
                {
                    GameplayTagLibExporter.SetOutputPath(_exportPath);
                    GameplayTagLibExporter.Export(_db, GameplayTagLibExporter.GetOutputPath());
                }
            }
        }

        private void DrawJsonToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("JSON", GUILayout.Width(50));
                _jsonExportPath = GUILayout.TextField(_jsonExportPath ?? string.Empty, EditorStyles.toolbarTextField, GUILayout.MinWidth(200));

                if (GUILayout.Button("Browse", EditorStyles.toolbarButton, GUILayout.Width(60)))
                {
                    var picked = EditorUtility.SaveFilePanelInProject(
                        "JSON output",
                        "GameplayTagDatabase",
                        "json",
                        "Choose where to save JSON",
                        Path.GetDirectoryName(string.IsNullOrEmpty(_jsonExportPath) ? DefaultJsonPath : _jsonExportPath)
                    );

                    if (!string.IsNullOrEmpty(picked))
                    {
                        _jsonExportPath = picked;
                        EditorPrefs.SetString("AbilityKit.GameplayTags.JsonExportPath", _jsonExportPath);
                    }
                }

                if (GUILayout.Button("Export", EditorStyles.toolbarButton, GUILayout.Width(60)))
                {
                    ExportJson();
                }

                if (GUILayout.Button("Import", EditorStyles.toolbarButton, GUILayout.Width(60)))
                {
                    ImportJson();
                }

                GUILayout.FlexibleSpace();
            }
        }

        private void DrawTreeView()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(70)))
                {
                    _db.SortAndDedup();
                    EditorUtility.SetDirty(_db);
                    _tree.Reload();
                }

                if (GUILayout.Button("Scan Code", EditorStyles.toolbarButton, GUILayout.Width(90)))
                {
                    ScanCodeAndMerge();
                }

                if (GUILayout.Button("Validate", EditorStyles.toolbarButton, GUILayout.Width(80)))
                {
                    Validate();
                }

                if (GUILayout.Button("Validate JSON", EditorStyles.toolbarButton, GUILayout.Width(100)))
                {
                    ValidateJson();
                }

                GUILayout.FlexibleSpace();
                _tree.searchString = _search.OnToolbarGUI(_tree.searchString);
            }

            var rect = GUILayoutUtility.GetRect(0, 100000, 0, 100000);
            _tree.OnGUI(rect);
        }

        private void ExportJson()
        {
            if (string.IsNullOrEmpty(_jsonExportPath)) return;

            var json = GameplayTagJsonExporter.ExportToJson(_db);
            var dir = Path.GetDirectoryName(_jsonExportPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(_jsonExportPath, json, System.Text.Encoding.UTF8);
            AssetDatabase.ImportAsset(_jsonExportPath, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[GameplayTags] Exported to: {_jsonExportPath}");
        }

        private void ImportJson()
        {
            var picked = EditorUtility.OpenFilePanel("Import JSON", "", "json");
            if (string.IsNullOrEmpty(picked)) return;

            var json = System.IO.File.ReadAllText(picked, System.Text.Encoding.UTF8);
            var entries = GameplayTagJsonExporter.ParseFromJson(json);
            if (entries == null || entries.Count == 0)
            {
                EditorUtility.DisplayDialog("Import", "No valid tags found in JSON", "OK");
                return;
            }

            Undo.RecordObject(_db, "Import Gameplay Tags from JSON");
            foreach (var entry in entries)
            {
                _db.GetOrCreate(entry.Name);
                if (_db.TryGetEntry(entry.Name, out var e))
                {
                    e.Comment = entry.Comment ?? string.Empty;
                    e.Category = entry.Category ?? string.Empty;
                }
            }
            _db.SortAndDedup();
            EditorUtility.SetDirty(_db);
            AssetDatabase.SaveAssets();
            GameplayTagLibExporter.Export(_db);
            _tree.Reload();
            Debug.Log($"[GameplayTags] Imported {entries.Count} tags from JSON");
        }

        private void Validate()
        {
            var report = GameplayTagValidator.Validate(_db.Tags);
            if (string.IsNullOrEmpty(report))
            {
                EditorUtility.DisplayDialog("Gameplay Tags", "OK", "Close");
            }
            else
            {
                EditorUtility.DisplayDialog("Gameplay Tags", report, "Close");
            }
        }

        private void ValidateJson()
        {
            if (string.IsNullOrEmpty(_jsonExportPath)) return;
            if (!System.IO.File.Exists(_jsonExportPath))
            {
                EditorUtility.DisplayDialog("Validate JSON", "JSON file not found", "OK");
                return;
            }

            var json = System.IO.File.ReadAllText(_jsonExportPath, System.Text.Encoding.UTF8);
            var entries = GameplayTagJsonExporter.ParseFromJson(json);
            if (entries == null || entries.Count == 0)
            {
                EditorUtility.DisplayDialog("Validate JSON", "No valid tags found", "OK");
                return;
            }

            var tagNames = new List<string>();
            foreach (var e in entries) tagNames.Add(e.Name);
            var report = GameplayTagValidator.Validate(tagNames);
            if (string.IsNullOrEmpty(report))
            {
                EditorUtility.DisplayDialog("Validate JSON", $"OK ({entries.Count} tags)", "Close");
            }
            else
            {
                EditorUtility.DisplayDialog("Validate JSON", report, "Close");
            }
        }

        private void ScanCodeAndMerge()
        {
            var root = Application.dataPath;
            var files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);

            var rx = new Regex(@"GameplayTags\.Tag\(""(?<tag>[^""]+)""\)", RegexOptions.Compiled);

            var set = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _db.Tags.Count; i++) set.Add(_db.Tags[i]);

            var added = 0;
            Undo.RecordObject(_db, "Scan Gameplay Tags");
            for (int i = 0; i < files.Length; i++)
            {
                var path = files[i];
                if (path.Contains("/Editor/") || path.Contains("\\Editor\\"))
                {
                }

                string text;
                try
                {
                    text = File.ReadAllText(path);
                }
                catch
                {
                    continue;
                }

                var matches = rx.Matches(text);
                for (int m = 0; m < matches.Count; m++)
                {
                    var tag = matches[m].Groups["tag"].Value;
                    if (string.IsNullOrWhiteSpace(tag)) continue;
                    if (set.Add(tag))
                    {
                        _db.GetOrCreate(tag);
                        added++;
                    }
                }
            }

            _db.SortAndDedup();
            EditorUtility.SetDirty(_db);
            AssetDatabase.SaveAssets();
            GameplayTagLibExporter.SetOutputPath(_exportPath);
            GameplayTagLibExporter.Export(_db, GameplayTagLibExporter.GetOutputPath());
            _tree.Reload();

            Debug.Log($"[GameplayTagManagerWindow] ScanCode added {added} tags.");
        }
    }
}
