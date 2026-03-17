using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Sirenix.OdinInspector;

namespace AbilityKit.ExcelSync.Editor
{
    [CreateAssetMenu(fileName = "ExcelSoSyncTemplate", menuName = "工具/Excel/ExcelSoSyncTemplate")]
    [Obsolete("ExcelSoSyncTemplate 已废弃，请使用新的 ExcelSyncService 和 ScriptableObjectDataBrowserWindow")]
    public sealed class ExcelSoSyncTemplate : ScriptableObject
    {
        private const string ExcelRootPrefKey = "aurora_excel_so_sync_excel_root";

        public ScriptableObject TargetAsset;
        public string ExcelRelativePath = "";

        public string RuntimeRowTypeName = "";

        [Button("生成编辑器数据类")]
        private void GenerateEditorModel()
        {
            if (TargetAsset == null)
            {
                throw new InvalidOperationException("模板 TargetAsset 为空");
            }

            if (string.IsNullOrWhiteSpace(RuntimeRowTypeName))
            {
                EditorUtility.DisplayDialog(
                    "缺少运行时类型",
                    "请在模板中填写 RuntimeRowTypeName（完整类型名），例如：\nHotUpdate.Config.TSkillEffectData",
                    "确定");
                return;
            }

            var excelPath = GetExcelAbsolutePath();
            if (string.IsNullOrWhiteSpace(excelPath))
            {
                EditorUtility.DisplayDialog(
                    "缺少 Excel 路径",
                    "请先在模板中选择 Excel 文件（ExcelRelativePath）。",
                    "确定");
                return;
            }

            if (!File.Exists(excelPath))
            {
                EditorUtility.DisplayDialog(
                    "Excel 不存在",
                    $"找不到 Excel 文件：\n{excelPath}\n\n请检查 Excel 根目录设置或重新选择文件。",
                    "确定");
                return;
            }

            var options = ToOptions();
            var assetsPath = ExcelSyncService.GenerateEditorPartialRawFromExcel(
                TargetAsset,
                RuntimeRowTypeName,
                excelPath,
                options,
                null);

            var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetsPath);
            if (obj != null)
            {
                EditorGUIUtility.PingObject(obj);
                Selection.activeObject = obj;
            }
        }

        [Button("选择 Excel 文件")]
        private void SelectExcelFile()
        {
            var picked = EditorUtility.OpenFilePanel("选择 Excel", GetExcelRootOrEmpty(), "xlsx");
            if (string.IsNullOrEmpty(picked))
            {
                return;
            }

            ExcelRelativePath = ToRelativeExcelPath(picked);
            EditorUtility.SetDirty(this);
        }

        public string SheetName = "";
        public int HeaderRowIndex = 6;
        public int DataStartRowIndex = 8;
        public string PrimaryKeyColumnName = "Code";

        public ExcelTableOptions ToOptions()
        {
            return new ExcelTableOptions
            {
                SheetName = SheetName,
                HeaderRowIndex = HeaderRowIndex,
                DataStartRowIndex = DataStartRowIndex,
                PrimaryKeyColumnName = PrimaryKeyColumnName
            };
        }

        public string GetExcelAbsolutePath()
        {
            if (string.IsNullOrEmpty(ExcelRelativePath))
            {
                return string.Empty;
            }

            if (Path.IsPathRooted(ExcelRelativePath))
            {
                return ExcelRelativePath;
            }

            var root = GetExcelRootOrEmpty();
            if (string.IsNullOrEmpty(root))
            {
                return ExcelRelativePath;
            }

            return Path.GetFullPath(Path.Combine(root, ExcelRelativePath));
        }

        private static string GetExcelRootOrEmpty()
        {
            return EditorPrefs.GetString(ExcelRootPrefKey, string.Empty);
        }

        private static string ToRelativeExcelPath(string absolutePath)
        {
            var root = GetExcelRootOrEmpty();
            if (!string.IsNullOrEmpty(root))
            {
                var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                var normalizedFile = Path.GetFullPath(absolutePath);
                if (normalizedFile.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                {
                    return normalizedFile.Substring(normalizedRoot.Length);
                }
            }

            return Path.GetFileName(absolutePath);
        }

    }
}
