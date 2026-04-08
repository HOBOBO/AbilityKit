/// <summary>
/// 文件名称: ConstraintSettingsWindow.cs
/// 
/// 功能描述: 包约束配置的编辑器可视化工具窗口。提供配置查看、编辑、验证和导入导出功能。
/// 通过菜单 Window > AbilityKit > Namespace Constraints 打开。
/// 
/// 创建日期: 2026-04-06
/// 修改日期: 2026-04-06
/// </summary>

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;

using UnityEngine;
using UnityEditor;

using Newtonsoft.Json;

using AbilityKit.Analyzer;
using AbilityKit.Analyzer.Configuration;

namespace AbilityKit.Analyzer.Editor
{

/// <summary>
/// 包约束配置的编辑器窗口。
/// </summary>
public sealed class ConstraintSettingsWindow : EditorWindow
{
    private const string MenuPath = "Window/AbilityKit/Namespace Constraints";
    private const string ConfigFileName = "PackageConstraints.json";

    private ConstraintLoader _loader;
    private PackageConstraintsConfig _config;
    private Vector2 _scrollPosition;
    private string _statusMessage = string.Empty;
    private Color _statusColor = Color.white;
    private bool _hasChanges;
    private string _currentConfigPath;

    // 编辑状态
    private string _editPackageName = string.Empty;
    private string _editNamespaces = string.Empty;
    private string _editAssemblies = string.Empty;
    private bool _editEnabled = true;
    private int _editSeverityIndex = 0;
    private string _editDescription = string.Empty;
    private bool _isNewConstraint = false;

    // 全局默认值编辑
    private bool _editGlobalEnabled = false;
    private string _editGlobalNamespaces = string.Empty;
    private string _editGlobalAssemblies = string.Empty;
    private int _editGlobalSeverityIndex = 0;

    private readonly string[] _severityLabels = new[]
    {
        "Error", "Warning", "Info", "Hidden"
    };

    [MenuItem(MenuPath)]
    private static void OpenWindow()
    {
        var window = GetWindow<ConstraintSettingsWindow>(title: "Namespace Constraints");
        window.minSize = new Vector2(700, 400);
        window.Show();
    }

    private void OnEnable()
    {
        _loader = new ConstraintLoader();
        _currentConfigPath = _loader.ConfigPath;
        LoadConfig();
    }

    private void OnGUI()
    {
        DrawToolbar();

        EditorGUILayout.Space(4);

        using (var scrollScope = new EditorGUILayout.ScrollViewScope(_scrollPosition))
        {
            _scrollPosition = scrollScope.scrollPosition;

            DrawGlobalDefaults();
            EditorGUILayout.Space(8);
            DrawConstraintsList();
            EditorGUILayout.Space(8);
            DrawEditPanel();
        }

        DrawStatusBar();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            LoadConfig();
            ShowStatus("Configuration reloaded.", Color.green);
        }

        if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            SaveConfig();
        }

        if (GUILayout.Button("Validate", EditorStyles.toolbarButton, GUILayout.Width(70)))
        {
            ValidateConfig();
        }

        GUILayout.FlexibleSpace();

        var path = _currentConfigPath ?? "(not found)";
        EditorGUILayout.LabelField($"Config: {path}", EditorStyles.miniLabel);

        if (GUILayout.Button("Open File", EditorStyles.toolbarButton, GUILayout.Width(80)))
        {
            OpenConfigFile();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawGlobalDefaults()
    {
        EditorGUILayout.LabelField("Global Defaults", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;

        _editGlobalEnabled = EditorGUILayout.Toggle(
            new GUIContent("Enabled", "对所有未单独配置的包启用全局默认约束"),
            _editGlobalEnabled);

        EditorGUILayout.LabelField("Forbidden Namespaces (comma-separated)", EditorStyles.miniLabel);
        _editGlobalNamespaces = EditorGUILayout.TextArea(_editGlobalNamespaces, GUILayout.Height(40));

        EditorGUILayout.LabelField("Forbidden Assemblies (comma-separated)", EditorStyles.miniLabel);
        _editGlobalAssemblies = EditorGUILayout.TextArea(_editGlobalAssemblies, GUILayout.Height(30));

        _editGlobalSeverityIndex = EditorGUILayout.Popup(
            new GUIContent("Severity"), _editGlobalSeverityIndex, _severityLabels);

        EditorGUI.indentLevel--;
    }

    private void DrawConstraintsList()
    {
        EditorGUILayout.LabelField("Package Constraints", EditorStyles.boldLabel);

        if (_config?.Constraints == null || _config.Constraints.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "No package constraints defined. Click 'Add Package' to create one.",
                MessageType.Info);
        }
        else
        {
            var keys = new List<string>(_config.Constraints.Keys);
            for (var i = 0; i < keys.Count; i++)
            {
                var key = keys[i];
                if (string.IsNullOrEmpty(key) || key.StartsWith("_"))
                    continue;

                DrawConstraintItem(key, _config.Constraints[key]);
            }
        }

        EditorGUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("+ Add Package Constraint", GUILayout.Width(180)))
        {
            ShowAddDialog();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawConstraintItem(string packageName, PackageConstraint constraint)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();

        var labelStyle = new GUIStyle(EditorStyles.boldLabel);
        labelStyle.normal.textColor = constraint.IsEnabled
            ? new Color(0.2f, 0.8f, 0.2f)
            : Color.gray;

        EditorGUILayout.LabelField($"[{(constraint.IsEnabled ? "ON" : "OFF")}] {packageName}", labelStyle);

        var severityColor = constraint.Severity switch
        {
            AKDiagnosticSeverity.Error => Color.red,
            AKDiagnosticSeverity.Warning => new Color(1f, 0.6f, 0f),
            AKDiagnosticSeverity.Info => Color.blue,
            _ => Color.gray
        };

        var severityLabel = new GUIContent(constraint.Severity.ToString());
        EditorGUILayout.LabelField(severityLabel, new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = severityColor }
        });

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Edit", EditorStyles.miniButton, GUILayout.Width(50)))
        {
            LoadConstraintIntoEditor(packageName, constraint);
        }

        if (GUILayout.Button("X", EditorStyles.miniButton, GUILayout.Width(25)))
        {
            if (EditorUtility.DisplayDialog(
                "Remove Constraint",
                $"Remove constraint for package '{packageName}'?",
                "Remove", "Cancel"))
            {
                _config.Constraints.Remove(packageName);
                _hasChanges = true;
            }
        }

        EditorGUILayout.EndHorizontal();

        if (constraint.ForbiddenNamespaces?.Count > 0)
        {
            EditorGUILayout.LabelField(
                $"Forbidden Namespaces: {string.Join(", ", constraint.ForbiddenNamespaces)}",
                EditorStyles.miniLabel);
        }

        if (!string.IsNullOrEmpty(constraint.Description))
        {
            EditorGUILayout.LabelField(constraint.Description, EditorStyles.wordWrappedMiniLabel);
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(2);
    }

    private void LoadConstraintIntoEditor(string packageName, PackageConstraint constraint)
    {
        _editPackageName = packageName;
        _editNamespaces = string.Join("\n", constraint.ForbiddenNamespaces ?? new List<string>());
        _editAssemblies = string.Join("\n", constraint.ForbiddenAssemblies ?? new List<string>());
        _editEnabled = constraint.IsEnabled;
        _editSeverityIndex = Array.IndexOf(_severityLabels, constraint.Severity.ToString());
        if (_editSeverityIndex < 0) _editSeverityIndex = 0;
        _editDescription = constraint.Description ?? string.Empty;
        _isNewConstraint = false;
        _scrollPosition.y = float.MaxValue;
    }

    private void ShowAddDialog()
    {
        _editPackageName = string.Empty;
        _editNamespaces = "UnityEngine\nUnityEngine.UI";
        _editAssemblies = string.Empty;
        _editEnabled = true;
        _editSeverityIndex = 0;
        _editDescription = string.Empty;
        _isNewConstraint = true;
        _scrollPosition.y = float.MaxValue;
    }

    private void ApplyEdit()
    {
        if (string.IsNullOrWhiteSpace(_editPackageName))
        {
            ShowStatus("Package name cannot be empty.", Color.red);
            return;
        }

        if (_config == null)
            _config = new PackageConstraintsConfig();
        if (_config.Constraints == null)
            _config.Constraints = new Dictionary<string, PackageConstraint>();

        var constraint = new PackageConstraint
        {
            PackageName = _editPackageName,
            ForbiddenNamespaces = ParseList(_editNamespaces),
            ForbiddenAssemblies = ParseList(_editAssemblies),
            IsEnabled = _editEnabled,
            Severity = ParseSeverity(_editSeverityIndex),
            CheckUsingAliases = true,
            Description = _editDescription
        };

        _config.Constraints[_editPackageName] = constraint;
        _hasChanges = true;
        ShowStatus($"Constraint for '{_editPackageName}' saved.", Color.green);
    }

    private void SaveConfig()
    {
        // 应用编辑中的约束
        ApplyEdit();

        // 应用全局默认值
        if (_config != null && _config.GlobalDefaults != null)
        {
            _config.GlobalDefaults.Enabled = _editGlobalEnabled;
            _config.GlobalDefaults.ForbiddenNamespaces = ParseList(_editGlobalNamespaces);
            _config.GlobalDefaults.ForbiddenAssemblies = ParseList(_editGlobalAssemblies);
            _config.GlobalDefaults.Severity = ParseSeverity(_editGlobalSeverityIndex);
        }

        try
        {
            var targetPath = _currentConfigPath;
            if (string.IsNullOrEmpty(targetPath))
            {
                // 使用第一个搜索路径
                targetPath = ConstraintLoader.SearchPaths[0];
                var dir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
            }

            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore
            };

            var json = JsonConvert.SerializeObject(_config, settings);
            File.WriteAllText(targetPath, json);

            ShowStatus($"Saved to: {targetPath}", Color.green);
            _hasChanges = false;
            AssetDatabase.Refresh();
        }
        catch (Exception ex)
        {
            ShowStatus($"Save failed: {ex.Message}", Color.red);
        }
    }

    private void LoadConfig()
    {
        try
        {
            _loader.Reload();
            _config = _loader.Load();
            _currentConfigPath = _loader.ConfigPath;

            // 加载全局默认值到编辑器
            if (_config?.GlobalDefaults != null)
            {
                _editGlobalEnabled = _config.GlobalDefaults.Enabled;
                _editGlobalNamespaces = string.Join("\n", _config.GlobalDefaults.ForbiddenNamespaces ?? new List<string>());
                _editGlobalAssemblies = string.Join("\n", _config.GlobalDefaults.ForbiddenAssemblies ?? new List<string>());
                _editGlobalSeverityIndex = Array.IndexOf(
                    _severityLabels, _config.GlobalDefaults.Severity.ToString());
                if (_editGlobalSeverityIndex < 0) _editGlobalSeverityIndex = 0;
            }

            ShowStatus("Configuration loaded successfully.", Color.green);
        }
        catch (Exception ex)
        {
            _config = new PackageConstraintsConfig();
            ShowStatus($"Load failed: {ex.Message}", Color.red);
        }
    }

    private void ValidateConfig()
    {
        if (_config == null)
        {
            ShowStatus("No configuration to validate.", Color.yellow);
            return;
        }

        var errors = ConstraintValidator.Validate(_config);
        if (errors.Count == 0)
        {
            ShowStatus($"Configuration is valid. {_config.Constraints?.Count ?? 0} constraints defined.", Color.green);
        }
        else
        {
            foreach (var error in errors)
            {
                Debug.LogWarning($"[ConstraintValidator] {error}");
            }
            ShowStatus($"Validation found {errors.Count} issue(s). Check Console for details.", Color.yellow);
        }
    }

    private void OpenConfigFile()
    {
        var path = _currentConfigPath ?? ConstraintLoader.SearchPaths[0];
        if (!File.Exists(path))
        {
            // 创建默认文件
            SaveConfig();
        }

        if (File.Exists(path))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        else
        {
            ShowStatus($"Config file not found at: {path}", Color.red);
        }
    }

    private void DrawEditPanel()
    {
        var hasContent = !string.IsNullOrEmpty(_editPackageName) || _isNewConstraint;
        if (!hasContent)
            return;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        var title = _isNewConstraint ? "+ Add New Constraint" : $"Edit: {_editPackageName}";
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        EditorGUI.indentLevel++;

        _editPackageName = EditorGUILayout.TextField("Package Name", _editPackageName);

        EditorGUILayout.LabelField("Forbidden Namespaces (one per line)", EditorStyles.miniLabel);
        _editNamespaces = EditorGUILayout.TextArea(_editNamespaces, GUILayout.Height(60));

        EditorGUILayout.LabelField("Forbidden Assemblies (one per line)", EditorStyles.miniLabel);
        _editAssemblies = EditorGUILayout.TextArea(_editAssemblies, GUILayout.Height(40));

        _editEnabled = EditorGUILayout.Toggle("Enabled", _editEnabled);
        _editSeverityIndex = EditorGUILayout.Popup("Severity", _editSeverityIndex, _severityLabels);

        EditorGUILayout.LabelField("Description", EditorStyles.miniLabel);
        _editDescription = EditorGUILayout.TextArea(_editDescription, GUILayout.Height(30));

        EditorGUI.indentLevel--;
        EditorGUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        if (GUILayout.Button(_isNewConstraint ? "Apply Add" : "Apply Edit", GUILayout.Width(100)))
        {
            ApplyEdit();
        }

        if (GUILayout.Button("Cancel", GUILayout.Width(60)))
        {
            _editPackageName = string.Empty;
            _editNamespaces = string.Empty;
            _editAssemblies = string.Empty;
            _editDescription = string.Empty;
            _isNewConstraint = false;
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private void ShowStatus(string message, Color color)
    {
        _statusMessage = $"[{DateTime.Now:HH:mm:ss}] {message}";
        _statusColor = color;
    }

    private void DrawStatusBar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

        if (!string.IsNullOrEmpty(_statusMessage))
        {
            var style = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = _statusColor }
            };
            EditorGUILayout.LabelField(_statusMessage, style);
        }
        else
        {
            EditorGUILayout.LabelField("Ready.", EditorStyles.miniLabel);
        }

        GUILayout.FlexibleSpace();

        if (_hasChanges)
        {
            EditorGUILayout.LabelField("Unsaved changes", new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = Color.yellow }
            });
        }

        EditorGUILayout.EndHorizontal();
    }

    private static List<string> ParseList(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<string>();

        var result = new List<string>();
        var lines = text.Split(new[] { '\n', '\r', ',' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (!string.IsNullOrEmpty(trimmed))
                result.Add(trimmed);
        }
        return result;
    }

    private static AKDiagnosticSeverity ParseSeverity(int index)
    {
        return index switch
        {
            0 => AKDiagnosticSeverity.Error,
            1 => AKDiagnosticSeverity.Warning,
            2 => AKDiagnosticSeverity.Info,
            3 => AKDiagnosticSeverity.Hidden,
            _ => AKDiagnosticSeverity.Error
        };
    }
}

} // namespace

#endif // UNITY_EDITOR
