using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace AbilityKit.Editor.GamplayTag
{
    public sealed class GameplayTagManagerWindow : EditorWindow
    {
        private const string DatabasePathPrefKey = "AbilityKit.GameplayTags.DatabasePath";
        private const string DefaultAssetPath = "Assets/GameplayTagDatabase.asset";

        private string _exportPath;

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

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Export", GUILayout.Width(45));
                _exportPath = GUILayout.TextField(_exportPath ?? string.Empty, EditorStyles.toolbarTextField, GUILayout.MinWidth(260));

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
            }

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

                if (GUILayout.Button("Export", EditorStyles.toolbarButton, GUILayout.Width(70)))
                {
                    GameplayTagLibExporter.SetOutputPath(_exportPath);
                    GameplayTagLibExporter.Export(_db, GameplayTagLibExporter.GetOutputPath());
                }

                if (GUILayout.Button("Validate", EditorStyles.toolbarButton, GUILayout.Width(80)))
                {
                    var report = GameplayTagValidation.Validate(_db.Tags);
                    if (string.IsNullOrEmpty(report))
                    {
                        EditorUtility.DisplayDialog("Gameplay Tags", "OK", "Close");
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Gameplay Tags", report, "Close");
                    }
                }

                GUILayout.FlexibleSpace();
                _tree.searchString = _search.OnToolbarGUI(_tree.searchString);
            }

            var rect = GUILayoutUtility.GetRect(0, 100000, 0, 100000);
            _tree.OnGUI(rect);
        }

        private void ScanCodeAndMerge()
        {
            var root = Application.dataPath;
            var files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);

            // GameplayTags.Tag("A.B.C")
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
                    // still scan editor code (it may contain tags), do not skip
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

    internal static class GameplayTagValidation
    {
        public static string Validate(IReadOnlyList<string> tags)
        {
            if (tags == null || tags.Count == 0) return "No tags.";

            var set = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < tags.Count; i++)
            {
                var t = tags[i];
                if (string.IsNullOrWhiteSpace(t)) return $"Invalid empty tag at index {i}";
                if (!TryNormalize(t, out var n)) return $"Invalid tag: '{t}'";
                if (!set.Add(n)) return $"Duplicate tag: '{n}'";
            }

            return string.Empty;
        }

        public static bool TryNormalize(string name, out string normalized)
        {
            normalized = null;
            if (string.IsNullOrWhiteSpace(name)) return false;

            var s = name.Trim();
            if (s.Length == 0) return false;
            if (s[0] == '.' || s[s.Length - 1] == '.') return false;

            for (int i = 0; i < s.Length; i++)
            {
                var c = s[i];
                if (c == '.')
                {
                    if (i > 0 && s[i - 1] == '.') return false;
                    continue;
                }

                if (char.IsWhiteSpace(c)) return false;
            }

            normalized = s;
            return true;
        }

        public static bool IsValidSegment(string segment)
        {
            if (string.IsNullOrWhiteSpace(segment)) return false;
            var s = segment.Trim();
            if (s.Length == 0) return false;
            if (s.Contains(".")) return false;
            for (int i = 0; i < s.Length; i++)
            {
                if (char.IsWhiteSpace(s[i])) return false;
            }
            return true;
        }
    }
}
