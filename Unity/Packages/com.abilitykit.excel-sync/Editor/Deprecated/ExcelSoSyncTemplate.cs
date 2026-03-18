using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using Sirenix.OdinInspector;
using AbilityKit.ExcelSync.Editor.Codecs;

namespace AbilityKit.ExcelSync.Editor
{
    [CreateAssetMenu(fileName = "ExcelSoSyncTemplate", menuName = "工具/Excel/ExcelSoSyncTemplate")]
    public sealed class ExcelSoSyncTemplate : ScriptableObject
    {
        private const string ExcelRootPrefKey = "aurora_excel_so_sync_excel_root";

        private const string DefaultCodeOutputFolderAssetsPath = "Assets/Scripts/Editor/Excel/Generated";
        private const string DefaultAssetOutputFolderAssetsPath = "Assets/Game/Configs/Excel";

        public string CodeOutputFolderAssetsPath = DefaultCodeOutputFolderAssetsPath;
        public string AssetOutputFolderAssetsPath = DefaultAssetOutputFolderAssetsPath;

        [LabelText("使用运行时类型校验 Schema")]
        public bool ValidateSchema = true;

        public ScriptableObject TargetAsset;
        public string ExcelRelativePath = "";

        public string RuntimeRowTypeName = "";

        [ShowInInspector]
        [ReadOnly]
        public string SheetRowTypeName => (string.IsNullOrWhiteSpace(SheetName) ? "Sheet1" : SheetName.Trim()) + "Row";

        [ShowInInspector]
        [ReadOnly]
        public string SheetTableTypeName => (string.IsNullOrWhiteSpace(SheetName) ? "Sheet1" : SheetName.Trim()) + "Table";

        [Button("一键生成+创建+导入")]
        private void OneClickGenerateCreateAndImport()
        {
            var assetPath = AssetDatabase.GetAssetPath(this);
            if (string.IsNullOrEmpty(assetPath))
            {
                EditorUtility.DisplayDialog(
                    "模板未保存",
                    "请先将 ExcelSoSyncTemplate 保存为一个 .asset（Project 面板中），再使用一键流程。",
                    "确定");
                return;
            }

            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
            {
                EditorUtility.DisplayDialog(
                    "GUID 获取失败",
                    "无法获取模板资产 GUID。",
                    "确定");
                return;
            }

            if (EditorApplication.isCompiling)
            {
                EditorUtility.DisplayDialog(
                    "正在编译",
                    "Unity 正在编译中，请稍后再点一键流程。",
                    "确定");
                return;
            }

            SessionState.SetString(OneClickState.BuildKey(guid, OneClickState.KeyAssetPath), assetPath);
            SessionState.SetInt(OneClickState.BuildKey(guid, OneClickState.KeyStep), (int)OneClickStep.PendingCompileThenCreateAndImport);

            GenerateRowAndTableCode();

            CompilationPipeline.RequestScriptCompilation();
            AssetDatabase.Refresh();
        }

        [Button("生成 Row/Table 代码")]
        private void GenerateRowAndTableCode()
        {
            var excelPath = GetExcelAbsolutePath();
            if (string.IsNullOrWhiteSpace(excelPath))
            {
                EditorUtility.DisplayDialog(
                    "缺少 Excel 路径",
                    "请先在模板中选择 Excel 文件（ExcelRelativePath）。",
                    "确定");
                return;
            }

            if (string.IsNullOrWhiteSpace(CodeOutputFolderAssetsPath))
            {
                CodeOutputFolderAssetsPath = DefaultCodeOutputFolderAssetsPath;
            }

            if (!CodeOutputFolderAssetsPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("CodeOutputFolderAssetsPath must start with Assets/");
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

            EnsureAssetsFolder(CodeOutputFolderAssetsPath);

            var rowShellAssetsPath = CodeOutputFolderAssetsPath.TrimEnd('/', '\\') + "/" + SheetRowTypeName + ".cs";
            var rowRawAssetsPath = CodeOutputFolderAssetsPath.TrimEnd('/', '\\') + "/" + SheetRowTypeName + ".Raw.g.cs";
            var tableShellAssetsPath = CodeOutputFolderAssetsPath.TrimEnd('/', '\\') + "/" + SheetTableTypeName + ".cs";

            WriteRowShellIfMissing(rowShellAssetsPath, SheetRowTypeName);
            WriteTableShell(tableShellAssetsPath, SheetTableTypeName, SheetRowTypeName);

            if (TargetAsset == null)
            {
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog(
                    "已生成壳代码",
                    "已生成 Row/Table 壳代码。\n\n请等待 Unity 编译完成后，点击『创建/绑定 Table.asset』创建资产并绑定到 TargetAsset，然后再次点击『生成 Row/Table 代码』生成 Raw 字段文件。",
                    "确定");
                return;
            }

            if (ValidateSchema && !string.IsNullOrWhiteSpace(RuntimeRowTypeName))
            {
                ExcelSyncService.ValidateSchema(TargetAsset, RuntimeRowTypeName);
            }

            var rawAssetsPath = ExcelSyncService.GenerateEditorPartialRawFromExcel(
                TargetAsset,
                RuntimeRowTypeName,
                excelPath,
                options,
                CodeOutputFolderAssetsPath);

            var rawObj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(rawAssetsPath);
            if (rawObj != null)
            {
                EditorGUIUtility.PingObject(rawObj);
                Selection.activeObject = rawObj;
            }

            AssetDatabase.Refresh();
        }

        [Button("创建/绑定 Table.asset")]
        private void CreateOrBindTableAsset()
        {
            if (string.IsNullOrWhiteSpace(AssetOutputFolderAssetsPath))
            {
                AssetOutputFolderAssetsPath = DefaultAssetOutputFolderAssetsPath;
            }

            if (!AssetOutputFolderAssetsPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("AssetOutputFolderAssetsPath must start with Assets/");
            }

            var fullTypeName = "AbilityKit.ExcelSync.Generated." + SheetTableTypeName;
            var tableType = ResolveTypeByName(fullTypeName);
            if (tableType == null)
            {
                EditorUtility.DisplayDialog(
                    "类型未生成",
                    $"找不到类型：{fullTypeName}\n\n请先点击『生成 Row/Table 代码』并等待 Unity 编译完成后再试。",
                    "确定");
                return;
            }

            EnsureAssetsFolder(AssetOutputFolderAssetsPath);
            var assetPath = AssetOutputFolderAssetsPath.TrimEnd('/', '\\') + "/" + SheetTableTypeName + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath(assetPath, tableType) as ScriptableObject;
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance(tableType);
                AssetDatabase.CreateAsset(asset, assetPath);
                AssetDatabase.SaveAssets();
            }

            TargetAsset = asset;
            EditorUtility.SetDirty(this);

            EditorGUIUtility.PingObject(asset);
            Selection.activeObject = asset;
        }

        [Button("导入 Excel -> Table")]
        private void Import()
        {
            if (TargetAsset == null)
            {
                throw new InvalidOperationException("模板 TargetAsset 为空");
            }

            var excelPath = GetExcelAbsolutePath();
            if (string.IsNullOrWhiteSpace(excelPath) || !File.Exists(excelPath))
            {
                throw new InvalidOperationException("Excel 文件不存在: " + excelPath);
            }

            if (ValidateSchema && !string.IsNullOrWhiteSpace(RuntimeRowTypeName))
            {
                ExcelSyncService.ValidateSchema(TargetAsset, RuntimeRowTypeName);
            }

            var backend = new EpplusTableReaderWriterFactory();
            ExcelSyncService.Import(TargetAsset, excelPath, ToOptions(), backend, ExcelCodecRegistry.Default);
        }

        [Button("导出 Table -> Excel")]
        private void Export()
        {
            if (TargetAsset == null)
            {
                throw new InvalidOperationException("模板 TargetAsset 为空");
            }

            var excelPath = GetExcelAbsolutePath();
            if (string.IsNullOrWhiteSpace(excelPath))
            {
                throw new InvalidOperationException("ExcelRelativePath 为空");
            }

            var backend = new EpplusTableReaderWriterFactory();
            ExcelSyncService.Export(TargetAsset, excelPath, ToOptions(), backend, ExcelCodecRegistry.Default);
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
        public string PrimaryKeyColumnName = "code";

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

        private static void EnsureAssetsFolder(string assetsPath)
        {
            var abs = ToAbsolutePathFromAssetsPath(assetsPath);
            if (string.IsNullOrEmpty(abs))
            {
                throw new InvalidOperationException("Invalid Assets path: " + assetsPath);
            }

            Directory.CreateDirectory(abs);
        }

        private static string ToAbsolutePathFromAssetsPath(string assetsPath)
        {
            if (string.IsNullOrEmpty(assetsPath))
            {
                return string.Empty;
            }

            if (Path.IsPathRooted(assetsPath))
            {
                return assetsPath;
            }

            if (!assetsPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFullPath(assetsPath);
            }

            var rel = assetsPath.Substring("Assets/".Length);
            return Path.Combine(Application.dataPath, rel);
        }

        private static void WriteRowShellIfMissing(string assetsPath, string rowTypeName)
        {
            var abs = ToAbsolutePathFromAssetsPath(assetsPath);
            if (File.Exists(abs))
            {
                return;
            }

            var sb = new StringBuilder(256);
            sb.AppendLine("using System;");
            sb.AppendLine();
            sb.AppendLine("namespace AbilityKit.ExcelSync.Generated");
            sb.AppendLine("{");
            sb.AppendLine("    [Serializable]");
            sb.Append("    public sealed partial class ").Append(rowTypeName).AppendLine();
            sb.AppendLine("    {");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            File.WriteAllText(abs, sb.ToString(), Encoding.UTF8);
        }

        private static void WriteTableShell(string assetsPath, string tableTypeName, string rowTypeName)
        {
            var abs = ToAbsolutePathFromAssetsPath(assetsPath);

            var sb = new StringBuilder(512);
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine("namespace AbilityKit.ExcelSync.Generated");
            sb.AppendLine("{");
            sb.Append("    public sealed class ").Append(tableTypeName).AppendLine(" : ScriptableObject");
            sb.AppendLine("    {");
            sb.Append("        public List<").Append(rowTypeName).AppendLine("> DataList = new List<" + rowTypeName + ">();");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            File.WriteAllText(abs, sb.ToString(), Encoding.UTF8);
        }

        private static Type ResolveTypeByName(string fullTypeName)
        {
            if (string.IsNullOrWhiteSpace(fullTypeName))
            {
                return null;
            }

            var t = Type.GetType(fullTypeName, false);
            if (t != null)
            {
                return t;
            }

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                var a = assemblies[i];
                if (a == null)
                {
                    continue;
                }

                t = a.GetType(fullTypeName, false);
                if (t != null)
                {
                    return t;
                }
            }

            return null;
        }

        private enum OneClickStep
        {
            None = 0,
            PendingCompileThenCreateAndImport = 1,
        }

        private static class OneClickState
        {
            public const string KeyStep = "step";
            public const string KeyAssetPath = "assetPath";
            private const string Prefix = "AbilityKit.ExcelSync.ExcelSoSyncTemplate.OneClick.";

            public static string BuildKey(string guid, string suffix)
            {
                return Prefix + guid + "." + suffix;
            }

            public static void Clear(string guid)
            {
                SessionState.EraseString(BuildKey(guid, KeyAssetPath));
                SessionState.EraseInt(BuildKey(guid, KeyStep));
            }
        }

        [InitializeOnLoad]
        private static class OneClickRunner
        {
            static OneClickRunner()
            {
                EditorApplication.delayCall += TryContinue;
            }

            private static void TryContinue()
            {
                if (EditorApplication.isCompiling)
                {
                    EditorApplication.delayCall += TryContinue;
                    return;
                }

                if (EditorApplication.isUpdating)
                {
                    EditorApplication.delayCall += TryContinue;
                    return;
                }

                var templateGuids = AssetDatabase.FindAssets("t:ExcelSoSyncTemplate");
                if (templateGuids == null || templateGuids.Length == 0)
                {
                    return;
                }

                for (int i = 0; i < templateGuids.Length; i++)
                {
                    var guid = templateGuids[i];
                    var step = (OneClickStep)SessionState.GetInt(OneClickState.BuildKey(guid, OneClickState.KeyStep), 0);
                    if (step == OneClickStep.None)
                    {
                        continue;
                    }

                    var assetPath = SessionState.GetString(OneClickState.BuildKey(guid, OneClickState.KeyAssetPath), string.Empty);
                    if (string.IsNullOrEmpty(assetPath))
                    {
                        OneClickState.Clear(guid);
                        continue;
                    }

                    var template = AssetDatabase.LoadAssetAtPath<ExcelSoSyncTemplate>(assetPath);
                    if (template == null)
                    {
                        OneClickState.Clear(guid);
                        continue;
                    }

                    try
                    {
                        template.CreateOrBindTableAsset();
                        template.GenerateRowAndTableCode();
                        template.Import();
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                    finally
                    {
                        OneClickState.Clear(guid);
                    }
                }
            }
        }

    }
}
