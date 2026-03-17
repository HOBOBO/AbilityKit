using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.ExcelSync.Editor
{
    [Obsolete("ExcelSoSyncWindow 已废弃，请使用新的 ScriptableObjectDataBrowserWindow 和 ExcelSyncService")]
    public sealed class ExcelSoSyncWindow : EditorWindow
    {
        private const string TemplateFolderPrefKey = "aurora_excel_so_sync_template_folder";
        private const string ExcelRootPrefKey = "aurora_excel_so_sync_excel_root";

        [SerializeField] private DefaultAsset templateFolder;
        [SerializeField] private Vector2 scroll;

        private readonly List<ExcelSoSyncTemplate> templates = new List<ExcelSoSyncTemplate>();
        private readonly Dictionary<string, bool> selectedByGuid = new Dictionary<string, bool>();

        private readonly ITableReaderWriterFactory factory = new EpplusTableReaderWriterFactory();

        [MenuItem("Tools/Excel/Excel <-> ScriptableObject 同步")]
        public static void Open()
        {
            var w = GetWindow<ExcelSoSyncWindow>();
            w.titleContent = new GUIContent("Excel<->SO 同步");
            w.Show();
        }

        private void OnEnable()
        {
            var savedPath = EditorPrefs.GetString(TemplateFolderPrefKey, string.Empty);
            if (!string.IsNullOrEmpty(savedPath))
            {
                templateFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(savedPath);
            }

            RefreshTemplates();
        }

        private void OnGUI()
        {
            DrawExcelRoot();
            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField("模板目录", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                var newFolder = (DefaultAsset)EditorGUILayout.ObjectField(templateFolder, typeof(DefaultAsset), false);
                if (newFolder != templateFolder)
                {
                    templateFolder = newFolder;
                    var path = templateFolder != null ? AssetDatabase.GetAssetPath(templateFolder) : string.Empty;
                    EditorPrefs.SetString(TemplateFolderPrefKey, path);
                    RefreshTemplates();
                }

                if (GUILayout.Button("刷新", GUILayout.Width(80)))
                {
                    RefreshTemplates();
                }
            }

            using (new EditorGUI.DisabledScope(templateFolder == null))
            {
                if (GUILayout.Button("创建模板"))
                {
                    CreateTemplateAsset();
                }
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField($"模板列表（{templates.Count}）", EditorStyles.boldLabel);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (var i = 0; i < templates.Count; i++)
            {
                var t = templates[i];
                if (t == null)
                {
                    continue;
                }

                var path = AssetDatabase.GetAssetPath(t);
                var guid = AssetDatabase.AssetPathToGUID(path);
                selectedByGuid.TryGetValue(guid, out var sel);

                using (new EditorGUILayout.VerticalScope("box"))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var newSel = EditorGUILayout.Toggle(sel, GUILayout.Width(18));
                        if (newSel != sel)
                        {
                            selectedByGuid[guid] = newSel;
                        }

                        EditorGUILayout.ObjectField(t, typeof(ExcelSoSyncTemplate), false);
                    }

                    var rel = string.IsNullOrEmpty(t.ExcelRelativePath) ? "（空）" : t.ExcelRelativePath;
                    EditorGUILayout.LabelField("Excel", rel);
                    EditorGUILayout.LabelField("目标资源", t.TargetAsset != null ? AssetDatabase.GetAssetPath(t.TargetAsset) : "（空）");
                }
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(8);
            DrawBaselineHintForSelection();
            using (new EditorGUI.DisabledScope(!HasAnySelected()))
            {
                if (GUILayout.Button("导出所选"))
                {
                    if (ConfirmExportWhenBaselineMissing())
                    {
                        RunBatch(export: true);
                    }
                }

                if (GUILayout.Button("导入所选"))
                {
                    RunBatch(export: false);
                }
            }
        }

        private void DrawBaselineHintForSelection()
        {
            if (!HasAnySelected())
            {
                return;
            }

            var missing = GetSelectedTemplatesMissingBaseline();
            if (missing.Count == 0)
            {
                EditorGUILayout.HelpBox("基线正常：已启用安全导出（三方合并）。", MessageType.Info);
                return;
            }

            var preview = string.Join(", ", missing.Take(5).Select(x => x.name));
            EditorGUILayout.HelpBox($"{missing.Count} 个所选模板缺少基线：{preview}。请先执行导入以建立基线，否则导出将失败。", MessageType.Warning);
        }

        private bool ConfirmExportWhenBaselineMissing()
        {
            var missing = GetSelectedTemplatesMissingBaseline();
            if (missing.Count == 0)
            {
                return true;
            }

            var preview = string.Join("\n", missing.Take(10).Select(x => "- " + x.name));
            return EditorUtility.DisplayDialog(
                "缺少基线",
                $"安全导出需要基线（由导入生成）。\n\n缺少：\n{preview}\n\n仍要继续导出吗？（将失败并输出错误报告）",
                "继续",
                "取消");
        }

        private List<ExcelSoSyncTemplate> GetSelectedTemplatesMissingBaseline()
        {
            var result = new List<ExcelSoSyncTemplate>();
            foreach (var t in templates)
            {
                if (t == null || t.TargetAsset == null)
                {
                    continue;
                }

                var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(t));
                if (!selectedByGuid.TryGetValue(guid, out var sel) || !sel)
                {
                    continue;
                }

                var targetPath = AssetDatabase.GetAssetPath(t.TargetAsset);
                if (string.IsNullOrEmpty(targetPath))
                {
                    continue;
                }

                var baselinePath = targetPath + ".excelBaseline.asset";
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(baselinePath) == null)
                {
                    result.Add(t);
                }
            }

            return result;
        }

        private void RefreshTemplates()
        {
            templates.Clear();
            selectedByGuid.Clear();

            if (templateFolder == null)
            {
                return;
            }

            var folderPath = AssetDatabase.GetAssetPath(templateFolder);
            if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            var guids = AssetDatabase.FindAssets($"t:{nameof(ExcelSoSyncTemplate)}", new[] { folderPath });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<ExcelSoSyncTemplate>(path);
                if (asset != null)
                {
                    templates.Add(asset);
                    selectedByGuid[guid] = false;
                }
            }

            templates.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
        }

        private bool HasAnySelected()
        {
            foreach (var kv in selectedByGuid)
            {
                if (kv.Value)
                {
                    return true;
                }
            }

            return false;
        }

        private void RunBatch(bool export)
        {
            var selected = new List<ExcelSoSyncTemplate>();
            foreach (var t in templates)
            {
                if (t == null)
                {
                    continue;
                }

                var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(t));
                if (selectedByGuid.TryGetValue(guid, out var sel) && sel)
                {
                    selected.Add(t);
                }
            }

            if (selected.Count == 0)
            {
                return;
            }

            var errors = new List<string>();
            for (var i = 0; i < selected.Count; i++)
            {
                var tpl = selected[i];
                var progress = (float)i / selected.Count;
                EditorUtility.DisplayProgressBar("Excel<->SO 同步", $"{(export ? "导出" : "导入")} {tpl.name}", progress);
                try
                {
                    ExecuteTemplate(tpl, export);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    errors.Add($"{tpl.name}: {FormatUserErrorMessage(e)}");
                }
            }
            EditorUtility.ClearProgressBar();

            if (errors.Count > 0)
            {
                EditorUtility.DisplayDialog("完成（有错误）", string.Join("\n", errors.Take(15)), "确定");
            }
            else
            {
                EditorUtility.DisplayDialog("完成", export ? "导出完成" : "导入完成", "确定");
            }
        }

        private static string FormatUserErrorMessage(Exception e)
        {
            if (e == null)
            {
                return "未知错误";
            }

            var msg = e.Message ?? string.Empty;
            var lower = msg.ToLowerInvariant();

            if (lower.Contains("baseline") && (lower.Contains("missing") || lower.Contains("please run import")))
            {
                return "缺少基线，请先执行导入建立基线后再导出。";
            }

            if (lower.Contains("excel headers are missing columns required") || lower.Contains("missing columns required"))
            {
                return "Excel 表头缺少必要列，请检查表头是否与配置类字段一致。";
            }

            if (lower.Contains("duplicated headers"))
            {
                return "Excel 表头存在重复列，请修复后再同步。";
            }

            if (lower.Contains("missing primary key header") || lower.Contains("cannot find primary key"))
            {
                return "找不到主键列/字段，请检查模板主键设置与表头/类型是否一致。";
            }

            if (lower.Contains("cannot resolve runtime row type"))
            {
                return "无法解析运行时数据类型，请检查模板 RuntimeRowTypeName 是否填写为完整类型名。";
            }

            if (lower.Contains("editor model type") && lower.Contains("is not consistent with runtime type"))
            {
                return "编辑器数据类与运行时生成数据类不一致，请先生成/更新编辑器数据类后再同步。";
            }

            if (lower.Contains("conflicts") && lower.Contains("export aborted"))
            {
                return "检测到冲突，已中止导出，请查看冲突报告文件。";
            }

            if (string.IsNullOrWhiteSpace(msg))
            {
                return "未知错误";
            }

            return msg;
        }

        private void ExecuteTemplate(ExcelSoSyncTemplate tpl, bool export)
        {
            if (tpl == null)
            {
                throw new ArgumentNullException(nameof(tpl));
            }

            if (tpl.TargetAsset == null)
            {
                throw new InvalidOperationException("模板 TargetAsset 为空");
            }

            var excelPath = tpl.GetExcelAbsolutePath();
            if (string.IsNullOrEmpty(excelPath))
            {
                throw new InvalidOperationException("模板 ExcelRelativePath 为空");
            }

            var options = tpl.ToOptions();
            ExcelSyncService.ValidateSchema(tpl.TargetAsset, tpl.RuntimeRowTypeName);
            if (export)
            {
                ExcelSyncService.Export(tpl.TargetAsset, excelPath, options, factory);
            }
            else
            {
                ExcelSyncService.Import(tpl.TargetAsset, excelPath, options, factory);
            }
        }

        private void DrawExcelRoot()
        {
            EditorGUILayout.LabelField("Excel 根目录（按用户本地保存）", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                var root = EditorPrefs.GetString(ExcelRootPrefKey, string.Empty);
                EditorGUILayout.SelectableLabel(string.IsNullOrEmpty(root) ? "（未设置）" : root, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                if (GUILayout.Button("选择", GUILayout.Width(80)))
                {
                    var picked = EditorUtility.OpenFolderPanel("选择 Excel 根目录", root, string.Empty);
                    if (!string.IsNullOrEmpty(picked))
                    {
                        EditorPrefs.SetString(ExcelRootPrefKey, picked);
                    }
                }

                if (GUILayout.Button("清除", GUILayout.Width(80)))
                {
                    EditorPrefs.DeleteKey(ExcelRootPrefKey);
                }
            }
        }

        private void CreateTemplateAsset()
        {
            if (templateFolder == null)
            {
                return;
            }

            var folderPath = AssetDatabase.GetAssetPath(templateFolder);
            if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            var asset = CreateInstance<ExcelSoSyncTemplate>();
            var path = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(folderPath, "ExcelSoSyncTemplate.asset"));
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(asset);
            Selection.activeObject = asset;
            RefreshTemplates();
        }
    }
}
