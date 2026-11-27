using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AbilityKit.Demo.Moba.Editor.BattleScenario;
using AbilityKit.Demo.Moba.EnvironmentModel;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.Editor
{
    /// <summary>
    /// 只枚举已发布的 Console 批次；单场文件在显式选择前不读取。
    /// </summary>
    internal sealed class BattleDebugScenarioRunCatalog
    {
        private readonly List<MobaScenarioRunIndex> _runs = new List<MobaScenarioRunIndex>();
        public IReadOnlyList<MobaScenarioRunIndex> Runs => _runs;
        public string Root { get; private set; }
        public int UnreadableCount { get; private set; }

        public MobaScenarioRunIndex Find(string runId) =>
            _runs.Find(run => string.Equals(run.runId, runId, StringComparison.Ordinal));

        public void Refresh(string root)
        {
            Root = null;
            _runs.Clear();
            UnreadableCount = 0;
            var full = Path.GetFullPath(root);
            if (!Directory.Exists(full)) return;
            if (IsReparsePoint(full)) throw new IOException("结果目录不能是重解析点。");
            Root = full;
            foreach (var directory in Directory.EnumerateDirectories(full))
            {
                var name = Path.GetFileName(directory);
                if (name.StartsWith(".", StringComparison.Ordinal)) continue;
                try
                {
                    if (IsReparsePoint(directory)) continue;
                    var indexPath = Path.Combine(directory, MobaScenarioRunIndex.FileName);
                    if (!File.Exists(indexPath) || IsReparsePoint(indexPath)) continue;
                    if (new FileInfo(indexPath).Length > 16 * 1024 * 1024) throw new IOException("批次索引过大。");

                    var index = JsonConvert.DeserializeObject<MobaScenarioRunIndex>(File.ReadAllText(indexPath));
                    if (index == null || index.suiteId != MobaScenarioRunIndex.Suite) continue;
                    if (index.schemaVersion != 1 || index.runId != name || index.cases == null || index.counts == null)
                        throw new InvalidDataException("批次索引无效或版本不受支持。");
                    _runs.Add(index);
                }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is JsonException)
                {
                    UnreadableCount++;
                }
            }
            _runs.Sort((left, right) => string.CompareOrdinal(right.runId, left.runId));
        }

        /// <summary>
        /// 解析批次内现有文件，拒绝跨批次引用及经过重解析点的路径。
        /// </summary>
        public string ResolveFile(MobaScenarioRunIndex run, string relative)
        {
            if (Root == null || !_runs.Contains(run) || string.IsNullOrWhiteSpace(relative))
                throw new InvalidDataException("当前批次没有可读的文件引用。");
            if (IsReparsePoint(Root)) throw new IOException("结果目录不能是重解析点。");
            var path = Path.Combine(Root, run.runId);
            if (IsReparsePoint(path)) throw new IOException("批次目录不能是重解析点。");
            foreach (var part in relative.Split('/'))
            {
                if (part.Length == 0 || part == "." || part == ".." || part.EndsWith(".", StringComparison.Ordinal) ||
                    part.EndsWith(" ", StringComparison.Ordinal) || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                    part.Contains("\\") || part.Contains(":"))
                    throw new InvalidDataException("文件引用必须是批次内的相对路径。");
                path = Path.Combine(path, part);
                if (IsReparsePoint(path)) throw new IOException("批次文件路径不能经过重解析点。");
            }
            if (!File.Exists(path)) throw new FileNotFoundException("批次记录引用的文件不存在。", relative);
            return path;
        }

        private static bool IsReparsePoint(string path) => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
    }

    /// <summary>
    /// 分开配置场景预设和持久分析库；只响应显式执行操作启动 Console。
    /// </summary>
    internal sealed class BattleDebugScenarioRuns
    {
        private const string PreferencesPrefix = "AbilityKit.BattleDebug.ScenarioAnalysis.";
        private static readonly string[] Filters = { "全部", "需排查", "断言通过", "无断言" };
        private static readonly string[] EvidenceModes = { "failed", "none", "all" };
        private static readonly string[] EvidenceLabels = { "失败留证", "仅判定", "全量留证" };
        private static Task<MobaBattleScenarioRunner.ArchiveBatchExecution> _execution;
        private readonly BattleDebugScenarioRunCatalog _catalog = new BattleDebugScenarioRunCatalog();
        private readonly HashSet<string> _selectedCases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private string _root;
        private string _presetDirectory;
        private string[] _caseFiles = Array.Empty<string>();
        private string _presetError;
        private string _executionMessage;
        private string _executionLog;
        private bool _allCases = true;
        private int _evidence;
        private string _message;
        private string _detail;
        private string _search = string.Empty;
        private int _filter;
        private MobaScenarioRunIndex _run;
        private MobaScenarioRunIndex.Case _entry;
        private Vector2 _scroll;
        private Vector2 _caseScroll;

        internal static string DefaultAnalysisRoot() => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AbilityKit", "BattleAnalysis", "MOBA", "runs");

        public void Draw(Action<string, string> openArtifact)
        {
            if (_root == null)
            {
                _root = EditorPrefs.GetString(PreferenceKey("Root"), DefaultAnalysisRoot());
                _presetDirectory = EditorPrefs.GetString(PreferenceKey("Presets"), MobaBattleScenarioRunner.DefaultArchivePresetDirectory());
                ReloadPresets();
                Refresh();
            }

            EditorGUILayout.LabelField("Console 场景批次", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(_execution != null))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (DrawDirectory("分析库", ref _root, "Root"))
                    {
                        _executionMessage = null;
                        Refresh();
                    }
                    if (GUILayout.Button("刷新批次", GUILayout.Width(74))) Refresh();
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (DrawDirectory("预设目录", ref _presetDirectory, "Presets"))
                    {
                        _selectedCases.Clear();
                        ReloadPresets();
                    }
                    if (GUILayout.Button("刷新预设", GUILayout.Width(74))) ReloadPresets();
                }
                DrawExecutionControls();
            }
            if (_execution != null) EditorGUILayout.HelpBox("正在后台执行 .NET Console 批次，完成后将定位本次结果。", MessageType.Info);
            if (_executionMessage != null)
            {
                EditorGUILayout.HelpBox(_executionMessage, MessageType.Info);
                if (!string.IsNullOrEmpty(_executionLog) && Directory.Exists(_executionLog) && GUILayout.Button("查看运行日志"))
                    EditorUtility.RevealInFinder(_executionLog);
            }
            if (!string.IsNullOrEmpty(_message)) EditorGUILayout.HelpBox(_message, MessageType.Info);
            if (_catalog.Runs.Count == 0) return;

            var runIndex = 0;
            var labels = new string[_catalog.Runs.Count];
            for (var index = 0; index < labels.Length; index++)
            {
                var run = _catalog.Runs[index];
                labels[index] = $"{run.startedAt} · {run.counts.total} 场 · {run.counts.failed} 失败 / {run.counts.errors} 错误 · {run.runId}";
                if (ReferenceEquals(_run, run)) runIndex = index;
            }
            var selected = _catalog.Runs[EditorGUILayout.Popup("批次", runIndex, labels)];
            if (!ReferenceEquals(selected, _run)) SelectRun(selected);
            var counts = _run.counts;
            EditorGUILayout.LabelField($"通过 {counts.passed} · 失败 {counts.failed} · 错误 {counts.errors} · 无断言 {counts.noAssertions}",
                EditorStyles.wordWrappedLabel);
            if (!string.IsNullOrEmpty(_run.evidenceMode))
                EditorGUILayout.LabelField($"留证策略：{_run.evidenceMode} · 批次耗时 {_run.durationMs / 1000d:F1} s", EditorStyles.miniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                _filter = EditorGUILayout.Popup(_filter, Filters, GUILayout.Width(92));
                _search = EditorGUILayout.TextField(_search);
            }

            using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = scroll.scrollPosition;
                var visible = 0;
                foreach (var entry in _run.cases)
                {
                    if (entry == null || !Matches(entry)) continue;
                    visible++;
                    var label = $"{Status(entry.status)} · {entry.caseId} · 证据：{Evidence(entry.evidenceStatus)}";
                    if (GUILayout.Button(label, ReferenceEquals(entry, _entry) ? EditorStyles.miniButtonMid : EditorStyles.miniButton))
                    {
                        _entry = entry;
                        _detail = null;
                    }
                    if (!ReferenceEquals(entry, _entry)) continue;
                    EditorGUILayout.LabelField(entry.scenario, EditorStyles.miniLabel);
                    EditorGUILayout.LabelField(entry.summary, EditorStyles.wordWrappedLabel);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("验收明细")) ReadDetail();
                        using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(entry.diagnosticUri)))
                        {
                            if (GUILayout.Button("打开单场证据"))
                            {
                                try
                                {
                                    var file = _catalog.ResolveFile(_run, entry.diagnosticUri);
                                    openArtifact(File.ReadAllText(file), file);
                                }
                                catch (Exception error) { _detail = "打开证据失败：" + error.Message; }
                            }
                        }
                    }
                    if (_detail != null) EditorGUILayout.LabelField(_detail, EditorStyles.wordWrappedLabel);
                }
                if (visible == 0) EditorGUILayout.HelpBox("没有匹配的用例，试试切换到「全部」。", MessageType.Info);
            }
        }

        private static string PreferenceKey(string name) => PreferencesPrefix + name + "." + Application.dataPath;

        private static bool DrawDirectory(string label, ref string value, string preference)
        {
            GUILayout.Label(label, GUILayout.Width(62));
            EditorGUILayout.SelectableLabel(value, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            if (!GUILayout.Button("选择…", GUILayout.Width(64))) return false;
            var chosen = EditorUtility.OpenFolderPanel("选择" + label, Directory.Exists(value) ? value : string.Empty, string.Empty);
            if (string.IsNullOrEmpty(chosen)) return false;
            value = Path.GetFullPath(chosen);
            EditorPrefs.SetString(PreferenceKey(preference), value);
            return true;
        }

        private void ReloadPresets()
        {
            _presetError = null;
            try
            {
                _caseFiles = Directory.GetFiles(_presetDirectory, "*.battlescenario")
                    .Select(Path.GetFileNameWithoutExtension).OrderBy(name => name, StringComparer.Ordinal).ToArray();
                _selectedCases.IntersectWith(_caseFiles);
            }
            catch (Exception error)
            {
                _caseFiles = Array.Empty<string>();
                _presetError = "无法读取该预设目录：" + error.Message;
            }
        }

        private void DrawExecutionControls()
        {
            if (_presetError != null) EditorGUILayout.HelpBox(_presetError, MessageType.Warning);
            using (new EditorGUILayout.HorizontalScope())
            {
                _allCases = EditorGUILayout.ToggleLeft($"运行目录内全部用例（{_caseFiles.Length}）", _allCases, GUILayout.MinWidth(184));
                _evidence = EditorGUILayout.Popup(_evidence, EvidenceLabels, GUILayout.Width(100));
                using (new EditorGUI.DisabledScope(_allCases ? _caseFiles.Length == 0 : _selectedCases.Count == 0))
                    if (GUILayout.Button("批量执行", GUILayout.Width(88))) StartExecution();
            }
            if (_allCases || _caseFiles.Length == 0) return;
            using (var scroll = new EditorGUILayout.ScrollViewScope(_caseScroll, GUILayout.MaxHeight(116)))
            {
                _caseScroll = scroll.scrollPosition;
                foreach (var caseFile in _caseFiles)
                {
                    var selected = EditorGUILayout.ToggleLeft(caseFile, _selectedCases.Contains(caseFile));
                    if (selected) _selectedCases.Add(caseFile);
                    else _selectedCases.Remove(caseFile);
                }
            }
        }

        private void StartExecution()
        {
            if (_execution != null) return;
            _executionMessage = _executionLog = null;
            try
            {
                var root = Path.GetFullPath(_root);
                var presets = Path.GetFullPath(_presetDirectory);
                ValidateBatchDirectories(presets, root);
                var selected = _allCases ? Array.Empty<string>() : _caseFiles.Where(_selectedCases.Contains).ToArray();
                _execution = MobaBattleScenarioRunner.ExecuteArchiveBatch(presets, root, EvidenceModes[_evidence], selected);
            }
            catch (Exception error) { _executionMessage = "未启动批次：" + error.Message; }
        }

        internal static void ValidateBatchDirectories(string presets, string root)
        {
            var prefix = presets.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var destination = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (destination.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new IOException("分析库不能放在预设目录中。");
        }

        /// <summary>
        /// 在编辑器主线程按 Console 回执定位批次；退出码非零也允许查看已发布结果。
        /// </summary>
        public bool PollExecution()
        {
            if (_root == null || _execution == null || !_execution.IsCompleted) return false;
            var result = _execution.GetAwaiter().GetResult();
            _execution = null;
            _executionLog = result.LogDirectory;
            if (string.Equals(Path.GetFullPath(_root), result.OutputRoot, StringComparison.Ordinal) && !string.IsNullOrEmpty(result.RunId))
            {
                Refresh(result.RunId);
                if (_run?.runId == result.RunId)
                {
                    _search = string.Empty;
                    _filter = _run.counts.failed + _run.counts.errors > 0 ? 1 : 0;
                    _executionMessage = $"已选中本次批次（Console 退出码 {result.ExitCode}）。";
                }
                else _executionMessage = "Console 已发布批次，但分析库没有读到回执指向的索引；请查看运行日志。";
            }
            else _executionMessage = result.Error ?? "批次写入了其他分析库，请切换到：" + result.OutputRoot;
            return true;
        }

        private void Refresh(string preferredRun = null)
        {
            var runId = preferredRun ?? _run?.runId;
            try
            {
                _catalog.Refresh(_root);
                var selected = _catalog.Find(runId);
                SelectRun(selected ?? (_catalog.Runs.Count > 0 ? _catalog.Runs[0] : null));
                _message = _catalog.Runs.Count == 0 ? "没有已发布的 Console 场景批次。" : null;
                if (_catalog.UnreadableCount > 0) _message = $"跳过 {_catalog.UnreadableCount} 个无法读取的批次索引。";
            }
            catch (Exception error) { _message = "读取结果目录失败：" + error.Message; }
        }

        private void ReadDetail()
        {
            if (_detail != null) { _detail = null; return; }
            try
            {
                var file = _catalog.ResolveFile(_run, _entry.summaryUri);
                if (new FileInfo(file).Length > 512 * 1024) throw new IOException("摘要过大，请在结果目录中打开文件。");
                _detail = File.ReadAllText(file);
            }
            catch (Exception error) { _detail = "读取验收明细失败：" + error.Message; }
        }

        private void SelectRun(MobaScenarioRunIndex run)
        {
            _run = run;
            _entry = null;
            _detail = null;
            _scroll = Vector2.zero;
        }

        private bool Matches(MobaScenarioRunIndex.Case entry) =>
            (_filter == 0 || (_filter == 1 && (entry.status == "failed" || entry.status == "error")) ||
             (_filter == 2 && entry.status == "passed") || (_filter == 3 && entry.status == "no-assertions")) &&
            ((entry.caseId ?? string.Empty).IndexOf(_search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0);

        private static string Status(string status) => status switch
        {
            "passed" => "断言通过", "failed" => "断言失败", "error" => "运行错误", "no-assertions" => "无断言", _ => "未知"
        };

        private static string Evidence(string status) => status switch
        {
            "available" => "已保留", "not-retained" => "未保留", "not-captured" => "未采集", "error" => "采集/导出失败", _ => "未知"
        };
    }
}
