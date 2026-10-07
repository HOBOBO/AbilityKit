#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.ExcelSync.Editor;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    /// <summary>
    /// 旧 SO/MiniTemplate 同步入口。仅保留只读 status 与迁移辅助代码；写入模式已停用。
    /// 生产配置编辑入口是 LubanConfig/Moba/Production/Datas，发布使用 export_pipeline.py。
    /// </summary>
    public static class MobaConfigHeadlessSync
    {
        private const string ResourcesMobaFolder = "Packages/com.abilitykit.demo.moba.view.runtime/Resources/moba";

        /// <summary>
        /// 不走 Excel 真相链的表：skill_flows 的 Phases 是多态相位列表（List&lt;SkillPhaseDef&gt;），
        /// 抽象类型无法经 codec 往返，该表以 JSON→Def 链（migrate-flows）为准。
        /// </summary>
        private static readonly HashSet<string> ExcelExcludedFiles =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "skill_flows" };

        private static bool IsExcelExcluded(MobaConfigTableAssetSO table)
        {
            return table != null && ExcelExcludedFiles.Contains(table.FileWithoutExt);
        }

        public static void Run()
        {
            var exitCode = 0;
            try
            {
                var args = Environment.GetCommandLineArgs();
                var mode = GetArg(args, "-mode") ?? "status";
                var table = GetArg(args, "-table");
                var excelFolder = GetArg(args, "-excelFolder") ?? MobaExcelSync.ExcelFolder;

                if (!string.Equals(mode, "status", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogError("[MobaConfigHeadlessSync] Legacy JSON/SO sync is retired. Edit LubanConfig/Moba/Production/Datas and run python LubanConfig/Moba/export_pipeline.py --apply.");
                    if (Application.isBatchMode) EditorApplication.Exit(3);
                    return;
                }
                Debug.LogWarning("[MobaConfigHeadlessSync] status reports the retired SO/MiniTemplate chain. Production source: LubanConfig/Moba/Production/Datas.");

                var tables = LoadTables(table);
                if (tables.Count == 0)
                {
                    Debug.LogError($"[MobaConfigHeadlessSync] No matching config table assets. table filter='{table}'");
                    EditorApplication.Exit(1);
                    return;
                }

                switch (mode.ToLowerInvariant())
                {
                    case "push-json":
                        exitCode = PushJson(tables, excelFolder);
                        break;
                    case "pull-excel":
                        exitCode = PullExcel(tables, excelFolder);
                        break;
                    case "bootstrap":
                        exitCode = Bootstrap(tables, excelFolder);
                        break;
                    case "export-typed":
                        exitCode = ExportTyped(tables, excelFolder);
                        break;
                    case "seed-from-json":
                        exitCode = SeedFromJson(tables, excelFolder);
                        break;
                    case "migrate-flows":
                        exitCode = MigrateFlows(tables, excelFolder);
                        break;
                    case "status":
                        exitCode = PrintStatus(tables, excelFolder);
                        break;
                    default:
                        Debug.LogError($"[MobaConfigHeadlessSync] Unknown -mode '{mode}'. Valid: push-json | pull-excel | bootstrap | status");
                        EditorApplication.Exit(3);
                        return;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[MobaConfigHeadlessSync] Fatal: {e}");
                exitCode = 1;
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
            }
            else
            {
                Debug.Log($"[MobaConfigHeadlessSync] Done. exit={exitCode}");
            }
        }

        private static int PushJson(List<MobaConfigTableAssetSO> tables, string excelFolder)
        {
            var failures = new List<string>();
            var conflicts = new List<string>();

            foreach (var table in tables)
            {
                var name = table.GetType().Name;
                try
                {
                    if (IsExcelExcluded(table))
                    {
                        // 该表不进 Excel：仅做 JSON ⇄ SO 同步。
                        var excludedFolderDir = ToAbsolute(Path.Combine(ResourcesMobaFolder, table.FileWithoutExt));
                        if (!Directory.Exists(excludedFolderDir) || Directory.GetFiles(excludedFolderDir, "*.json").Length == 0)
                        {
                            MobaConfigJsonFolderSync.ExportFromReplacing(table);
                        }

                        MobaConfigJsonFolderSync.ImportFolderReplacing(table);
                        MobaConfigJsonFolderSync.ImportFolderToArrayJson(table);
                        Debug.Log($"[Headless][push] {name}: ok (JSON⇄SO only, Excel excluded)");
                        continue;
                    }

                    // 1) 编辑面（逐条目文件夹）不存在时先从 SO 引导生成。
                    var folderDir = ToAbsolute(Path.Combine(ResourcesMobaFolder, table.FileWithoutExt));
                    if (!Directory.Exists(folderDir) || Directory.GetFiles(folderDir, "*.json").Length == 0)
                    {
                        Debug.Log($"[Headless][push] {name}: per-entry folder missing, bootstrapping from SO -> {folderDir}");
                        MobaConfigJsonFolderSync.ExportFromReplacing(table);
                    }

                    // 2) 逐条目 JSON → SO（整表替换：文件夹是 AI 的全量快照，删除文件即删除条目；
                    //    Excel 侧的三方合并会在下一步拦截"本地删除 vs 远端修改"的分歧）。
                    MobaConfigJsonFolderSync.ImportFolderReplacing(table);

                    // 3) SO → Excel。已接入走三方合并；首次接入从 SO 全量建 Excel+baseline。
                    var excelPath = ToAbsolute(Path.Combine(excelFolder, MobaExcelSync.ExcelFileNameFor(table)));
                    if (!File.Exists(excelPath))
                    {
                        Debug.Log($"[Headless][push] {name}: excel missing, bootstrapping from SO -> {excelPath}");
                        ScriptableObjectExcelSync.BootstrapExcelFromSo(table, excelPath, MobaExcelSync.DefaultOptions(table.FileWithoutExt), new EpplusTableReaderWriterFactory());
                    }
                    else
                    {
                        MobaExcelSync.ExportTable(table, excelPath);
                    }

                    // 4) 刷新运行时数组 JSON（从编辑面文件夹重导）。
                    MobaConfigJsonFolderSync.ImportFolderToArrayJson(table);

                    Debug.Log($"[Headless][push] {name}: ok");
                }
                catch (InvalidOperationException e) when (e.Message != null && e.Message.IndexOf("conflict", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    conflicts.Add($"{name}: {e.Message}");
                }
                catch (Exception e)
                {
                    failures.Add($"{name}: {e.Message}");
                }
            }

            return Report("push-json", tables.Count, failures, conflicts);
        }

        private static int PullExcel(List<MobaConfigTableAssetSO> tables, string excelFolder)
        {
            var failures = new List<string>();
            var skipped = new List<string>();

            foreach (var table in tables)
            {
                var name = table.GetType().Name;
                if (IsExcelExcluded(table))
                {
                    MobaConfigJsonFolderSync.ExportFromReplacing(table);
                    MobaConfigJsonFolderSync.ImportFolderToArrayJson(table);
                    Debug.Log($"[Headless][pull] {name}: ok (JSON⇄SO only, Excel excluded)");
                    continue;
                }

                var excelPath = ToAbsolute(Path.Combine(excelFolder, MobaExcelSync.ExcelFileNameFor(table)));
                if (!File.Exists(excelPath))
                {
                    skipped.Add($"{name}: no excel at {excelPath} (run bootstrap first)");
                    continue;
                }

                try
                {
                    // 1) Excel → SO（刷新 baseline）。
                    MobaExcelSync.ImportTable(table, excelPath);

                    // 2) SO → 逐条目文件夹（AI 编辑面，整表快照：孤儿文件一并清理）。
                    MobaConfigJsonFolderSync.ExportFromReplacing(table);

                    // 3) 文件夹 → 数组 JSON（运行时）。
                    MobaConfigJsonFolderSync.ImportFolderToArrayJson(table);

                    Debug.Log($"[Headless][pull] {name}: ok");
                }
                catch (Exception e)
                {
                    failures.Add($"{name}: {e.Message}");
                }
            }

            foreach (var s in skipped)
            {
                Debug.LogWarning($"[Headless][pull] skipped {s}");
            }

            return Report("pull-excel", tables.Count, failures, null);
        }

        private static int Bootstrap(List<MobaConfigTableAssetSO> tables, string excelFolder)
        {
            var failures = new List<string>();

            foreach (var table in tables)
            {
                var name = table.GetType().Name;
                try
                {
                    if (IsExcelExcluded(table))
                    {
                        Debug.Log($"[Headless][bootstrap] {name}: excluded from Excel chain (uses JSON→Def)");
                        continue;
                    }

                    var excelPath = ToAbsolute(Path.Combine(excelFolder, MobaExcelSync.ExcelFileNameFor(table)));
                    if (!File.Exists(excelPath))
                    {
                        ScriptableObjectExcelSync.BootstrapExcelFromSo(table, excelPath, MobaExcelSync.DefaultOptions(table.FileWithoutExt), new EpplusTableReaderWriterFactory());
                        Debug.Log($"[Headless][bootstrap] {name}: created {excelPath} + baseline");
                    }
                    else
                    {
                        Debug.Log($"[Headless][bootstrap] {name}: excel already exists, skipped");
                    }

                    var folderDir = ToAbsolute(Path.Combine(ResourcesMobaFolder, table.FileWithoutExt));
                    if (!Directory.Exists(folderDir) || Directory.GetFiles(folderDir, "*.json").Length == 0)
                    {
                        MobaConfigJsonFolderSync.ExportFromReplacing(table);
                        Debug.Log($"[Headless][bootstrap] {name}: created per-entry folder {folderDir}");
                    }
                }
                catch (Exception e)
                {
                    failures.Add($"{name}: {e.Message}");
                }
            }

            return Report("bootstrap", tables.Count, failures, null);
        }

        private static int ExportTyped(List<MobaConfigTableAssetSO> tables, string excelFolder)
        {
            var failures = new List<string>();

            foreach (var table in tables)
            {
                var name = table.GetType().Name;
                try
                {
                    var excelPath = ToAbsolute(Path.Combine(excelFolder, MobaExcelSync.ExcelFileNameFor(table)));
                    if (File.Exists(excelPath))
                    {
                        Debug.Log($"[Headless][export-typed] {name}: excel already exists, skipped");
                        continue;
                    }

                    // 用与 DefaultOptions 一致的布局：表头第 6 行、类型第 7 行、数据第 8 行起（Luban/Wizard 约定）。
                    ScriptableObjectExcelSync.BootstrapExcelFromSo(
                        table, excelPath, MobaExcelSync.DefaultOptions(table.FileWithoutExt), new EpplusTableReaderWriterFactory(),
                        DefaultExcelTypeNameProvider.Instance);
                    Debug.Log($"[Headless][export-typed] {name}: typed excel written to {excelPath} + baseline established");
                }
                catch (Exception e)
                {
                    failures.Add($"{name}: {e.Message}");
                }
            }

            return Report("export-typed", tables.Count, failures, null);
        }

        private static int SeedFromJson(List<MobaConfigTableAssetSO> tables, string excelFolder)
        {
            var failures = new List<string>();

            foreach (var table in tables)
            {
                var name = table.GetType().Name;
                try
                {
                    // 1) 数组 JSON（长期真相源）→ 逐条目文件夹。
                    var arrayJsonPath = ToAbsolute(Path.Combine(ResourcesMobaFolder, table.FileWithoutExt + ".json"));
                    if (!File.Exists(arrayJsonPath))
                    {
                        failures.Add($"{name}: array json not found at {arrayJsonPath}");
                        continue;
                    }
                    MobaConfigJsonFolderSync.ExportArrayJsonToFolder(table);

                    // 2) 文件夹 → SO（整表替换：SO 从 JSON 重建）。
                    MobaConfigJsonFolderSync.ImportFolderReplacing(table);

                    // 3) SO → 带类型 Excel + baseline（正式化；已存在则跳过不覆盖）。
                    var excelPath = ToAbsolute(Path.Combine(excelFolder, MobaExcelSync.ExcelFileNameFor(table)));
                    if (File.Exists(excelPath))
                    {
                        Debug.Log($"[Headless][seed-from-json] {name}: excel already exists, skipped formalization");
                    }
                    else
                    {
                        ScriptableObjectExcelSync.BootstrapExcelFromSo(
                            table, excelPath, MobaExcelSync.DefaultOptions(table.FileWithoutExt), new EpplusTableReaderWriterFactory(),
                            DefaultExcelTypeNameProvider.Instance);
                    }

                    Debug.Log($"[Headless][seed-from-json] {name}: ok");
                }
                catch (Exception e)
                {
                    failures.Add($"{name}: {e.Message}");
                }
            }

            return Report("seed-from-json", tables.Count, failures, null);
        }

        private static int MigrateFlows(List<MobaConfigTableAssetSO> tables, string excelFolder)
        {
            var failures = new List<string>();

            foreach (var table in tables)
            {
                var name = table.GetType().Name;
                try
                {
                    if (!(table is SkillFlowSO flowSo))
                    {
                        failures.Add($"{name}: migrate-flows only supports SkillFlowSO");
                        continue;
                    }

                    // 从数组 JSON（真相）读 DTO，迁移成富作者形态 Def 填 dataList。
                    var arrayJsonPath = ToAbsolute(Path.Combine(ResourcesMobaFolder, table.FileWithoutExt + ".json"));
                    if (!File.Exists(arrayJsonPath))
                    {
                        failures.Add($"{name}: array json not found at {arrayJsonPath}");
                        continue;
                    }

                    var dtos = JsonConvert.DeserializeObject<SkillFlowDTO[]>(File.ReadAllText(arrayJsonPath));
                    if (dtos == null)
                    {
                        failures.Add($"{name}: failed to deserialize {arrayJsonPath}");
                        continue;
                    }

                    var defs = new List<SkillFlowDef>(dtos.Length);
                    foreach (var dto in dtos)
                    {
                        var def = SkillFlowDef.FromDto(dto);
                        if (def != null) defs.Add(def);
                    }

                    flowSo.dataList = defs.ToArray();
                    // legacyDataList 是递归 DTO，会触发 Unity 序列化深度告警；迁移到 Def 后清空以消除该告警。
                    flowSo.legacyDataList = Array.Empty<SkillFlowDTO>();
                    EditorUtility.SetDirty(flowSo);
                    AssetDatabase.SaveAssets();

                    Debug.Log($"[Headless][migrate-flows] {name}: migrated {defs.Count}/{dtos.Length} flows into dataList (Def)");
                }
                catch (Exception e)
                {
                    failures.Add($"{name}: {e.Message}");
                }
            }

            return Report("migrate-flows", tables.Count, failures, null);
        }

        private static int PrintStatus(List<MobaConfigTableAssetSO> tables, string excelFolder)
        {
            Debug.Log($"[Headless][status] {tables.Count} table(s). excelFolder={excelFolder}");
            foreach (var table in tables)
            {
                var soPath = AssetDatabase.GetAssetPath(table);
                var excelPath = ToAbsolute(Path.Combine(excelFolder, MobaExcelSync.ExcelFileNameFor(table)));
                var baselineExists = File.Exists(ToAbsolute(soPath + ".excelBaseline.asset"));
                var folderDir = ToAbsolute(Path.Combine(ResourcesMobaFolder, table.FileWithoutExt));
                var folderCount = Directory.Exists(folderDir) ? Directory.GetFiles(folderDir, "*.json").Length : 0;
                var arrayPath = ToAbsolute(Path.Combine(ResourcesMobaFolder, table.FileWithoutExt + ".json"));
                var soCount = 0;
                var entries = table.GetEntries();
                if (entries != null)
                {
                    foreach (var _ in entries)
                    {
                        soCount++;
                    }
                }

                Debug.Log(
                    $"[Headless][status] {table.GetType().Name,-32} file={table.FileWithoutExt,-28} so={soCount,4} " +
                    $"excel={(File.Exists(excelPath) ? "Y" : "n")} baseline={(baselineExists ? "Y" : "n")} " +
                    $"folderJson={folderCount,4} arrayJson={(File.Exists(arrayPath) ? "Y" : "n")}");
            }

            return 0;
        }

        private static List<MobaConfigTableAssetSO> LoadTables(string tableFilter)
        {
            var result = new List<MobaConfigTableAssetSO>();
            var allNames = new List<string>();
            foreach (var t in MobaConfigTableRegistry.TableAssetTypes)
            {
                var guids = AssetDatabase.FindAssets($"t:{t.Name}");
                if (guids.Length == 0)
                {
                    continue;
                }

                if (guids.Length > 1)
                {
                    Debug.LogWarning($"[MobaConfigHeadlessSync] Multiple {t.Name} assets found, using the first.");
                }

                var asset = AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guids[0]), t) as MobaConfigTableAssetSO;
                if (asset == null)
                {
                    continue;
                }

                allNames.Add($"{asset.GetType().Name}({asset.FileWithoutExt})");
                if (!string.IsNullOrEmpty(tableFilter) && !MatchesFilter(asset, tableFilter))
                {
                    continue;
                }

                result.Add(asset);
            }

            if (!string.IsNullOrEmpty(tableFilter) && result.Count == 0)
            {
                Debug.LogWarning($"[MobaConfigHeadlessSync] Filter '{tableFilter}' matched no table. Available: {string.Join(", ", allNames)}");
            }

            return result;
        }

        private static bool MatchesFilter(MobaConfigTableAssetSO table, string filter)
        {
            var typeName = table.GetType().Name;
            if (string.Equals(typeName, filter, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // 允许去掉 "SO" 后缀的类型名（如 "Buff" 匹配 "BuffSO"）。
            if (typeName.EndsWith("SO", StringComparison.OrdinalIgnoreCase)
                && string.Equals(typeName.Substring(0, typeName.Length - 2), filter, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // 允许按 FileWithoutExt（如 "buffs"）匹配。
            return string.Equals(table.FileWithoutExt, filter, StringComparison.OrdinalIgnoreCase);
        }

        private static int Report(string mode, int total, List<string> failures, List<string> conflicts)
        {
            if (conflicts != null && conflicts.Count > 0)
            {
                Debug.LogError($"[Headless][{mode}] CONFLICTS ({conflicts.Count}/{total}):\n" + string.Join("\n", conflicts));
                Debug.LogError("[Headless] Excel 是真相源：解决冲突报告（*.conflicts.json，Local/Remote 取舍后重改 JSON 或 Excel）后再重跑。");
                return 2;
            }

            if (failures.Count > 0)
            {
                Debug.LogError($"[Headless][{mode}] FAILURES ({failures.Count}/{total}):\n" + string.Join("\n", failures));
                return 1;
            }

            Debug.Log($"[Headless][{mode}] all {total} table(s) ok");
            return 0;
        }

        private static string GetArg(string[] args, string name)
        {
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return null;
        }

        private static string ToAbsolute(string projectRelativePath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", projectRelativePath));
        }
    }
}
#endif
