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
        /// <summary>
        /// 落盘真相源目录：仓库根的 Luban 工程 Datas 目录。Excel 是唯一真相源，SO 与 JSON 都是它的投影。
        /// 注意 "../" 前缀——路径按 Application.dataPath（Unity/Assets）解析，需上溯到仓库根。
        /// </summary>
        public const string ExcelFolder = "../LubanConfig/Moba/MiniTemplate/Datas";

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
        /// Luban 数据表布局：R1 = ##var + 字段名、R2 = ##type + 类型、R3 = ## 注释行、R4 起数据，主键列 Id。
        /// A 列为 Luban 标记列，读取侧无需开关（标记列自然成为 headers[0]，列索引自洽）。
        /// </summary>
        public static ExcelTableOptions DefaultOptions()
        {
            return DefaultOptions(string.Empty);
        }

        /// <summary>同上，并指定 sheet 名（Luban 的 input=sheet名@文件名 需要与文件内 sheet 名一致）。</summary>
        public static ExcelTableOptions DefaultOptions(string sheetName)
        {
            return new ExcelTableOptions
            {
                SheetName = sheetName ?? string.Empty,
                HeaderRowIndex = 1,
                DataStartRowIndex = 4,
                PrimaryKeyColumnName = "Id",
                LubanMarkers = true
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
                table, excelPath, DefaultOptions(table.FileWithoutExt), new EpplusTableReaderWriterFactory());
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
                table, excelPath, DefaultOptions(table.FileWithoutExt), new EpplusTableReaderWriterFactory());
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

            var nameRow = new List<object> { "##var" };
            var typeRow = new List<object> { "##type" };
            foreach (var f in typeof(TDto).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                nameRow.Add(f.Name);
                typeRow.Add(LubanExcelTypeNameProvider.Instance.GetTypeName(f.FieldType));
            }

            using (var writer = new EpplusTableReaderWriterFactory().CreateWriter(excelPath, DefaultOptions()))
            {
                writer.WriteRow(1, nameRow);
                writer.WriteRow(2, typeRow);
                writer.Save();
            }

            AssetDatabase.Refresh();
            Debug.Log($"[MobaExcelSync] Created skeleton Excel ({nameRow.Count - 1} columns): {excelPath}");
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
