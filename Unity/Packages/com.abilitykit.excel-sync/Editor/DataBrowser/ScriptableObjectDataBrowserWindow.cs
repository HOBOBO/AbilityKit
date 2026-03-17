using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AbilityKit.ExcelSync.Editor;
using AbilityKit.ExcelSync.Editor.Codecs;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.ExcelSync.Editor.DataBrowser
{
    public sealed class ScriptableObjectDataBrowserWindow : EditorWindow
    {
        private sealed class RowEntry
        {
            public string Key;
            public int LocalIndex;
            public RowCompareStatus Status;
        }

        private enum RowCompareStatus
        {
            Unknown = 0,
            Unchanged,
            LocalModified,
            RemoteModified,
            Conflict,
            LocalNew,
            RemoteNew,
            LocalDeleted,
            RemoteDeleted,
        }

        private ScriptableObject _targetAsset;
        private readonly DataBrowserBindingContext _binding = new DataBrowserBindingContext();
        private readonly DefaultListAdapter _adapter = new DefaultListAdapter();
        private readonly DataBrowserPager _pager = new DataBrowserPager();
        private readonly ITableReaderWriterFactory _backend = new EpplusTableReaderWriterFactory();

        private string _keyword;
        private Vector2 _listScroll;
        private Vector2 _detailScroll;
        private string _selectedKey;
        private int _selectedLocalIndex = -1;

        // 过滤器选项
        private bool _filterConflicts;
        private bool _filterLocalModified;
        private bool _filterRemoteModified;
        private bool _filterNew;
        private bool _filterDeleted;

        // 批量编辑选项
        private readonly HashSet<string> _selectedKeys = new HashSet<string>();
        private bool _batchSelectMode;
        private string _batchEditFieldName;
        private string _batchEditValue;

        private string _excelAbsolutePath;
        private string _sheetName;
        private int _headerRowIndex = 1;
        private int _dataStartRowIndex = 2;
        private string _primaryKeyColumnName;
        private string _runtimeRowTypeName;
        private bool _excelSettingsInitialized;

        private ExcelSoSyncBaselineAsset _baselineAsset;
        private IReadOnlyList<string> _compareHeaders;
        private Dictionary<string, List<string>> _baselineRowMap;
        private Dictionary<string, List<string>> _remoteRowMap;
        private Dictionary<string, List<string>> _localRowMap;
        private Dictionary<string, int> _localIndexByKey;
        private Dictionary<string, RowCompareStatus> _statusByKey;
        private List<RowEntry> _allEntries;
        private string _remoteLoadError;
        private bool _compareDirty = true;

        private Func<object, string> _getName;
        private Func<object, string> _getDesc;

        [MenuItem("Tools/Excel/ScriptableObject Data Browser")]
        private static void Open()
        {
            GetWindow<ScriptableObjectDataBrowserWindow>(false, "SO Data Browser", true);
        }

        private void OnEnable()
        {
            if (_targetAsset != null)
            {
                BindTarget(_targetAsset);
            }
        }

        private void OnDisable()
        {
            _binding.Clear();
        }

        private void OnGUI()
        {
            DrawTargetPicker();
            DrawExcelSyncBar();
            DrawCompareBar();

            if (_targetAsset == null)
            {
                EditorGUILayout.HelpBox("Pick a ScriptableObject that contains DataList/Items/Rows.", MessageType.Info);
                return;
            }

            if (!_binding.IsBound || _binding.ListProperty == null)
            {
                EditorGUILayout.HelpBox($"Cannot bind list on '{_targetAsset.GetType().FullName}'. Expected fields: DataList/Items/Rows.", MessageType.Warning);
                if (GUILayout.Button("Rebind"))
                {
                    BindTarget(_targetAsset);
                }
                return;
            }

            _binding.SerializedTarget.Update();

            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField($"List: {_binding.ListPropertyName}    Element: {_binding.ElementType?.Name ?? "(unknown)"}    Count: {_binding.GetItemCount()}");
                
                // 关键字过滤
                _keyword = EditorGUILayout.TextField("Keyword (Name/Desc)", _keyword);
                
                // 状态过滤器
                EditorGUILayout.LabelField("Status Filters", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    _filterConflicts = GUILayout.Toggle(_filterConflicts, "Conflicts", "Button", GUILayout.Width(80));
                    _filterLocalModified = GUILayout.Toggle(_filterLocalModified, "Local", "Button", GUILayout.Width(60));
                    _filterRemoteModified = GUILayout.Toggle(_filterRemoteModified, "Remote", "Button", GUILayout.Width(70));
                    _filterNew = GUILayout.Toggle(_filterNew, "New", "Button", GUILayout.Width(50));
                    _filterDeleted = GUILayout.Toggle(_filterDeleted, "Deleted", "Button", GUILayout.Width(70));
                    
                    if (GUILayout.Button("Clear", GUILayout.Width(50)))
                    {
                        _filterConflicts = false;
                        _filterLocalModified = false;
                        _filterRemoteModified = false;
                        _filterNew = false;
                        _filterDeleted = false;
                    }
                }
                
                // 批量编辑工具栏
                EditorGUILayout.LabelField("Batch Edit", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    _batchSelectMode = GUILayout.Toggle(_batchSelectMode, "Batch Select", "Button", GUILayout.Width(100));
                    
                    if (GUILayout.Button("Select All", GUILayout.Width(80)))
                    {
                        BatchSelectAll();
                    }
                    
                    if (GUILayout.Button("Deselect All", GUILayout.Width(90)))
                    {
                        _selectedKeys.Clear();
                    }
                    
                    EditorGUI.BeginDisabledGroup(_selectedKeys.Count == 0);
                    if (GUILayout.Button("Delete Selected", GUILayout.Width(110)))
                    {
                        BatchDeleteSelected();
                    }
                    EditorGUI.EndDisabledGroup();
                }
                
                if (_selectedKeys.Count > 0)
                {
                    EditorGUILayout.LabelField($"Selected: {_selectedKeys.Count} items");
                    
                    // 批量编辑字段
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _batchEditFieldName = EditorGUILayout.TextField("Field", _batchEditFieldName);
                        _batchEditValue = EditorGUILayout.TextField("Value", _batchEditValue);
                        
                        if (GUILayout.Button("Apply Batch", GUILayout.Width(90)))
                        {
                            BatchEditField();
                        }
                    }
                }
            }

            EditorGUILayout.BeginHorizontal();

            var leftWidth = Mathf.Max(280f, position.width * 0.45f);
            DrawListPanel(leftWidth);
            DrawDetailPanel();

            EditorGUILayout.EndHorizontal();

            _binding.SerializedTarget.ApplyModifiedProperties();
        }

        private void DrawTargetPicker()
        {
            EditorGUILayout.Space(4);

            EditorGUI.BeginChangeCheck();
            var next = (ScriptableObject)EditorGUILayout.ObjectField("Target", _targetAsset, typeof(ScriptableObject), false);
            if (EditorGUI.EndChangeCheck())
            {
                _targetAsset = next;
                BindTarget(_targetAsset);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Ping", GUILayout.Width(60)) && _targetAsset != null)
                {
                    EditorGUIUtility.PingObject(_targetAsset);
                    Selection.activeObject = _targetAsset;
                }

                if (GUILayout.Button("Rebind", GUILayout.Width(60)) && _targetAsset != null)
                {
                    BindTarget(_targetAsset);
                }

                EditorGUI.BeginDisabledGroup(_binding.ListProperty == null || _selectedLocalIndex < 0);
                if (GUILayout.Button("Ping Item", GUILayout.Width(80)))
                {
                    var itemProp = _binding.ListProperty.GetArrayElementAtIndex(_selectedLocalIndex);
                    EditorGUIUtility.PingObject(_targetAsset);
                    Selection.activeObject = _targetAsset;
                    EditorGUIUtility.editingTextField = false;
                    EditorGUIUtility.keyboardControl = 0;
                    _detailScroll = Vector2.zero;
                }
                EditorGUI.EndDisabledGroup();
            }
        }

        private void BindTarget(ScriptableObject asset)
        {
            _binding.TryBind(_adapter, asset);
            _selectedKey = null;
            _selectedLocalIndex = -1;
            _listScroll = Vector2.zero;
            _detailScroll = Vector2.zero;
            _pager.ResetToFirstPage();

            _excelSettingsInitialized = false;
            _compareDirty = true;

            _getName = BuildStringGetter(_binding.ElementType, "Name");
            _getDesc = BuildStringGetter(_binding.ElementType, "Desc");

            Repaint();
        }

        private void DrawExcelSyncBar()
        {
            if (_targetAsset == null)
            {
                return;
            }

            EnsureExcelSettingsLoaded();

            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Excel Sync", EditorStyles.boldLabel);

                _excelAbsolutePath = EditorGUILayout.TextField("Excel Path", _excelAbsolutePath);
                _runtimeRowTypeName = EditorGUILayout.TextField("Runtime Row Type", _runtimeRowTypeName);
                _sheetName = EditorGUILayout.TextField("Sheet", _sheetName);
                _headerRowIndex = EditorGUILayout.IntField("Header Row", _headerRowIndex);
                _dataStartRowIndex = EditorGUILayout.IntField("Data Start Row", _dataStartRowIndex);
                _primaryKeyColumnName = EditorGUILayout.TextField("Primary Key", _primaryKeyColumnName);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Reload From Baseline", GUILayout.Width(160)))
                    {
                        _excelSettingsInitialized = false;
                        _compareDirty = true;
                        EnsureExcelSettingsLoaded();
                    }

                    EditorGUI.BeginDisabledGroup(string.IsNullOrWhiteSpace(_excelAbsolutePath));
                    if (GUILayout.Button("Import", GUILayout.Width(80)))
                    {
                        ExecuteExcelSync(export: false);
                    }
                    if (GUILayout.Button("Export", GUILayout.Width(80)))
                    {
                        ExecuteExcelSync(export: true);
                    }
                    EditorGUI.EndDisabledGroup();
                }
            }
        }

        private void DrawCompareBar()
        {
            if (_targetAsset == null)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Compare", EditorStyles.boldLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Refresh Compare", GUILayout.Width(140)))
                    {
                        _compareDirty = true;
                    }

                    if (_baselineAsset != null)
                    {
                        EditorGUILayout.ObjectField("Baseline", _baselineAsset, typeof(ExcelSoSyncBaselineAsset), false);
                    }
                    else
                    {
                        EditorGUILayout.LabelField("Baseline: (none)");
                    }
                }

                if (!string.IsNullOrWhiteSpace(_remoteLoadError))
                {
                    EditorGUILayout.HelpBox(_remoteLoadError, MessageType.Warning);
                }
            }
        }

        private void EnsureExcelSettingsLoaded()
        {
            if (_excelSettingsInitialized)
            {
                return;
            }

            _excelSettingsInitialized = true;

            if (TryLoadBaseline(_targetAsset, out var baseline) && baseline != null)
            {
                _excelAbsolutePath = baseline.ExcelAbsolutePath;
                _sheetName = baseline.SheetName;
                _headerRowIndex = baseline.HeaderRowIndex;
                _dataStartRowIndex = baseline.DataStartRowIndex;
                _primaryKeyColumnName = baseline.PrimaryKeyColumnName;
                return;
            }

            if (string.IsNullOrWhiteSpace(_sheetName))
            {
                _sheetName = "Sheet1";
            }
        }

        private static bool TryLoadBaseline(ScriptableObject targetAsset, out ExcelSoSyncBaselineAsset baseline)
        {
            baseline = null;
            if (targetAsset == null)
            {
                return false;
            }

            var assetPath = AssetDatabase.GetAssetPath(targetAsset);
            if (string.IsNullOrEmpty(assetPath))
            {
                return false;
            }

            var baselinePath = assetPath + ".excelBaseline";
            baseline = AssetDatabase.LoadAssetAtPath<ExcelSoSyncBaselineAsset>(baselinePath);
            if (baseline != null)
            {
                return true;
            }

            baselinePath = assetPath + ".excelBaseline.asset";
            baseline = AssetDatabase.LoadAssetAtPath<ExcelSoSyncBaselineAsset>(baselinePath);
            return baseline != null;
        }

        private void ExecuteExcelSync(bool export)
        {
            if (_targetAsset == null)
            {
                return;
            }

            var options = new ExcelTableOptions
            {
                SheetName = _sheetName,
                HeaderRowIndex = _headerRowIndex,
                DataStartRowIndex = _dataStartRowIndex,
                PrimaryKeyColumnName = _primaryKeyColumnName
            };

            if (!string.IsNullOrWhiteSpace(_runtimeRowTypeName))
            {
                ExcelSyncService.ValidateSchema(_targetAsset, _runtimeRowTypeName);
            }

            if (export)
            {
                ExcelSyncService.Export(_targetAsset, _excelAbsolutePath, options, _backend);
            }
            else
            {
                ExcelSyncService.Import(_targetAsset, _excelAbsolutePath, options, _backend);
            }

            BindTarget(_targetAsset);
            _compareDirty = true;
        }

        private void DrawListPanel(float width)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(width)))
            {
                EditorGUILayout.LabelField("Results", EditorStyles.boldLabel);

                EnsureCompareBuilt();

                var matched = CollectMatchedEntries();

                _pager.GetRange(matched.Count, out var start, out var end);

                _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
                for (int k = start; k < end; k++)
                {
                    var e = matched[k];
                    var isSelected = string.Equals(e.Key, _selectedKey, StringComparison.OrdinalIgnoreCase);
                    var isBatchSelected = _selectedKeys.Contains(e.Key);
                    var status = e.Status;
                    var label = GetEntryLabel(e);

                    // 批量选择模式下使用不同的背景色
                    var boxStyle = isSelected ? EditorStyles.helpBox : "box";
                    if (_batchSelectMode && isBatchSelected)
                    {
                        boxStyle = "box";
                        GUI.backgroundColor = new Color(0.8f, 0.9f, 1f, 1f); // 浅蓝色背景
                    }

                    using (new EditorGUILayout.HorizontalScope(boxStyle))
                    {
                        if (_batchSelectMode)
                        {
                            // 批量选择复选框
                            var newSelected = GUILayout.Toggle(isBatchSelected, "", GUILayout.Width(22));
                            if (newSelected != isBatchSelected)
                            {
                                if (newSelected)
                                    _selectedKeys.Add(e.Key);
                                else
                                    _selectedKeys.Remove(e.Key);
                            }
                        }
                        else
                        {
                            // 正常选择按钮
                            if (GUILayout.Button(isSelected ? "▶" : "", GUILayout.Width(22)))
                            {
                                SelectEntry(e);
                            }
                        }

                        var statusTag = FormatStatusTag(status);
                        if (!string.IsNullOrEmpty(statusTag))
                        {
                            GUILayout.Label(statusTag, GUILayout.Width(90));
                        }

                        if (GUILayout.Button(label, EditorStyles.label, GUILayout.ExpandWidth(true)))
                        {
                            if (!_batchSelectMode)
                            {
                                SelectEntry(e);
                            }
                            else
                            {
                                // 批量选择模式下点击标签切换选择状态
                                if (_selectedKeys.Contains(e.Key))
                                    _selectedKeys.Remove(e.Key);
                                else
                                    _selectedKeys.Add(e.Key);
                            }
                        }

                        if (GUILayout.Button("Ping", GUILayout.Width(50)))
                        {
                            EditorGUIUtility.PingObject(_targetAsset);
                            Selection.activeObject = _targetAsset;
                            if (!_batchSelectMode)
                                SelectEntry(e);
                        }
                    }
                    
                    // 重置背景色
                    if (_batchSelectMode && isBatchSelected)
                    {
                        GUI.backgroundColor = Color.white;
                    }
                }
                EditorGUILayout.EndScrollView();

                _pager.DrawGUI(matched.Count, () => { _listScroll = Vector2.zero; Repaint(); });

                if (matched.Count == 0)
                {
                    EditorGUILayout.HelpBox("No results.", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.LabelField($"Matched: {matched.Count}", EditorStyles.miniLabel);
                }
            }
        }

        private void DrawDetailPanel()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                EditorGUILayout.LabelField("Details", EditorStyles.boldLabel);

                EnsureCompareBuilt();

                if (string.IsNullOrWhiteSpace(_selectedKey))
                {
                    EditorGUILayout.HelpBox("Select an item from the list.", MessageType.Info);
                    return;
                }

                var pk = _selectedKey;
                var status = GetStatusByKey(pk);
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    EditorGUILayout.LabelField($"Key: {pk}");
                    EditorGUILayout.LabelField($"Status: {status}");
                }

                DrawDiffPanel(pk);

                if (_selectedLocalIndex < 0)
                {
                    EditorGUILayout.HelpBox("This row does not exist in Local(SO) list. Detail editor is unavailable.", MessageType.Info);
                    return;
                }

                if (_binding.ListProperty == null || _selectedLocalIndex >= _binding.ListProperty.arraySize)
                {
                    EditorGUILayout.HelpBox("Local index out of range.", MessageType.Warning);
                    return;
                }

                var elementProp = _binding.ListProperty.GetArrayElementAtIndex(_selectedLocalIndex);
                if (elementProp == null)
                {
                    EditorGUILayout.HelpBox("Element property is null.", MessageType.Warning);
                    return;
                }

                Undo.RecordObject(_binding.TargetAsset, "Modify ScriptableObject Data");

                _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);
                EditorGUILayout.PropertyField(elementProp, true);
                EditorGUILayout.EndScrollView();

                if (GUI.changed)
                {
                    EditorUtility.SetDirty(_binding.TargetAsset);
                    _compareDirty = true;
                }
            }
        }

        private void DrawDiffPanel(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            if (_compareHeaders == null || _compareHeaders.Count == 0)
            {
                return;
            }

            List<string> baseRow = null;
            List<string> localRow = null;
            List<string> remoteRow = null;
            _baselineRowMap?.TryGetValue(key, out baseRow);
            _localRowMap?.TryGetValue(key, out localRow);
            _remoteRowMap?.TryGetValue(key, out remoteRow);

            var diffs = new List<(string col, string b, string l, string r, bool isConflict)>();
            for (int i = 0; i < _compareHeaders.Count; i++)
            {
                var col = _compareHeaders[i];
                var b = GetCell(baseRow, i);
                var l = GetCell(localRow, i);
                var r = GetCell(remoteRow, i);

                var localChanged = !string.Equals(b, l, StringComparison.Ordinal);
                var remoteChanged = !string.Equals(b, r, StringComparison.Ordinal);
                var isConflict = localChanged && remoteChanged && !string.Equals(l, r, StringComparison.Ordinal);
                
                if (localChanged || remoteChanged)
                {
                    diffs.Add((col, b, l, r, isConflict));
                }
            }

            if (diffs.Count == 0)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField($"Diffs ({diffs.Count})", EditorStyles.boldLabel);
                for (int i = 0; i < diffs.Count; i++)
                {
                    var d = diffs[i];
                    
                    // 对于冲突字段，使用红色背景突出显示
                    if (d.isConflict)
                    {
                        using (new EditorGUILayout.VerticalScope("box"))
                        {
                            var originalColor = GUI.backgroundColor;
                            GUI.backgroundColor = new Color(1f, 0.8f, 0.8f, 1f); // 浅红色背景
                            
                            EditorGUILayout.LabelField(d.col, EditorStyles.miniBoldLabel);
                            GUI.backgroundColor = originalColor;
                            
                            // 使用红色文本标记冲突
                            var originalTextColor = GUI.color;
                            GUI.color = Color.red;
                            EditorGUILayout.LabelField("⚠ CONFLICT");
                            GUI.color = originalTextColor;
                            
                            EditorGUILayout.LabelField($"B: {d.b}");
                            EditorGUILayout.LabelField($"L: {d.l}");
                            EditorGUILayout.LabelField($"R: {d.r}");
                        }
                    }
                    else
                    {
                        EditorGUILayout.LabelField(d.col, EditorStyles.miniBoldLabel);
                        EditorGUILayout.LabelField($"B: {d.b}");
                        EditorGUILayout.LabelField($"L: {d.l}");
                        EditorGUILayout.LabelField($"R: {d.r}");
                    }
                    
                    EditorGUILayout.Space(4);
                }
            }
        }

        private List<RowEntry> CollectMatchedEntries()
        {
            var results = new List<RowEntry>();
            if (_allEntries == null || _allEntries.Count == 0)
            {
                return results;
            }

            for (int i = 0; i < _allEntries.Count; i++)
            {
                var e = _allEntries[i];
                if (PassEntry(e))
                {
                    results.Add(e);
                }
            }

            return results;
        }

        private bool PassEntry(RowEntry entry)
        {
            if (entry == null)
            {
                return false;
            }

            // 关键字过滤
            if (!string.IsNullOrWhiteSpace(_keyword))
            {
                var k = _keyword.Trim();
                var keywordMatch = false;
                
                if (!string.IsNullOrEmpty(entry.Key) && entry.Key.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    keywordMatch = true;
                }
                
                // 如果有本地项，使用现有的关键字逻辑
                if (!keywordMatch && entry.LocalIndex >= 0)
                {
                    keywordMatch = Pass(entry.LocalIndex);
                }
                
                if (!keywordMatch)
                {
                    return false;
                }
            }

            // 状态过滤
            if (HasActiveStatusFilters())
            {
                if (!PassStatusFilter(entry.Status))
                {
                    return false;
                }
            }

            return true;
        }
        
        private bool HasActiveStatusFilters()
        {
            return _filterConflicts || _filterLocalModified || _filterRemoteModified || _filterNew || _filterDeleted;
        }
        
        private bool PassStatusFilter(RowCompareStatus status)
        {
            if (!HasActiveStatusFilters())
            {
                return true;
            }
            
            switch (status)
            {
                case RowCompareStatus.Conflict:
                    return _filterConflicts;
                case RowCompareStatus.LocalModified:
                    return _filterLocalModified;
                case RowCompareStatus.RemoteModified:
                    return _filterRemoteModified;
                case RowCompareStatus.LocalNew:
                case RowCompareStatus.RemoteNew:
                    return _filterNew;
                case RowCompareStatus.LocalDeleted:
                case RowCompareStatus.RemoteDeleted:
                    return _filterDeleted;
                default:
                    // 对于 Unchanged 和 Unknown，只有在没有激活过滤器时才显示
                    return !HasActiveStatusFilters();
            }
        }

        private void EnsureCompareBuilt()
        {
            if (!_compareDirty)
            {
                return;
            }

            _compareDirty = false;
            _remoteLoadError = null;

            _baselineAsset = null;
            _compareHeaders = null;
            _baselineRowMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            _remoteRowMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            _localRowMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            _localIndexByKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _statusByKey = new Dictionary<string, RowCompareStatus>(StringComparer.OrdinalIgnoreCase);
            _allEntries = new List<RowEntry>();

            if (_targetAsset == null)
            {
                return;
            }

            if (TryLoadBaseline(_targetAsset, out var baseline) && baseline != null)
            {
                _baselineAsset = baseline;
                try
                {
                    _baselineRowMap = baseline.BuildRowMap();
                }
                catch
                {
                    _baselineRowMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                }

                _compareHeaders = baseline.Headers != null && baseline.Headers.Count > 0 ? baseline.Headers : null;
            }

            if (_compareHeaders == null)
            {
                // fallback to current bindings when no baseline headers.
                _compareHeaders = new List<string>();
            }

            BuildLocalRowMap();
            BuildRemoteRowMap();
            BuildStatuses();
            BuildEntries();
        }

        private void BuildLocalRowMap()
        {
            if (_binding == null || _binding.ElementType == null || _binding.ListProperty == null)
            {
                return;
            }

            // If baseline provides headers, reuse them for stable mapping.
            var headers = _compareHeaders != null && _compareHeaders.Count > 0 ? _compareHeaders : new List<string>();
            if (headers.Count == 0)
            {
                // Build minimal header list from element type.
                var dummy = new List<string>();
                foreach (var f in _binding.ElementType.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    var attr = f.GetCustomAttribute<ExcelColumnAttribute>();
                    if (attr != null && attr.Ignore) continue;
                    dummy.Add(attr?.Name ?? f.Name);
                }
                headers = dummy;
                _compareHeaders = dummy;
            }

            var bindings = ExcelReflectionMapper.BuildBindings(_binding.ElementType, headers);

            var n = _binding.ListProperty.arraySize;
            for (int i = 0; i < n; i++)
            {
                var item = GetItemObject(i);
                if (item == null) continue;

                var key = GetPrimaryKeyForItem(item, bindings, headers);
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (_localRowMap.ContainsKey(key))
                {
                    continue;
                }

                _localIndexByKey[key] = i;

                var values = new List<string>(headers.Count);
                for (int h = 0; h < headers.Count; h++)
                {
                    var col = headers[h];
                    var b = bindings.FirstOrDefault(x => x.ColumnIndex == h);
                    object v = null;
                    if (b != null)
                    {
                        v = ExcelReflectionMapper.GetValue(item, b.Member);
                    }
                    var s = ExcelReflectionMapper.FormatCellValue(v, col, ExcelCodecRegistry.Default);
                    values.Add(s != null ? s.ToString() : string.Empty);
                }
                _localRowMap.Add(key, values);
            }
        }

        private void BuildEntries()
        {
            _allEntries.Clear();

            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (_baselineRowMap != null)
            {
                foreach (var k in _baselineRowMap.Keys) keys.Add(k);
            }
            if (_localRowMap != null)
            {
                foreach (var k in _localRowMap.Keys) keys.Add(k);
            }
            if (_remoteRowMap != null)
            {
                foreach (var k in _remoteRowMap.Keys) keys.Add(k);
            }

            foreach (var k in keys)
            {
                _allEntries.Add(new RowEntry
                {
                    Key = k,
                    LocalIndex = _localIndexByKey != null && _localIndexByKey.TryGetValue(k, out var idx) ? idx : -1,
                    Status = GetStatusByKey(k),
                });
            }

            _allEntries.Sort((a, b) =>
            {
                if (a == null && b == null) return 0;
                if (a == null) return 1;
                if (b == null) return -1;

                // Conflicts first.
                var pa = GetStatusPriority(a.Status);
                var pb = GetStatusPriority(b.Status);
                var cmp = pa.CompareTo(pb);
                if (cmp != 0) return cmp;
                return string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase);
            });
        }

        private static int GetStatusPriority(RowCompareStatus status)
        {
            switch (status)
            {
                case RowCompareStatus.Conflict: return 0;
                case RowCompareStatus.LocalModified: return 1;
                case RowCompareStatus.RemoteModified: return 2;
                case RowCompareStatus.LocalNew: return 3;
                case RowCompareStatus.RemoteNew: return 4;
                case RowCompareStatus.LocalDeleted: return 5;
                case RowCompareStatus.RemoteDeleted: return 6;
                case RowCompareStatus.Unchanged: return 7;
                default: return 8;
            }
        }

        private string GetEntryLabel(RowEntry entry)
        {
            if (entry == null)
            {
                return string.Empty;
            }

            if (entry.LocalIndex >= 0)
            {
                return GetItemLabel(entry.LocalIndex);
            }

            return $"[virtual] {entry.Key}";
        }

        private void SelectEntry(RowEntry entry)
        {
            if (entry == null)
            {
                _selectedKey = null;
                _selectedLocalIndex = -1;
                return;
            }

            _selectedKey = entry.Key;
            _selectedLocalIndex = entry.LocalIndex;
        }

        private void BuildRemoteRowMap()
        {
            if (string.IsNullOrWhiteSpace(_excelAbsolutePath))
            {
                _remoteLoadError = "Remote: Excel path is empty.";
                return;
            }

            if (!File.Exists(_excelAbsolutePath))
            {
                _remoteLoadError = $"Remote: Excel file not found: {_excelAbsolutePath}";
                return;
            }

            var options = new ExcelTableOptions
            {
                SheetName = _sheetName,
                HeaderRowIndex = _headerRowIndex,
                DataStartRowIndex = _dataStartRowIndex,
                PrimaryKeyColumnName = _primaryKeyColumnName
            };

            try
            {
                using var reader = _backend.CreateReader(_excelAbsolutePath, options);
                var headers = reader.GetHeaders();
                if (headers != null && headers.Count > 0)
                {
                    // Prefer baseline headers if present, otherwise use remote headers.
                    if (_compareHeaders == null || _compareHeaders.Count == 0)
                    {
                        _compareHeaders = new List<string>(headers);
                    }
                }

                var pkIndex = -1;
                for (int i = 0; i < headers.Count; i++)
                {
                    if (string.Equals(headers[i]?.Trim(), options.PrimaryKeyColumnName?.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        pkIndex = i;
                        break;
                    }
                }

                var excelRowIndex = options.DataStartRowIndex;
                foreach (var row in reader.ReadRows(options.DataStartRowIndex))
                {
                    if (row == null || row.Count == 0)
                    {
                        excelRowIndex++;
                        continue;
                    }

                    if (pkIndex < 0 || pkIndex >= row.Count)
                    {
                        excelRowIndex++;
                        continue;
                    }

                    var pkCell = row[pkIndex];
                    var pkName = pkIndex >= 0 && pkIndex < headers.Count ? headers[pkIndex] : null;
                    var pkStrObj = ExcelReflectionMapper.FormatCellValue(pkCell, pkName, ExcelCodecRegistry.Default);
                    var key = pkStrObj != null ? pkStrObj.ToString() : string.Empty;
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        excelRowIndex++;
                        continue;
                    }

                    if (_remoteRowMap.ContainsKey(key))
                    {
                        excelRowIndex++;
                        continue;
                    }

                    var values = new List<string>(headers.Count);
                    for (int c = 0; c < headers.Count; c++)
                    {
                        var colName = headers[c];
                        var cell = c < row.Count ? row[c] : null;
                        var sObj = ExcelReflectionMapper.FormatCellValue(cell, colName, ExcelCodecRegistry.Default);
                        values.Add(sObj != null ? sObj.ToString() : string.Empty);
                    }
                    _remoteRowMap.Add(key, values);

                    excelRowIndex++;
                }
            }
            catch (Exception ex)
            {
                _remoteLoadError = $"Remote: Failed to read excel. {ex.GetType().Name}: {ex.Message}";
            }
        }

        private void BuildStatuses()
        {
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (_baselineRowMap != null)
            {
                foreach (var k in _baselineRowMap.Keys) keys.Add(k);
            }
            if (_localRowMap != null)
            {
                foreach (var k in _localRowMap.Keys) keys.Add(k);
            }
            if (_remoteRowMap != null)
            {
                foreach (var k in _remoteRowMap.Keys) keys.Add(k);
            }

            foreach (var k in keys)
            {
                var hasB = _baselineRowMap != null && _baselineRowMap.ContainsKey(k);
                var hasL = _localRowMap != null && _localRowMap.ContainsKey(k);
                var hasR = _remoteRowMap != null && _remoteRowMap.ContainsKey(k);

                if (!hasB)
                {
                    if (hasL && hasR)
                    {
                        _statusByKey[k] = RowCompareStatus.LocalNew;
                    }
                    else if (hasL)
                    {
                        _statusByKey[k] = RowCompareStatus.LocalNew;
                    }
                    else if (hasR)
                    {
                        _statusByKey[k] = RowCompareStatus.RemoteNew;
                    }
                    continue;
                }

                if (!hasL)
                {
                    _statusByKey[k] = hasR ? RowCompareStatus.LocalDeleted : RowCompareStatus.LocalDeleted;
                    continue;
                }

                if (!hasR)
                {
                    _statusByKey[k] = RowCompareStatus.RemoteDeleted;
                    continue;
                }

                var bRow = _baselineRowMap[k];
                var lRow = _localRowMap[k];
                var rRow = _remoteRowMap[k];

                var localChanged = !AreRowsEqual(bRow, lRow);
                var remoteChanged = !AreRowsEqual(bRow, rRow);

                if (!localChanged && !remoteChanged)
                {
                    _statusByKey[k] = RowCompareStatus.Unchanged;
                }
                else if (localChanged && !remoteChanged)
                {
                    _statusByKey[k] = RowCompareStatus.LocalModified;
                }
                else if (!localChanged && remoteChanged)
                {
                    _statusByKey[k] = RowCompareStatus.RemoteModified;
                }
                else
                {
                    // both changed: conflict if local != remote
                    _statusByKey[k] = AreRowsEqual(lRow, rRow) ? RowCompareStatus.LocalModified : RowCompareStatus.Conflict;
                }
            }
        }

        private static bool AreRowsEqual(IReadOnlyList<string> a, IReadOnlyList<string> b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (!string.Equals(a[i] ?? string.Empty, b[i] ?? string.Empty, StringComparison.Ordinal))
                {
                    return false;
                }
            }
            return true;
        }

        private static string GetCell(IReadOnlyList<string> row, int index)
        {
            if (row == null || index < 0 || index >= row.Count)
            {
                return string.Empty;
            }
            return row[index] ?? string.Empty;
        }

        private string GetPrimaryKeyForIndex(int index)
        {
            var item = GetItemObject(index);
            if (item == null)
            {
                return string.Empty;
            }

            if (_compareHeaders == null || _compareHeaders.Count == 0)
            {
                return string.Empty;
            }

            var bindings = ExcelReflectionMapper.BuildBindings(_binding.ElementType, _compareHeaders);
            return GetPrimaryKeyForItem(item, bindings, _compareHeaders);
        }

        private string GetPrimaryKeyForItem(object item, IReadOnlyList<ExcelReflectionMapper.ColumnBinding> bindings, IReadOnlyList<string> headers)
        {
            if (item == null || headers == null || headers.Count == 0)
            {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(_primaryKeyColumnName))
            {
                return string.Empty;
            }

            ExcelReflectionMapper.ColumnBinding pkBinding = null;
            for (int i = 0; i < bindings.Count; i++)
            {
                if (string.Equals(bindings[i].ColumnName, _primaryKeyColumnName, StringComparison.OrdinalIgnoreCase))
                {
                    pkBinding = bindings[i];
                    break;
                }
            }

            if (pkBinding == null)
            {
                return string.Empty;
            }

            var v = ExcelReflectionMapper.GetValue(item, pkBinding.Member);
            var sObj = ExcelReflectionMapper.FormatCellValue(v, pkBinding.ColumnName, ExcelCodecRegistry.Default);
            return sObj != null ? sObj.ToString() : string.Empty;
        }

        private RowCompareStatus GetStatusByKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || _statusByKey == null)
            {
                return RowCompareStatus.Unknown;
            }

            return _statusByKey.TryGetValue(key, out var s) ? s : RowCompareStatus.Unknown;
        }

        private static string FormatStatusTag(RowCompareStatus status)
        {
            switch (status)
            {
                case RowCompareStatus.Unchanged: return "=BASE";
                case RowCompareStatus.LocalModified: return "LOCAL";
                case RowCompareStatus.RemoteModified: return "REMOTE";
                case RowCompareStatus.Conflict: return "CONFLICT";
                case RowCompareStatus.LocalNew: return "LOCAL+";
                case RowCompareStatus.RemoteNew: return "REMOTE+";
                case RowCompareStatus.LocalDeleted: return "LOCAL-";
                case RowCompareStatus.RemoteDeleted: return "REMOTE-";
                default: return string.Empty;
            }
        }

        private bool Pass(int index)
        {
            if (string.IsNullOrWhiteSpace(_keyword))
            {
                return true;
            }

            var itemObj = GetItemObject(index);
            if (itemObj == null)
            {
                return false;
            }

            var k = _keyword.Trim();

            if (_getName != null)
            {
                var s = _getName(itemObj);
                if (!string.IsNullOrEmpty(s) && s.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            if (_getDesc != null)
            {
                var s = _getDesc(itemObj);
                if (!string.IsNullOrEmpty(s) && s.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            var fallback = itemObj.ToString();
            return !string.IsNullOrEmpty(fallback) && fallback.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private string GetItemLabel(int index)
        {
            var obj = GetItemObject(index);
            if (obj == null)
            {
                return $"[{index}] (null)";
            }

            var name = _getName != null ? _getName(obj) : null;
            if (!string.IsNullOrEmpty(name))
            {
                return $"[{index}] {name}";
            }

            return $"[{index}] {obj}";
        }

        private object GetItemObject(int index)
        {
            if (_binding.TargetAsset == null || string.IsNullOrEmpty(_binding.ListPropertyName) || index < 0)
            {
                return null;
            }

            var t = _binding.TargetAsset.GetType();
            var f = t.GetField(_binding.ListPropertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f == null)
            {
                return null;
            }

            object v;
            try
            {
                v = f.GetValue(_binding.TargetAsset);
            }
            catch
            {
                return null;
            }

            if (v is System.Collections.IList list)
            {
                return index < list.Count ? list[index] : null;
            }

            if (v is Array arr)
            {
                return index < arr.Length ? arr.GetValue(index) : null;
            }

            return null;
        }

        private static Func<object, string> BuildStringGetter(Type elementType, string memberName)
        {
            if (elementType == null)
            {
                return null;
            }

            var field = elementType.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null && field.FieldType == typeof(string))
            {
                return o => (string)field.GetValue(o);
            }

            var prop = elementType.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null && prop.PropertyType == typeof(string))
            {
                return o => (string)prop.GetValue(o);
            }

            return null;
        }
        
        // 批量操作方法
        private void BatchSelectAll()
        {
            _selectedKeys.Clear();
            EnsureCompareBuilt();
            
            var matched = CollectMatchedEntries();
            foreach (var entry in matched)
            {
                _selectedKeys.Add(entry.Key);
            }
        }
        
        private void BatchDeleteSelected()
        {
            if (_selectedKeys.Count == 0 || _binding.ListProperty == null)
                return;
                
            Undo.RecordObject(_binding.TargetAsset, "Batch Delete Items");
            
            // 按索引从大到小删除，避免索引偏移问题
            var indicesToDelete = new List<int>();
            foreach (var key in _selectedKeys)
            {
                if (_localIndexByKey.TryGetValue(key, out var index) && index >= 0)
                {
                    indicesToDelete.Add(index);
                }
            }
            
            indicesToDelete.Sort((a, b) => b.CompareTo(a));
            
            foreach (var index in indicesToDelete)
            {
                if (index < _binding.ListProperty.arraySize)
                {
                    _binding.ListProperty.DeleteArrayElementAtIndex(index);
                }
            }
            
            _selectedKeys.Clear();
            EditorUtility.SetDirty(_binding.TargetAsset);
            _compareDirty = true;
            BindTarget(_targetAsset);
        }
        
        private void BatchEditField()
        {
            if (_selectedKeys.Count == 0 || string.IsNullOrWhiteSpace(_batchEditFieldName) || _binding.ListProperty == null)
                return;
                
            Undo.RecordObject(_binding.TargetAsset, "Batch Edit Field");
            
            var fieldName = _batchEditFieldName.Trim();
            var newValue = _batchEditValue;
            var modifiedCount = 0;
            
            foreach (var key in _selectedKeys)
            {
                if (_localIndexByKey.TryGetValue(key, out var index) && index >= 0 && index < _binding.ListProperty.arraySize)
                {
                    var itemProp = _binding.ListProperty.GetArrayElementAtIndex(index);
                    if (itemProp != null)
                    {
                        var fieldProp = itemProp.FindPropertyRelative(fieldName);
                        if (fieldProp != null)
                        {
                            // 根据属性类型设置值
                            switch (fieldProp.propertyType)
                            {
                                case SerializedPropertyType.String:
                                    fieldProp.stringValue = newValue;
                                    break;
                                case SerializedPropertyType.Integer:
                                    if (int.TryParse(newValue, out var intValue))
                                        fieldProp.intValue = intValue;
                                    break;
                                case SerializedPropertyType.Float:
                                    if (float.TryParse(newValue, out var floatValue))
                                        fieldProp.floatValue = floatValue;
                                    break;
                                case SerializedPropertyType.Boolean:
                                    if (bool.TryParse(newValue, out var boolValue))
                                        fieldProp.boolValue = boolValue;
                                    break;
                                default:
                                    // 对于其他类型，尝试字符串转换
                                    fieldProp.stringValue = newValue;
                                    break;
                            }
                            modifiedCount++;
                        }
                    }
                }
            }
            
            if (modifiedCount > 0)
            {
                EditorUtility.SetDirty(_binding.TargetAsset);
                _compareDirty = true;
                Debug.Log($"批量编辑完成：修改了 {modifiedCount} 个项目的 '{fieldName}' 字段");
            }
            else
            {
                Debug.LogWarning($"批量编辑失败：未找到字段 '{fieldName}' 或没有可修改的项目");
            }
        }
    }
}
