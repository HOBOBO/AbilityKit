using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.ExcelSync.Editor
{
    /// <summary>
    /// Excel 同步模板管理器窗口
    /// </summary>
    public class ExcelSyncTemplateManagerWindow : EditorWindow
    {
        private ExcelSyncProjectConfig _config;
        private Vector2 _scrollPosition;
        private bool _showGlobalSettings = true;
        private bool _showTemplateList = true;

        // 过滤选项
        private string _searchFilter = "";
        private bool _showEnabledOnly = false;
        private bool _showDisabledOnly = false;
        private bool _showWithoutAssetOnly = false;
        private bool _showWithoutBaselineOnly = false;

        // 批量选择
        private System.Collections.Generic.HashSet<int> _selectedIndices = new();

        [MenuItem("Tools/Excel/模板管理器")]
        public static void Open()
        {
            var w = GetWindow<ExcelSyncTemplateManagerWindow>();
            w.titleContent = new GUIContent("Excel 模板管理器");
            w.minSize = new Vector2(800, 600);
            w.Show();
        }

        public static void OpenWithConfig(ExcelSyncProjectConfig config)
        {
            var w = GetWindow<ExcelSyncTemplateManagerWindow>();
            w.titleContent = new GUIContent("Excel 模板管理器");
            w.minSize = new Vector2(800, 600);
            w._config = config;
            w.Show();
        }

        private void OnEnable()
        {
            if (_config == null)
            {
                _config = ExcelSyncProjectConfig.Instance;
            }
        }

        private void OnGUI()
        {
            DrawHeader();
            EditorGUILayout.Space(5);

            if (_config == null)
            {
                DrawNoConfigGUI();
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawGlobalSettings();
            EditorGUILayout.Space(5);

            DrawToolbar();
            EditorGUILayout.Space(5);

            DrawTemplateList();

            EditorGUILayout.EndScrollView();

            DrawFooter();
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUILayout.LabelField("Excel 同步模板管理器", EditorStyles.boldLabel);

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("刷新", GUILayout.Width(80)))
                {
                    _config = ExcelSyncProjectConfig.Instance;
                    _selectedIndices.Clear();
                }

                if (GUILayout.Button("打开配置", GUILayout.Width(80)))
                {
                    Selection.activeObject = _config;
                }
            }
        }

        private void DrawNoConfigGUI()
        {
            EditorGUILayout.HelpBox(
                "未找到 ExcelSyncProjectConfig。\n\n请在 Resources 目录下创建配置资源：\nAssets/Resources/ExcelSyncProjectConfig.asset\n\n或点击下方按钮创建：",
                MessageType.Warning);

            EditorGUILayout.Space(10);

            if (GUILayout.Button("创建默认配置", GUILayout.Height(30)))
            {
                CreateDefaultConfig();
            }
        }

        private void CreateDefaultConfig()
        {
            var folder = "Assets/Resources";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            var config = CreateInstance<ExcelSyncProjectConfig>();
            var path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/ExcelSyncProjectConfig.asset");
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();

            _config = config;
            Selection.activeObject = config;

            Debug.Log($"[ExcelSync] 创建配置: {path}");
        }

        private void DrawGlobalSettings()
        {
            _showGlobalSettings = EditorGUILayout.Foldout(_showGlobalSettings, "全局配置", true);

            if (!_showGlobalSettings)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("ExcelRootPath"), true);
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("DefaultCodeOutputFolder"), true);
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("DefaultAssetOutputFolder"), true);
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("DefaultNamespace"), true);

                    EditorGUILayout.Space(5);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("默认表选项", EditorStyles.boldLabel);
                    }

                    EditorGUILayout.PropertyField(serializedObject.FindProperty("DefaultHeaderRowIndex"), true);
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("DefaultDataStartRowIndex"), true);
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("DefaultPrimaryKeyColumnName"), true);
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("DefaultSheetName"), true);

                    if (GUI.changed)
                    {
                        serializedObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(_config);
                    }
                }
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                // 搜索和过滤
                using (new EditorGUILayout.HorizontalScope())
                {
                    _searchFilter = EditorGUILayout.TextField("搜索", _searchFilter, "SearchTextField");
                    if (GUILayout.Button("", "SearchCancelButton", GUILayout.Width(20)))
                    {
                        _searchFilter = "";
                    }
                }

                EditorGUILayout.Space(3);

                // 过滤选项
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("过滤:", GUILayout.Width(40));

                    var prevEnabled = _showEnabledOnly;
                    var prevDisabled = _showDisabledOnly;
                    var prevNoAsset = _showWithoutAssetOnly;
                    var prevNoBaseline = _showWithoutBaselineOnly;

                    _showEnabledOnly = GUILayout.Toggle(_showEnabledOnly, "仅启用", "Button", GUILayout.Width(60));
                    _showDisabledOnly = GUILayout.Toggle(_showDisabledOnly, "仅禁用", "Button", GUILayout.Width(60));
                    _showWithoutAssetOnly = GUILayout.Toggle(_showWithoutAssetOnly, "无Asset", "Button", GUILayout.Width(60));
                    _showWithoutBaselineOnly = GUILayout.Toggle(_showWithoutBaselineOnly, "无基线", "Button", GUILayout.Width(60));

                    // 互斥
                    if (_showEnabledOnly && prevDisabled) _showDisabledOnly = false;
                    if (_showDisabledOnly && prevEnabled) _showEnabledOnly = false;

                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("清除过滤", GUILayout.Width(70)))
                    {
                        _searchFilter = "";
                        _showEnabledOnly = false;
                        _showDisabledOnly = false;
                        _showWithoutAssetOnly = false;
                        _showWithoutBaselineOnly = false;
                    }
                }

                EditorGUILayout.Space(5);

                // 操作按钮
                using (new EditorGUILayout.HorizontalScope())
                {
                    // 选择操作
                    EditorGUILayout.LabelField("选择:", GUILayout.Width(40));

                    if (GUILayout.Button("全选", GUILayout.Width(50)))
                    {
                        SelectAll(true);
                    }

                    if (GUILayout.Button("取消", GUILayout.Width(50)))
                    {
                        SelectAll(false);
                    }

                    if (GUILayout.Button("反转", GUILayout.Width(50)))
                    {
                        InvertSelection();
                    }

                    GUILayout.FlexibleSpace();

                    EditorGUILayout.LabelField($"已选: {_selectedIndices.Count}", GUILayout.Width(60));
                }

                EditorGUILayout.Space(5);

                // 批量操作
                using (new EditorGUILayout.HorizontalScope())
                {
                    // 扫描
                    if (GUILayout.Button("扫描 Excel", GUILayout.Width(100)))
                    {
                        ScanExcelFiles();
                    }

                    // 生成
                    GUI.enabled = _selectedIndices.Count > 0;
                    if (GUILayout.Button("生成代码", GUILayout.Width(80)))
                    {
                        GenerateSelectedCode();
                    }
                    GUI.enabled = true;
                }

                EditorGUILayout.Space(3);

                using (new EditorGUILayout.HorizontalScope())
                {
                    // 同步
                    GUI.enabled = _selectedIndices.Count > 0;

                    var importColor = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
                    if (GUILayout.Button("导入", GUILayout.Width(60)))
                    {
                        ImportSelected();
                    }
                    GUI.backgroundColor = importColor;

                    var exportColor = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(1f, 0.8f, 0.7f);
                    if (GUILayout.Button("导出", GUILayout.Width(60)))
                    {
                        ExportSelected();
                    }
                    GUI.backgroundColor = exportColor;

                    GUI.enabled = true;

                    GUILayout.FlexibleSpace();

                    // 一键
                    GUI.enabled = _selectedIndices.Count > 0;
                    var oneClickColor = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.7f, 0.7f, 1f);
                    if (GUILayout.Button("一键生成+导入", GUILayout.Width(100)))
                    {
                        OneClickGenerateAndImport();
                    }
                    GUI.backgroundColor = oneClickColor;
                    GUI.enabled = true;
                }
            }
        }

        private SerializedObject serializedObject
        {
            get
            {
                if (_serializedObject == null || _serializedObject.targetObject != _config)
                {
                    _serializedObject = _config != null ? new SerializedObject(_config) : null;
                }
                return _serializedObject;
            }
        }

        private SerializedObject _serializedObject;

        private void DrawTemplateList()
        {
            if (_config == null || _config.TableTemplates == null)
            {
                return;
            }

            var templates = _config.TableTemplates;
            var filteredIndices = GetFilteredIndices();

            EditorGUILayout.LabelField($"配置表列表 ({filteredIndices.Count}/{templates.Count})", EditorStyles.boldLabel);

            _showTemplateList = EditorGUILayout.Foldout(_showTemplateList, "展开/折叠", true);

            if (!_showTemplateList)
            {
                return;
            }

            for (int i = 0; i < templates.Count; i++)
            {
                if (!filteredIndices.Contains(i))
                {
                    continue;
                }

                var template = templates[i];
                if (template == null)
                {
                    continue;
                }

                DrawTemplateItem(i, template);
            }
        }

        private void DrawTemplateItem(int index, ExcelSyncTableTemplate template)
        {
            var isSelected = _selectedIndices.Contains(index);
            var isEnabled = template.Enabled;
            var hasAsset = template.TableAsset != null;
            var baselineStatus = template.BaselineStatus;

            var bgColor = GetItemBackgroundColor(isSelected, isEnabled, hasAsset, baselineStatus);

            using (new EditorGUILayout.VerticalScope("box"))
            {
                using (new EditorGUI.DisabledGroupScope(true))
                {
                    EditorGUILayout.BeginHorizontal();

                    // 选择框
                    var newSelected = EditorGUILayout.Toggle(isSelected, GUILayout.Width(20));
                    if (newSelected != isSelected)
                    {
                        if (newSelected)
                        {
                            _selectedIndices.Add(index);
                        }
                        else
                        {
                            _selectedIndices.Remove(index);
                        }
                    }

                    // 启用状态
                    EditorGUILayout.Toggle(isEnabled, GUILayout.Width(20));

                    // 名称
                    EditorGUILayout.LabelField(template.GetDisplayName(), EditorStyles.boldLabel);

                    GUILayout.FlexibleSpace();

                    // 状态标签
                    DrawStatusLabels(template);

                    // 快速操作
                    if (GUILayout.Button("生成", GUILayout.Width(50)))
                    {
                        GenerateCode(index);
                    }

                    if (GUILayout.Button("导入", GUILayout.Width(50)))
                    {
                        Import(index);
                    }

                    EditorGUILayout.EndHorizontal();

                    // Excel 路径
                    EditorGUILayout.LabelField($"Excel: {template.ExcelRelativePath}", EditorStyles.miniLabel);

                    // Asset 路径
                    if (hasAsset)
                    {
                        EditorGUILayout.LabelField($"Asset: {template.TableAssetPath}", EditorStyles.miniLabel);
                    }
                    else
                    {
                        using (new EditorGUI.DisabledGroupScope(true))
                        {
                            EditorGUILayout.LabelField("Asset: (未绑定)", EditorStyles.miniLabel);
                        }
                    }
                }
            }
        }

        private void DrawStatusLabels(ExcelSyncTableTemplate template)
        {
            var hasAsset = template.TableAsset != null;
            var baselineStatus = template.BaselineStatus;

            // Asset 状态
            var assetColor = hasAsset ? Color.green : Color.gray;
            using (new EditorGUI.DisabledGroupScope(true))
            {
                var assetStyle = new GUIStyle(EditorStyles.miniButton)
                {
                    normal = { textColor = assetColor }
                };
                GUILayout.Button(hasAsset ? "Asset" : "无Asset", assetStyle, GUILayout.Width(50));
            }

            // Baseline 状态
            Color baselineColor;
            string baselineText;
            switch (baselineStatus)
            {
                case BaselineStatus.Exists:
                    baselineColor = Color.green;
                    baselineText = "基线";
                    break;
                case BaselineStatus.Missing:
                    baselineColor = Color.yellow;
                    baselineText = "无基线";
                    break;
                default:
                    baselineColor = Color.gray;
                    baselineText = "-";
                    break;
            }

            using (new EditorGUI.DisabledGroupScope(true))
            {
                var baselineStyle = new GUIStyle(EditorStyles.miniButton)
                {
                    normal = { textColor = baselineColor }
                };
                GUILayout.Button(baselineText, baselineStyle, GUILayout.Width(50));
            }
        }

        private Color GetItemBackgroundColor(bool isSelected, bool isEnabled, bool hasAsset, BaselineStatus baselineStatus)
        {
            if (isSelected)
            {
                return new Color(0.3f, 0.5f, 0.8f, 0.3f);
            }

            if (!isEnabled)
            {
                return new Color(0.5f, 0.5f, 0.5f, 0.2f);
            }

            if (baselineStatus == BaselineStatus.Missing)
            {
                return new Color(1f, 1f, 0f, 0.1f);
            }

            return Color.white;
        }

        private System.Collections.Generic.List<int> GetFilteredIndices()
        {
            var result = new System.Collections.Generic.List<int>();
            if (_config == null || _config.TableTemplates == null)
            {
                return result;
            }

            for (int i = 0; i < _config.TableTemplates.Count; i++)
            {
                var template = _config.TableTemplates[i];
                if (template == null) continue;

                // 搜索过滤
                if (!string.IsNullOrEmpty(_searchFilter))
                {
                    var searchLower = _searchFilter.ToLower();
                    var match = template.GetDisplayName().ToLower().Contains(searchLower) ||
                                template.ExcelRelativePath.ToLower().Contains(searchLower);
                    if (!match) continue;
                }

                // 启用状态过滤
                if (_showEnabledOnly && !template.Enabled) continue;
                if (_showDisabledOnly && template.Enabled) continue;

                // Asset 过滤
                if (_showWithoutAssetOnly && template.TableAsset != null) continue;

                // 基线过滤
                if (_showWithoutBaselineOnly && template.BaselineStatus == BaselineStatus.Exists) continue;

                result.Add(i);
            }

            return result;
        }

        private void SelectAll(bool selected)
        {
            var filteredIndices = GetFilteredIndices();
            if (selected)
            {
                foreach (var i in filteredIndices)
                {
                    _selectedIndices.Add(i);
                }
            }
            else
            {
                _selectedIndices.Clear();
            }
        }

        private void InvertSelection()
        {
            var filteredIndices = GetFilteredIndices();
            foreach (var i in filteredIndices)
            {
                if (_selectedIndices.Contains(i))
                {
                    _selectedIndices.Remove(i);
                }
                else
                {
                    _selectedIndices.Add(i);
                }
            }
        }

        private void DrawFooter()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUILayout.LabelField($"总计: {_config?.TableTemplates?.Count ?? 0} 个配置表", GUILayout.Width(150));

                GUILayout.FlexibleSpace();

                var enabled = _config?.TableTemplates?.Count(t => t != null && t.Enabled) ?? 0;
                var withAsset = _config?.TableTemplates?.Count(t => t != null && t.TableAsset != null) ?? 0;
                var withBaseline = _config?.TableTemplates?.Count(t => t != null && t.BaselineStatus == BaselineStatus.Exists) ?? 0;

                EditorGUILayout.LabelField($"启用: {enabled} | 有Asset: {withAsset} | 有基线: {withBaseline}", GUILayout.Width(200));
            }
        }

        #region 操作方法

        private void ScanExcelFiles()
        {
            if (_config == null)
            {
                return;
            }

            var result = ExcelSyncTemplateService.ScanAndCreateTemplates(_config);

            var message = $"扫描完成:\n";
            if (result.HasErrors)
            {
                message += $"- 扫描过程中有错误，请检查 Console\n";
            }
            if (result.CreatedCount > 0)
            {
                message += $"- 新建模板: {result.CreatedCount}\n";
            }
            if (result.ExistingCount > 0)
            {
                message += $"- 已存在: {result.ExistingCount}\n";
            }

            EditorUtility.DisplayDialog(result.HasErrors ? "扫描完成（有错误）" : "扫描完成", message, "确定");
        }

        private void GenerateSelectedCode()
        {
            if (_config == null || _selectedIndices.Count == 0)
            {
                return;
            }

            var templates = _config.TableTemplates;
            var successCount = 0;
            var errorCount = 0;

            foreach (var index in _selectedIndices)
            {
                if (index < 0 || index >= templates.Count) continue;

                var result = ExcelSyncTemplateService.GenerateCode(templates[index], _config);
                if (result.IsSuccess)
                {
                    successCount++;
                }
                else
                {
                    errorCount++;
                    Debug.LogError($"[ExcelSync] 生成失败: {templates[index].GetDisplayName()}: {result.Message}");
                }
            }

            var message = $"代码生成完成:\n成功: {successCount}\n失败: {errorCount}";
            EditorUtility.DisplayDialog("生成完成", message, "确定");
        }

        private void GenerateCode(int index)
        {
            if (_config == null || index < 0 || index >= _config.TableTemplates.Count)
            {
                return;
            }

            var result = ExcelSyncTemplateService.GenerateCode(_config.TableTemplates[index], _config);

            if (result.IsSuccess)
            {
                EditorUtility.DisplayDialog("生成成功", result.Message, "确定");
            }
            else
            {
                EditorUtility.DisplayDialog("生成失败", result.Message, "确定");
            }
        }

        private void ImportSelected()
        {
            if (_config == null || _selectedIndices.Count == 0)
            {
                return;
            }

            var templates = _config.TableTemplates;
            var successCount = 0;
            var errorCount = 0;

            foreach (var index in _selectedIndices)
            {
                if (index < 0 || index >= templates.Count) continue;

                var result = ExcelSyncTemplateService.Import(templates[index], _config);
                if (result.IsSuccess)
                {
                    successCount++;
                }
                else
                {
                    errorCount++;
                    Debug.LogError($"[ExcelSync] 导入失败: {templates[index].GetDisplayName()}: {result.Message}");
                }
            }

            var message = $"导入完成:\n成功: {successCount}\n失败: {errorCount}";
            EditorUtility.DisplayDialog("导入完成", message, "确定");
        }

        private void Import(int index)
        {
            if (_config == null || index < 0 || index >= _config.TableTemplates.Count)
            {
                return;
            }

            var result = ExcelSyncTemplateService.Import(_config.TableTemplates[index], _config);

            if (result.IsSuccess)
            {
                EditorUtility.DisplayDialog("导入成功", "导入完成", "确定");
            }
            else
            {
                EditorUtility.DisplayDialog("导入失败", result.Message, "确定");
            }
        }

        private void ExportSelected()
        {
            if (_config == null || _selectedIndices.Count == 0)
            {
                return;
            }

            var templates = _config.TableTemplates;
            var successCount = 0;
            var errorCount = 0;

            foreach (var index in _selectedIndices)
            {
                if (index < 0 || index >= templates.Count) continue;

                var result = ExcelSyncTemplateService.Export(templates[index], _config);
                if (result.IsSuccess)
                {
                    successCount++;
                }
                else
                {
                    errorCount++;
                    Debug.LogError($"[ExcelSync] 导出失败: {templates[index].GetDisplayName()}: {result.Message}");
                }
            }

            var message = $"导出完成:\n成功: {successCount}\n失败: {errorCount}";
            EditorUtility.DisplayDialog("导出完成", message, "确定");
        }

        private void OneClickGenerateAndImport()
        {
            if (_config == null || _selectedIndices.Count == 0)
            {
                return;
            }

            var templates = _config.TableTemplates;
            var successCount = 0;
            var errorCount = 0;

            foreach (var index in _selectedIndices)
            {
                if (index < 0 || index >= templates.Count) continue;

                var template = templates[index];

                // 1. 生成代码
                var codeResult = ExcelSyncTemplateService.GenerateCode(template, _config);
                if (!codeResult.IsSuccess)
                {
                    errorCount++;
                    Debug.LogError($"[ExcelSync] 代码生成失败: {template.GetDisplayName()}: {codeResult.Message}");
                    continue;
                }

                // 2. 创建/绑定 Asset
                var assetResult = ExcelSyncTemplateService.CreateOrBindTableAsset(template, _config);
                if (!assetResult.IsSuccess)
                {
                    errorCount++;
                    Debug.LogError($"[ExcelSync] Asset 创建失败: {template.GetDisplayName()}: {assetResult.Message}");
                    continue;
                }

                // 3. 导入
                var importResult = ExcelSyncTemplateService.Import(template, _config);
                if (importResult.IsSuccess)
                {
                    successCount++;
                }
                else
                {
                    errorCount++;
                    Debug.LogError($"[ExcelSync] 导入失败: {template.GetDisplayName()}: {importResult.Message}");
                }
            }

            // 刷新编译
            AssetDatabase.Refresh();

            var message = $"一键完成:\n成功: {successCount}\n失败: {errorCount}";
            EditorUtility.DisplayDialog("一键完成", message, "确定");
        }

        #endregion
    }
}
