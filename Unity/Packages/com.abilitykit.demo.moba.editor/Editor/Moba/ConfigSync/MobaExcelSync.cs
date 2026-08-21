#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.ExcelSync.Editor;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    /// <summary>
    /// 把 demo.moba 的配置表 SO（MobaConfigTableAssetSO，字段形态为 T[] dataList）桥接到 excel-sync，
    /// 实现 Excel ⇄ SO 双向同步。
    ///
    /// 定位：Excel 是落盘唯一来源，SO 是 Unity 侧派生产物（预览 / asset 引用载体）。
    /// 导入（Excel→SO）会建立 baseline；导出（SO→Excel）走 excel-sync 的三方合并，冲突时中止并写 .conflicts.json。
    ///
    /// 依赖 excel-sync 对集合成员的泛化解析（大小写不敏感 + 支持 T[] 数组）。
    /// 首个落地表：BuffSO / BuffDTO（其余表照此模式复制菜单项即可）。
    /// 批处理/CI 入口见 MobaConfigHeadlessSync（-executeMethod）。
    /// </summary>
    public static class MobaExcelSync
    {
        // 与 view.runtime 的 Configs/Moba 同目录树，Excel 落在其 Excel 子目录。
        public const string ExcelFolder = "Packages/com.abilitykit.demo.moba.view.runtime/Configs/Moba/Excel";

        [MenuItem("Tools/AbilityKit/Demos/Moba/Config Excel/Buff: Import Excel -> SO")]
        public static void ImportBuffExcelToSo()
        {
            Import<BuffSO>("buffs.xlsx");
        }

        [MenuItem("Tools/AbilityKit/Demos/Moba/Config Excel/Buff: Export SO -> Excel")]
        public static void ExportBuffSoToExcel()
        {
            Export<BuffSO>("buffs.xlsx");
        }

        [MenuItem("Tools/AbilityKit/Demos/Moba/Config Excel/Buff: Create Skeleton Excel")]
        public static void CreateBuffSkeletonExcel()
        {
            CreateSkeleton<BuffDTO>("buffs.xlsx");
        }

        /// <summary>表对应的 Excel 文件名：{FileWithoutExt}.xlsx（与 Resources 数组 JSON 同名规则）。</summary>
        public static string ExcelFileNameFor(MobaConfigTableAssetSO table)
        {
            return table.FileWithoutExt + ".xlsx";
        }

        /// <summary>
        /// 单表单 sheet 的规范布局：第 6 行表头（字段名）、第 7 行类型（export-typed 写出，日常 import/export 忽略）、
        /// 第 8 行起数据、主键列 Id。与 ExcelTableOptions 默认值及 excel-sync Wizard 的 Luban 读取约定（表头 6/类型 7）对齐。
        /// </summary>
        public static ExcelTableOptions DefaultOptions()
        {
            // SheetName 留空以始终使用首个工作表，避免 sheet 名不匹配时静默回退的歧义。
            return new ExcelTableOptions
            {
                SheetName = string.Empty,
                HeaderRowIndex = 6,
                DataStartRowIndex = 8,
                PrimaryKeyColumnName = "Id"
            };
        }

        /// <summary>Excel → SO（建立 baseline）。Excel 文件必须已存在。</summary>
        public static void Import<T>(string excelFileName, string excelFolder = ExcelFolder) where T : MobaConfigTableAssetSO
        {
            var table = FindTable<T>();
            ImportTable(table, ToAbsoluteExcelPath(excelFileName, excelFolder));
        }

        /// <summary>SO → Excel（三方合并）。要求已先 Import 建立 baseline。</summary>
        public static void Export<T>(string excelFileName, string excelFolder = ExcelFolder) where T : MobaConfigTableAssetSO
        {
            var table = FindTable<T>();
            ExportTable(table, ToAbsoluteExcelPath(excelFileName, excelFolder));
        }

        /// <summary>Excel → SO（非泛型核心，供 headless 批量驱动）。</summary>
        public static void ImportTable(MobaConfigTableAssetSO table, string excelPath)
        {
            if (!File.Exists(excelPath))
            {
                Debug.LogError($"[MobaExcelSync] Excel not found: {excelPath}");
                return;
            }

            ScriptableObjectExcelSync.ImportToSingleAssetDataList(
                table, excelPath, DefaultOptions(), new EpplusTableReaderWriterFactory());
            Debug.Log($"[MobaExcelSync] Imported {excelPath} into {table.GetType().Name}");
        }

        /// <summary>SO → Excel 三方合并（非泛型核心，供 headless 批量驱动）。冲突时抛异常并写 .conflicts.json。</summary>
        public static void ExportTable(MobaConfigTableAssetSO table, string excelPath)
        {
            if (!File.Exists(excelPath))
            {
                Debug.LogError($"[MobaExcelSync] Excel not found: {excelPath}. Run Import/Bootstrap first to create the file and baseline.");
                return;
            }

            ScriptableObjectExcelSync.ExportFromSingleAssetDataList(
                table, excelPath, DefaultOptions(), new EpplusTableReaderWriterFactory());
            Debug.Log($"[MobaExcelSync] Exported {table.GetType().Name} into {excelPath}");
        }

        /// <summary>按 DTO 的公开字段生成一个只有表头的空 Excel，用于首次引导。</summary>
        public static void CreateSkeleton<TDto>(string excelFileName, string excelFolder = ExcelFolder)
        {
            var excelPath = ToAbsoluteExcelPath(excelFileName, excelFolder);
            if (File.Exists(excelPath))
            {
                Debug.LogError($"[MobaExcelSync] Excel already exists: {excelPath}. Refusing to overwrite.");
                return;
            }

            var headers = new List<string>();
            foreach (var f in typeof(TDto).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                headers.Add(f.Name);
            }

            using (var writer = new EpplusTableReaderWriterFactory().CreateWriter(excelPath, DefaultOptions()))
            {
                writer.WriteHeaders(headers, 1);
                writer.Save();
            }

            AssetDatabase.Refresh();
            Debug.Log($"[MobaExcelSync] Created skeleton Excel ({headers.Count} columns): {excelPath}");
        }

        /// <summary>工程根相对路径（Packages/... 或 Assets/...）转绝对路径。</summary>
        public static string ToAbsoluteExcelPath(string excelFileName, string excelFolder)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", excelFolder, excelFileName));
        }

        private static T FindTable<T>() where T : MobaConfigTableAssetSO
        {
            var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null)
                {
                    return asset;
                }
            }

            throw new System.InvalidOperationException($"Cannot find a {typeof(T).Name} asset in the project.");
        }
    }
}
#endif
