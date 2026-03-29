using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AbilityKit.ExcelSync.Editor.Codecs;
using OfficeOpenXml;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.ExcelSync.Editor
{
    /// <summary>
    /// Excel 同步服务 - 基于模板的同步操作
    /// </summary>
    public static class ExcelSyncTemplateService
    {
        private static ITableReaderWriterFactory _defaultBackend;

        /// <summary>
        /// 获取默认的后端工厂
        /// </summary>
        public static ITableReaderWriterFactory DefaultBackend
        {
            get
            {
                if (_defaultBackend == null)
                {
                    _defaultBackend = new EpplusTableReaderWriterFactory();
                }
                return _defaultBackend;
            }
        }

        #region 代码生成

        /// <summary>
        /// 为单个模板生成代码
        /// </summary>
        public static CodeGenResult GenerateCode(ExcelSyncTableTemplate template, ExcelSyncProjectConfig config)
        {
            if (template == null)
            {
                return CodeGenResult.Fail("模板为空");
            }

            if (string.IsNullOrEmpty(template.ExcelRelativePath))
            {
                return CodeGenResult.Fail("Excel 相对路径为空");
            }

            var excelPath = config.GetExcelAbsolutePath(template);
            if (string.IsNullOrEmpty(excelPath) || !File.Exists(excelPath))
            {
                return CodeGenResult.Fail($"Excel 文件不存在: {excelPath}");
            }

            try
            {
                var options = template.CreateOptions();
                var codeOutputFolder = config.GetCodeOutputFolder(template);
                var ns = !string.IsNullOrEmpty(template.Namespace) ? template.Namespace : config.DefaultNamespace;
                var rowTypeName = template.GetRowTypeName();
                var tableTypeName = template.GetTableTypeName();

                // 确保目录存在
                EnsureAssetsFolder(codeOutputFolder);

                // 生成 Row 壳类
                var rowShellPath = codeOutputFolder.TrimEnd('/', '\\') + "/" + rowTypeName + ".cs";
                WriteRowShellIfMissing(rowShellPath, rowTypeName, ns);

                // 生成 Table 壳类
                var tableShellPath = codeOutputFolder.TrimEnd('/', '\\') + "/" + tableTypeName + ".cs";
                WriteTableShell(tableShellPath, tableTypeName, rowTypeName, ns);

                // 如果已有 Table Asset，生成 Raw 字段
                if (template.TableAsset != null)
                {
                    ExcelSyncService.GenerateEditorPartialRawFromExcel(
                        template.TableAsset,
                        template.RuntimeRowTypeName,
                        excelPath,
                        options,
                        codeOutputFolder);
                }

                AssetDatabase.Refresh();

                return CodeGenResult.Succeed($"代码生成完成: {rowTypeName}, {tableTypeName}");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return CodeGenResult.Fail($"代码生成失败: {e.Message}");
            }
        }

        /// <summary>
        /// 为所有启用的模板生成代码
        /// </summary>
        public static BatchResult GenerateAllCode(ExcelSyncProjectConfig config)
        {
            var result = new BatchResult();

            if (config == null)
            {
                result.AddError("配置", "项目配置为空");
                return result;
            }

            foreach (var template in config.EnabledTemplates)
            {
                var genResult = GenerateCode(template, config);
                result.Add(template.GetDisplayName(), genResult.IsSuccess, genResult.Message);
            }

            return result;
        }

        #endregion

        #region Asset 创建

        /// <summary>
        /// 为模板创建或绑定 Table Asset
        /// </summary>
        public static AssetCreationResult CreateOrBindTableAsset(ExcelSyncTableTemplate template, ExcelSyncProjectConfig config)
        {
            if (template == null)
            {
                return AssetCreationResult.Fail("模板为空");
            }

            try
            {
                var assetOutputFolder = config.GetAssetOutputFolder(template);
                var tableTypeName = template.GetTableTypeName();
                var rowTypeName = template.GetRowTypeName();
                var ns = !string.IsNullOrEmpty(template.Namespace) ? template.Namespace : config.DefaultNamespace;
                var fullTypeName = ns + "." + tableTypeName;

                // 解析类型
                var tableType = ResolveTypeByName(fullTypeName);
                if (tableType == null)
                {
                    return AssetCreationResult.Fail($"找不到类型: {fullTypeName}\n请先执行代码生成并等待编译完成");
                }

                // 确保目录存在
                EnsureAssetsFolder(assetOutputFolder);

                // 创建或加载 Asset
                var assetPath = assetOutputFolder.TrimEnd('/', '\\') + "/" + tableTypeName + ".asset";
                var asset = AssetDatabase.LoadAssetAtPath(assetPath, tableType) as ScriptableObject;

                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance(tableType);
                    AssetDatabase.CreateAsset(asset, assetPath);
                    AssetDatabase.SaveAssets();
                }

                // 绑定到模板
                template.TableAsset = asset;

                AssetDatabase.Refresh();

                return AssetCreationResult.Succeed(assetPath, asset);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return AssetCreationResult.Fail($"创建 Asset 失败: {e.Message}");
            }
        }

        #endregion

        #region 导入导出

        /// <summary>
        /// 导入单个模板
        /// </summary>
        public static SyncResult Import(ExcelSyncTableTemplate template, ExcelSyncProjectConfig config)
        {
            if (template == null || template.TableAsset == null)
            {
                return SyncResult.Fail("模板未绑定 Table Asset");
            }

            var excelPath = config.GetExcelAbsolutePath(template);
            if (string.IsNullOrEmpty(excelPath) || !File.Exists(excelPath))
            {
                return SyncResult.Fail($"Excel 文件不存在: {excelPath}");
            }

            try
            {
                // Schema 校验
                if (template.ValidateSchema && !string.IsNullOrEmpty(template.RuntimeRowTypeName))
                {
                    ExcelSyncService.ValidateSchema(template.TableAsset, template.RuntimeRowTypeName);
                }

                var options = template.CreateOptions();
                ExcelSyncService.Import(template.TableAsset, excelPath, options, DefaultBackend, ExcelCodecRegistry.Default);

                Debug.Log($"[ExcelSync] 导入成功: {template.GetDisplayName()}");
                return SyncResult.Succeed();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return SyncResult.Fail($"导入失败: {FormatUserErrorMessage(e)}");
            }
        }

        /// <summary>
        /// 导出单个模板
        /// </summary>
        public static SyncResult Export(ExcelSyncTableTemplate template, ExcelSyncProjectConfig config)
        {
            if (template == null || template.TableAsset == null)
            {
                return SyncResult.Fail("模板未绑定 Table Asset");
            }

            var excelPath = config.GetExcelAbsolutePath(template);
            if (string.IsNullOrEmpty(excelPath))
            {
                return SyncResult.Fail("Excel 路径为空");
            }

            try
            {
                var options = template.CreateOptions();
                ExcelSyncService.Export(template.TableAsset, excelPath, options, DefaultBackend, ExcelCodecRegistry.Default);

                Debug.Log($"[ExcelSync] 导出成功: {template.GetDisplayName()}");
                return SyncResult.Succeed();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return SyncResult.Fail($"导出失败: {FormatUserErrorMessage(e)}");
            }
        }

        /// <summary>
        /// 批量导入
        /// </summary>
        public static BatchResult ImportAll(ExcelSyncProjectConfig config)
        {
            var result = new BatchResult();

            if (config == null)
            {
                result.AddError("配置", "项目配置为空");
                return result;
            }

            foreach (var template in config.EnabledTemplates)
            {
                if (template.TableAsset == null)
                {
                    result.Add(template.GetDisplayName(), false, "未绑定 Table Asset");
                    continue;
                }

                var syncResult = Import(template, config);
                result.Add(template.GetDisplayName(), syncResult.IsSuccess, syncResult.Message);
            }

            return result;
        }

        /// <summary>
        /// 批量导出
        /// </summary>
        public static BatchResult ExportAll(ExcelSyncProjectConfig config)
        {
            var result = new BatchResult();

            if (config == null)
            {
                result.AddError("配置", "项目配置为空");
                return result;
            }

            foreach (var template in config.EnabledTemplates)
            {
                if (template.TableAsset == null)
                {
                    result.Add(template.GetDisplayName(), false, "未绑定 Table Asset");
                    continue;
                }

                if (template.BaselineStatus == BaselineStatus.Missing)
                {
                    result.Add(template.GetDisplayName(), false, "缺少基线，请先导入");
                    continue;
                }

                var syncResult = Export(template, config);
                result.Add(template.GetDisplayName(), syncResult.IsSuccess, syncResult.Message);
            }

            return result;
        }

        #endregion

        #region 一键同步

        /// <summary>
        /// 一键生成并导入
        /// </summary>
        public static BatchResult GenerateAndImportAll(ExcelSyncProjectConfig config)
        {
            var result = new BatchResult();

            if (config == null)
            {
                result.AddError("配置", "项目配置为空");
                return result;
            }

            // 1. 批量生成代码
            var codeResult = GenerateAllCode(config);
            result.Merge(codeResult);

            // 等待编译
            if (codeResult.HasErrors)
            {
                Debug.LogWarning("[ExcelSync] 部分代码生成失败，跳过后续步骤");
                return result;
            }

            // 2. 批量创建 Asset
            foreach (var template in config.EnabledTemplates)
            {
                if (template.TableAsset != null)
                {
                    continue; // 已有 Asset，跳过
                }

                var assetResult = CreateOrBindTableAsset(template, config);
                if (!assetResult.IsSuccess)
                {
                    result.Add(template.GetDisplayName(), false, $"Asset 创建失败: {assetResult.Message}");
                }
            }

            // 3. 批量导入
            var importResult = ImportAll(config);
            result.Merge(importResult);

            return result;
        }

        #endregion

        #region 自动扫描

        /// <summary>
        /// 扫描 Excel 文件并自动创建模板
        /// </summary>
        public static ScanResult ScanAndCreateTemplates(ExcelSyncProjectConfig config)
        {
            var result = new ScanResult();

            if (config == null)
            {
                result.AddError("配置", "项目配置为空");
                return result;
            }

            var excelFiles = config.ScanExcelFiles();

            foreach (var file in excelFiles)
            {
                var relativePath = config.GetExcelRelativePath(file);
                var templateName = Path.GetFileNameWithoutExtension(relativePath);

                // 检查是否已有模板
                var existing = config.FindTemplateByExcelPath(relativePath);
                if (existing != null)
                {
                    result.AddExisting(templateName, relativePath);
                    continue;
                }

                // 获取 Sheet 名称
                var sheetNames = GetExcelSheetNames(file);
                foreach (var sheetName in sheetNames)
                {
                    // 为每个 Sheet 创建模板
                    var template = new ExcelSyncTableTemplate
                    {
                        Enabled = true,
                        DisplayName = string.IsNullOrEmpty(sheetName) || sheetName == "Sheet1"
                            ? templateName
                            : templateName + "_" + sheetName,
                        ExcelRelativePath = relativePath,
                        SheetName = sheetName,
                        HeaderRowIndex = config.DefaultHeaderRowIndex,
                        DataStartRowIndex = config.DefaultDataStartRowIndex,
                        PrimaryKeyColumnName = config.DefaultPrimaryKeyColumnName
                    };

                    config.AddTemplate(template);
                    result.AddCreated(template.GetDisplayName(), relativePath, sheetName);
                }
            }

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            return result;
        }

        /// <summary>
        /// 获取 Excel 文件的 Sheet 名称列表
        /// </summary>
        public static List<string> GetExcelSheetNames(string excelPath)
        {
            var sheetNames = new List<string>();

            try
            {
                #if EPPLUS_4_5_OR_NEWER
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                #endif
                using var package = new ExcelPackage(new FileInfo(excelPath));
                if (package.Workbook.Worksheets.Count > 0)
                {
                    foreach (var sheet in package.Workbook.Worksheets)
                    {
                        sheetNames.Add(sheet.Name);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ExcelSync] 读取 Excel Sheet 失败: {excelPath}\n{e.Message}");
                sheetNames.Add(""); // 返回空字符串表示使用第一个 Sheet
            }

            return sheetNames;
        }

        #endregion

        #region 辅助方法

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

        private static void WriteRowShellIfMissing(string assetsPath, string rowTypeName, string ns)
        {
            var abs = ToAbsolutePathFromAssetsPath(assetsPath);
            if (File.Exists(abs))
            {
                return;
            }

            var sb = new StringBuilder(256);
            sb.AppendLine("using System;");
            sb.AppendLine();
            sb.Append("namespace ").AppendLine(ns);
            sb.AppendLine("{");
            sb.AppendLine("    [Serializable]");
            sb.Append("    public sealed partial class ").Append(rowTypeName).AppendLine();
            sb.AppendLine("    {");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            File.WriteAllText(abs, sb.ToString(), Encoding.UTF8);
        }

        private static void WriteTableShell(string assetsPath, string tableTypeName, string rowTypeName, string ns)
        {
            var abs = ToAbsolutePathFromAssetsPath(assetsPath);

            var sb = new StringBuilder(512);
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.Append("namespace ").AppendLine(ns);
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
                if (a == null) continue;

                t = a.GetType(fullTypeName, false);
                if (t != null)
                {
                    return t;
                }
            }

            return null;
        }

        private static string FormatUserErrorMessage(Exception e)
        {
            if (e == null) return "未知错误";

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

            return string.IsNullOrWhiteSpace(msg) ? "未知错误" : msg;
        }

        #endregion
    }

    #region 结果类

    public class CodeGenResult
    {
        public bool IsSuccess { get; private set; }
        public string Message { get; private set; }

        public static CodeGenResult Succeed(string message) => new() { IsSuccess = true, Message = message };
        public static CodeGenResult Fail(string message) => new() { IsSuccess = false, Message = message };
    }

    public class AssetCreationResult
    {
        public bool IsSuccess { get; private set; }
        public string Message { get; private set; }
        public string AssetPath { get; private set; }
        public ScriptableObject Asset { get; private set; }

        public static AssetCreationResult Succeed(string assetPath, ScriptableObject asset) =>
            new() { IsSuccess = true, AssetPath = assetPath, Asset = asset };
        public static AssetCreationResult Fail(string message) =>
            new() { IsSuccess = false, Message = message };
    }

    public class SyncResult
    {
        public bool IsSuccess { get; private set; }
        public string Message { get; private set; }

        public static SyncResult Succeed() => new() { IsSuccess = true };
        public static SyncResult Fail(string message) => new() { IsSuccess = false, Message = message };
    }

    public class BatchResult
    {
        private readonly List<(string Name, bool IsSuccess, string Message)> _items = new();

        public int TotalCount => _items.Count;
        public int SuccessCount => _items.Count(x => x.IsSuccess);
        public int ErrorCount => _items.Count(x => !x.IsSuccess);
        public bool HasErrors => ErrorCount > 0;
        public bool AllSuccess => TotalCount > 0 && ErrorCount == 0;

        public void Add(string name, bool isSuccess, string message)
        {
            _items.Add((name, isSuccess, message));
        }

        public void AddError(string name, string message)
        {
            _items.Add((name, false, message));
        }

        public void Merge(BatchResult other)
        {
            if (other == null) return;
            _items.AddRange(other._items);
        }

        public IEnumerable<(string Name, bool IsSuccess, string Message)> Items => _items;
        public IEnumerable<(string Name, bool IsSuccess, string Message)> Errors => _items.Where(x => !x.IsSuccess);
    }

    public class ScanResult
    {
        private readonly List<(string Name, string Path, string Sheet)> _created = new();
        private readonly List<(string Name, string Path)> _existing = new();
        private readonly List<(string Category, string Message)> _errors = new();

        public int CreatedCount => _created.Count;
        public int ExistingCount => _existing.Count;
        public bool HasErrors => _errors.Count > 0;

        public void AddCreated(string name, string path, string sheet)
        {
            _created.Add((name, path, sheet));
        }

        public void AddExisting(string name, string path)
        {
            _existing.Add((name, path));
        }

        public void AddError(string category, string message)
        {
            _errors.Add((category, message));
            Debug.LogError($"[ExcelSync] 扫描失败 [{category}]: {message}");
        }
    }

    #endregion
}
