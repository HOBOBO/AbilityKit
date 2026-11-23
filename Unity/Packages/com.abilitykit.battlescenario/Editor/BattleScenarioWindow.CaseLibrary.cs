#if UNITY_EDITOR
#nullable enable annotations
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;

[assembly: InternalsVisibleTo("AbilityKit.Demo.Moba.BattleScenario.Editor.Tests")]

namespace AbilityKit.BattleScenario.Editor
{
    public sealed partial class BattleScenarioWindow
    {
        [SerializeField] private string _caseSearch = string.Empty;
        [SerializeField] private bool _caseLibraryExpanded = true;
        private readonly List<CaseLibraryEntry> _caseLibrary = new List<CaseLibraryEntry>();
        private Vector2 _caseLibraryScroll;
        private string? _caseLibraryError;
        private EditorSnapshot? _cleanDraft;
        private string? _savedCaseSource;
        private string _savedScenePath = string.Empty;
        private string? _savedSceneSource;

        private void DrawExecutionToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("配置校验", EditorStyles.toolbarButton, GUILayout.Width(70))) ValidateCurrent();
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || _casePath.Length == 0))
                {
                    using (new EditorGUI.DisabledScope(BattleScenarioEditorIntegration.PlaySavedCase == null))
                    {
                        if (GUILayout.Button("技能预览", EditorStyles.toolbarButton, GUILayout.Width(70))) PlaySavedCase(previewOnly: true);
                        if (GUILayout.Button("播放 ▾", EditorStyles.toolbarDropDown, GUILayout.Width(60)))
                        {
                            var menu = new GenericMenu();
                            menu.AddItem(new GUIContent("交互播放（保留 HUD 与手动输入）"), false,
                                () => PlaySavedCase(previewOnly: false));
                            menu.ShowAsContext();
                        }
                    }
                    if (GUILayout.Button("运行断言", EditorStyles.toolbarButton, GUILayout.Width(70))) RunSavedAssertions();
                }
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                    if (GUILayout.Button("批量运行", EditorStyles.toolbarButton, GUILayout.Width(70))) RunBatch();

                var label = _casePath.Length == 0 ? "先选择或保存用例" : Path.GetFileName(_casePath);
                GUILayout.Label(new GUIContent(label, _casePath), EditorStyles.miniLabel, GUILayout.Width(200));
                GUILayout.FlexibleSpace();
                GUILayout.Label("预览与断言使用已保存版本", EditorStyles.miniLabel);
            }
        }

        private void DrawCaseLibrary()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _caseLibraryExpanded = EditorGUILayout.Foldout(_caseLibraryExpanded, "用例库", true);
                if (GUILayout.Button("刷新", GUILayout.Width(44))) RefreshCaseLibrary();
            }
            if (!_caseLibraryExpanded) return;
            _caseSearch = EditorGUILayout.TextField(_caseSearch, EditorStyles.toolbarSearchField);
            if (!string.IsNullOrEmpty(_caseLibraryError)) EditorGUILayout.HelpBox(_caseLibraryError, MessageType.Warning);
            var matches = 0;
            _caseLibraryScroll = EditorGUILayout.BeginScrollView(_caseLibraryScroll, GUILayout.MaxHeight(260));
            foreach (var entry in FilterCases(_caseSearch))
            {
                matches++;
                var selected = string.Equals(entry.Path, _casePath, StringComparison.OrdinalIgnoreCase);
                if (GUILayout.Button(new GUIContent((selected ? "● " : "") + entry.Label, entry.Tooltip), EditorStyles.miniButton))
                    SelectCase(entry.Path);
            }
            if (matches == 0) EditorGUILayout.LabelField("没有匹配用例（名称 / 英雄 / 槽位 / 标签）", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndScrollView();
        }

        private void RefreshCaseLibrary()
        {
            _caseLibrary.Clear();
            _caseLibraryError = null;
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var directory in BattleScenarioEditorIntegration.CaseDirectories.Prepend(_caseDirectory))
            {
                try
                {
                    if (Directory.Exists(directory))
                        foreach (var file in Directory.EnumerateFiles(directory, "*.battlescenario", SearchOption.AllDirectories))
                            paths.Add(Path.GetFullPath(file));
                }
                catch (Exception error)
                {
                    _caseLibraryError = "用例目录读取失败：" + error.Message;
                }
            }
            if (File.Exists(_casePath)) paths.Add(_casePath);
            foreach (var path in paths.OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).ThenBy(value => value, StringComparer.Ordinal))
                _caseLibrary.Add(ReadCaseEntry(path));
            Repaint();
        }

        internal IEnumerable<CaseLibraryEntry> FilterCases(string search)
        {
            var query = search.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            return _caseLibrary.Where(entry => entry.Matches(query));
        }

        private static CaseLibraryEntry ReadCaseEntry(string path)
        {
            var fileName = Path.GetFileNameWithoutExtension(path);
            var entry = new CaseLibraryEntry { Path = path, Label = fileName, Tooltip = path };
            entry.Terms.Add(fileName);
            try
            {
                var document = BattleScenarioCodec.Load(path);
                if (!string.IsNullOrWhiteSpace(document.DisplayName)) entry.Label = document.DisplayName;
                entry.Terms.AddRange(new[] { document.DisplayName, document.CaseId });
                entry.Terms.AddRange(document.Tags);
                entry.Tooltip += "\n" + string.Join(" · ", document.Tags);
                try
                {
                    if (BattleScenarioEditorIntegration.SearchTerms != null)
                        entry.Terms.AddRange(BattleScenarioEditorIntegration.SearchTerms(document));
                }
                catch (Exception error)
                {
                    entry.Tooltip += "\n项目搜索词不可用：" + error.Message;
                }
            }
            catch (Exception error)
            {
                entry.Label += "（读取失败）";
                entry.Tooltip += "\n" + error.Message;
            }
            return entry;
        }

        private void SelectCase(string path)
        {
            if (IsDraftDirty() && !EditorUtility.DisplayDialog("切换用例", "当前编辑尚未保存。要放弃修改并加载用例吗？", "放弃并加载", "取消"))
                return;
            if (LoadCaseFromPath(path) && !_caseLibrary.Any(entry => entry.Path == _casePath)) RefreshCaseLibrary();
        }

        internal void RestoreSelectedCase()
        {
            var selectedPath = _casePath;
            _casePath = string.Empty;
            _cleanDraft = CaptureEditorSnapshot();
            if (!string.IsNullOrEmpty(selectedPath)) LoadCaseFromPath(selectedPath);
            else RefreshSceneLibrary();
        }

        internal bool LoadCaseFromPath(string path)
        {
            try
            {
                path = Path.GetFullPath(path);
                var source = File.ReadAllText(path);
                var document = BattleScenarioCodec.Parse(source);
                BattleScenarioDocumentValidator.ThrowIfInvalid(document);
                var scenePath = string.IsNullOrWhiteSpace(document.ScenarioRef) ? string.Empty
                    : BattleScenarioCodec.ResolveScenePath(path, document.ScenarioRef);
                var sceneSource = scenePath.Length == 0 ? null : File.ReadAllText(scenePath);
                var scene = sceneSource == null ? null : BattleScenarioCodec.ParseScene(sceneSource);
                if (scene != null) BattleScenarioDocumentValidator.ThrowIfInvalid(scene);
                LoadDocument(document, path, scenePath, scene);
                _undoStack.Clear();
                RememberSavedCase(source, scenePath, sceneSource);
                _traceNodes = null;
                _result = "已加载：" + path;
                return true;
            }
            catch (Exception error)
            {
                _result = "加载用例失败：" + error.Message;
                return false;
            }
        }

        private void RememberSavedCase(string source, string scenePath, string? sceneSource)
        {
            _cleanDraft = CaptureEditorSnapshot();
            _savedCaseSource = source;
            _savedScenePath = scenePath;
            _savedSceneSource = sceneSource;
        }

        private bool IsDraftDirty()
        {
            var current = CaptureEditorSnapshot();
            return _cleanDraft != null && (current.DocumentJson != _cleanDraft.DocumentJson ||
                current.ScenePath != _cleanDraft.ScenePath || current.SceneId != _cleanDraft.SceneId ||
                current.SceneDisplayName != _cleanDraft.SceneDisplayName || current.CasePath != _cleanDraft.CasePath);
        }

        internal bool TryGetSavedCasePath(out string path)
        {
            path = string.Empty;
            if (_casePath.Length == 0 || _savedCaseSource == null)
                _result = "请先选择或保存用例。";
            else if (IsDraftDirty())
                _result = "当前有未保存的修改。请先保存用例及其引用场景，或重新加载用例。";
            else if (!File.Exists(_casePath) || File.ReadAllText(_casePath) != _savedCaseSource ||
                (_savedSceneSource != null && (!File.Exists(_savedScenePath) || File.ReadAllText(_savedScenePath) != _savedSceneSource)))
                _result = "磁盘上的用例或引用场景已变化，请重新加载用例。";
            else
                path = _casePath;
            return path.Length != 0;
        }

        internal void PlaySavedCase(bool previewOnly)
        {
            _traceNodes = null;
            try
            {
                if (!TryGetSavedCasePath(out var path)) return;
                var play = BattleScenarioEditorIntegration.PlaySavedCase;
                if (play == null) _result = "项目未注册画面播放入口。";
                else if (!play(path, previewOnly)) _result = "未启动播放。";
                else _result = (previewOnly ? "已启动技能预览：" : "已启动交互播放：") + Path.GetFileName(path);
            }
            catch (Exception error)
            {
                _result = "无法播放：" + error.Message;
            }
        }

        internal sealed class CaseLibraryEntry
        {
            public string Path = string.Empty;
            public string Label = string.Empty;
            public string Tooltip = string.Empty;
            public readonly List<string> Terms = new List<string>();

            public bool Matches(string[] query) => query.All(token => Terms.Any(term => !string.IsNullOrEmpty(term) &&
                (token.Length == 1 && char.IsLetter(token[0]) && token[0] <= 127
                    ? term.Split(' ', '-', '_').Contains(token, StringComparer.OrdinalIgnoreCase)
                    : term.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)));
        }
    }
}
#endif
