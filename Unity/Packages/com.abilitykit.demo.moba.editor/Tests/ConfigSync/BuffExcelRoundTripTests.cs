using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Editor;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.ExcelSync.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Demo.Moba.ConfigSync.Tests
{
    /// <summary>
    /// Buff 表 Excel ⇄ SO 垂直切片的往返验证：
    /// 1) CreateSkeleton 按 DTO 字段生成表头；
    /// 2) Excel → SO 导入（含 int[]/string[]/复杂对象数组列的解码 + baseline 建立）；
    /// 3) SO → Excel 安全导出（三方合并写回）；
    /// 4) 双方分歧时导出中止并产出冲突报告。
    /// 全程使用临时资产与临时 Excel，不触碰真实 BuffCO 资产与仓库内文件。
    /// 运行：tools/run-unity-editmode-tests.ps1 -TestAssembly AbilityKit.Demo.Moba.ConfigSync.Tests
    /// </summary>
    public sealed class BuffExcelRoundTripTests
    {
        private const string TempAssetFolder = "Assets/ConfigSyncTestsTmp";
        private const string TempSoAssetPath = TempAssetFolder + "/BuffRoundTrip.asset";

        private string _excelDir;
        private string _excelPath;

        [SetUp]
        public void SetUp()
        {
            _excelDir = Path.Combine(Application.temporaryCachePath, "AbilityKitConfigSyncTests");
            if (Directory.Exists(_excelDir))
            {
                Directory.Delete(_excelDir, true);
            }
            Directory.CreateDirectory(_excelDir);
            _excelPath = Path.Combine(_excelDir, "Buff.xlsx");

            if (!AssetDatabase.IsValidFolder(TempAssetFolder))
            {
                AssetDatabase.CreateFolder("Assets", Path.GetFileName(TempAssetFolder));
            }
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TempAssetFolder);
            if (Directory.Exists(_excelDir))
            {
                Directory.Delete(_excelDir, true);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [Test]
        public void CreateSkeleton_WritesAllDtoFieldHeaders()
        {
            MobaExcelSync.CreateSkeleton<BuffDTO>("Buff.xlsx", _excelDir);

            Assert.That(File.Exists(_excelPath), Is.True, "skeleton xlsx should be created");

            var expectedCount = typeof(BuffDTO).GetFields(BindingFlags.Public | BindingFlags.Instance).Length;
            var headers = ReadHeaders();
            Assert.That(headers, Has.Count.EqualTo(expectedCount), "header count should match BuffDTO public field count");
            Assert.That(headers, Does.Contain("Id"));
            Assert.That(headers, Does.Contain("OnAddEffects"));
            Assert.That(headers, Does.Contain("Modifiers"));
        }

        [Test]
        public void ExcelSoRoundTrip_Import_Export_Conflict()
        {
            var factory = new EpplusTableReaderWriterFactory();
            var headers = typeof(BuffDTO)
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Select(f => f.Name)
                .ToList();

            WriteRowA(factory, headers, durationMs: "5000", maxStacks: "1");
            using (var writer = factory.CreateWriter(_excelPath, NewOptions()))
            {
                writer.WriteRow(3, BuildRowValues(headers, RowBValues()));
                writer.Save();
            }

            var so = ScriptableObject.CreateInstance<BuffSO>();
            AssetDatabase.CreateAsset(so, TempSoAssetPath);

            // --- 1) Import：Excel → SO（建立 baseline） ---
            ScriptableObjectExcelSync.ImportToSingleAssetDataList(so, _excelPath, NewOptions(), factory);

            Assert.That(so.dataList, Is.Not.Null);
            Assert.That(so.dataList.Length, Is.EqualTo(2), "both data rows should be imported");

            var a = so.dataList[0];
            Assert.That(a.Id, Is.EqualTo(900001));
            Assert.That(a.Name, Is.EqualTo("TestBuffA"));
            Assert.That(a.DurationMs, Is.EqualTo(5000));
            Assert.That(a.OnAddEffects, Is.EqualTo(new[] { 101, 102 }), "int[] 列应按逗号分隔解码");
            Assert.That(a.TriggerIds, Is.EqualTo(new[] { 201, 202 }));
            Assert.That(a.TagNames, Is.EqualTo(new[] { "burn", "slow" }), "string[] 列应按逗号分隔解码");
            Assert.That(a.MaxStacks, Is.EqualTo(1));
            Assert.That(a.Modifiers, Is.Not.Null, "复杂对象数组应按 JSON 数组解码");
            Assert.That(a.Modifiers.Length, Is.EqualTo(1));
            Assert.That(a.Modifiers[0].TargetId, Is.EqualTo(33));
            Assert.That(a.Modifiers[0].Priority, Is.EqualTo(2));
            Assert.That(a.Modifiers[0].Value, Is.EqualTo(1.5f).Within(0.0001f));

            var baselinePath = TempSoAssetPath + ".excelBaseline.asset";
            Assert.That(File.Exists(baselinePath), Is.True, "Import 应建立 baseline 资产");

            // --- 2) Export：SO 改动 → 三方合并写回 Excel ---
            a.DurationMs = 6000;
            EditorUtility.SetDirty(so);
            ScriptableObjectExcelSync.ExportFromSingleAssetDataList(so, _excelPath, NewOptions(), factory);

            var exportedDuration = ReadCellValue(headers, "DurationMs", "900001");
            Assert.That(exportedDuration, Is.EqualTo("6000"), "SO 侧改动应写入 Excel");

            // --- 3) 冲突：Excel 与 SO 同时偏离 baseline，导出必须中止 ---
            // 策划侧只改 MaxStacks（1 -> 9），其余保持导出后的现状（DurationMs=6000）。
            WriteRowA(factory, headers, durationMs: "6000", maxStacks: "9");
            a.MaxStacks = 5; // AI/程序侧改同一格
            EditorUtility.SetDirty(so);

            var ex = Assert.Throws<InvalidOperationException>(
                () => ScriptableObjectExcelSync.ExportFromSingleAssetDataList(so, _excelPath, NewOptions(), factory),
                "三方合并检测到分歧时应中止导出");
            Assert.That(ex.Message, Does.Contain("conflict"), "异常消息应说明是冲突导致的中止");

            Assert.That(
                File.Exists(baselinePath + ".conflicts.json"),
                Is.True,
                "冲突时应产出 .conflicts.json 报告");
        }

        [Test]
        public void Export_DeletionPropagates_ToExcel()
        {
            var factory = new EpplusTableReaderWriterFactory();
            var headers = typeof(BuffDTO)
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Select(f => f.Name)
                .ToList();

            WriteRowA(factory, headers, durationMs: "5000", maxStacks: "1");
            using (var writer = factory.CreateWriter(_excelPath, NewOptions()))
            {
                writer.WriteRow(3, BuildRowValues(headers, RowBValues()));
                writer.Save();
            }

            var so = ScriptableObject.CreateInstance<BuffSO>();
            AssetDatabase.CreateAsset(so, TempSoAssetPath);
            ScriptableObjectExcelSync.ImportToSingleAssetDataList(so, _excelPath, NewOptions(), factory);
            Assert.That(so.dataList.Length, Is.EqualTo(2));

            // 本地删除 B（Excel 侧未改）→ 应安全传播为删行。
            so.dataList = new[] { so.dataList[0] };
            EditorUtility.SetDirty(so);
            ScriptableObjectExcelSync.ExportFromSingleAssetDataList(so, _excelPath, NewOptions(), factory);

            Assert.That(ReadCellValue(headers, "Name", "900002"), Is.Null, "被删除条目的行应从 Excel 移除");
            Assert.That(CountDataRows(), Is.EqualTo(1), "删除后应只剩一条数据行");
        }

        [Test]
        public void Export_LocalDelete_RemoteModify_Conflicts()
        {
            var factory = new EpplusTableReaderWriterFactory();
            var headers = typeof(BuffDTO)
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Select(f => f.Name)
                .ToList();

            WriteRowA(factory, headers, durationMs: "5000", maxStacks: "1");
            using (var writer = factory.CreateWriter(_excelPath, NewOptions()))
            {
                writer.WriteRow(3, BuildRowValues(headers, RowBValues()));
                writer.Save();
            }

            var so = ScriptableObject.CreateInstance<BuffSO>();
            AssetDatabase.CreateAsset(so, TempSoAssetPath);
            ScriptableObjectExcelSync.ImportToSingleAssetDataList(so, _excelPath, NewOptions(), factory);
            Assert.That(so.dataList.Length, Is.EqualTo(2));

            // 远端修改 B + 本地删除 B → 删除冲突，导出必须中止且不删行。
            using (var writer = factory.CreateWriter(_excelPath, NewOptions()))
            {
                writer.WriteHeaders(headers, 1);
                writer.WriteRow(2, BuildRowValues(headers, RowAValues("5000", "1")));
                writer.WriteRow(3, BuildRowValues(headers, RowBValues(name: "TestBuffB-modified")));
                writer.Save();
            }

            so.dataList = new[] { so.dataList[0] };
            EditorUtility.SetDirty(so);

            var ex = Assert.Throws<InvalidOperationException>(
                () => ScriptableObjectExcelSync.ExportFromSingleAssetDataList(so, _excelPath, NewOptions(), factory),
                "本地删除 vs 远端修改时应中止导出");
            Assert.That(ex.Message, Does.Contain("conflict"));
            Assert.That(File.Exists(TempSoAssetPath + ".excelBaseline.asset.conflicts.json"), Is.True, "应产出冲突报告");
        }

        [Test]
        public void FolderSync_ReplaceRoundTrip_DeletesOrphans()
        {
            var folderDir = Path.Combine(Application.temporaryCachePath, "AbilityKitConfigSyncTests", "foldersync", "buffs");
            if (Directory.Exists(folderDir))
            {
                Directory.Delete(folderDir, true);
            }
            Directory.CreateDirectory(folderDir);

            var so = ScriptableObject.CreateInstance<BuffSO>();
            so.dataList = new[]
            {
                new BuffDTO { Id = 900001, Name = "A", DurationMs = 1000 },
                new BuffDTO { Id = 900002, Name = "B", DurationMs = 2000 },
            };

            // 全量导出：两条 → 两个逐条目文件。
            MobaConfigJsonFolderSync.ExportFromReplacing(so, folderDir);
            Assert.That(Directory.GetFiles(folderDir, "*.json").Length, Is.EqualTo(2));

            // 本地删除 B 后重导出：孤儿文件应被清理，文件夹与 SO 快照一致。
            so.dataList = new[] { so.dataList[0] };
            MobaConfigJsonFolderSync.ExportFromReplacing(so, folderDir);
            var files = Directory.GetFiles(folderDir, "*.json");
            Assert.That(files.Length, Is.EqualTo(1), "被删条目的孤儿 JSON 应被清理");
            Assert.That(Path.GetFileName(files[0]), Does.Contain("900001"));

            // 整表替换导入：另一个 SO 从文件夹重建，应只有 1 条且字段正确。
            var so2 = ScriptableObject.CreateInstance<BuffSO>();
            AssetDatabase.CreateAsset(so2, TempSoAssetPath);
            MobaConfigJsonFolderSync.ImportFolderReplacing(so2, folderDir);
            Assert.That(so2.dataList, Is.Not.Null);
            Assert.That(so2.dataList.Length, Is.EqualTo(1), "文件夹快照重建后不应有残留条目");
            Assert.That(so2.dataList[0].Id, Is.EqualTo(900001));
            Assert.That(so2.dataList[0].Name, Is.EqualTo("A"));
            Assert.That(so2.dataList[0].DurationMs, Is.EqualTo(1000));
        }

        private static ExcelTableOptions NewOptions()
        {
            return new ExcelTableOptions
            {
                SheetName = string.Empty,
                HeaderRowIndex = 1,
                DataStartRowIndex = 2,
                PrimaryKeyColumnName = "Id"
            };
        }

        private static Dictionary<string, string> RowAValues(string durationMs, string maxStacks)
        {
            return new Dictionary<string, string>
            {
                { "Id", "900001" },
                { "Name", "TestBuffA" },
                { "DurationMs", durationMs },
                { "OnAddEffects", "101,102" },
                { "TriggerIds", "201,202" },
                { "TagNames", "burn,slow" },
                { "MaxStacks", maxStacks },
                { "Modifiers", "[{\"TargetId\":33,\"Value\":1.5,\"Priority\":2}]" },
            };
        }

        private static Dictionary<string, string> RowBValues(string name = "TestBuffB")
        {
            return new Dictionary<string, string>
            {
                { "Id", "900002" },
                { "Name", name },
                { "DurationMs", "3000" },
            };
        }

        private static List<object> BuildRowValues(List<string> headers, Dictionary<string, string> values)
        {
            var row = new List<object>(headers.Count);
            foreach (var h in headers)
            {
                row.Add(values.TryGetValue(h, out var v) ? v : string.Empty);
            }
            return row;
        }

        private void WriteRowA(EpplusTableReaderWriterFactory factory, List<string> headers, string durationMs, string maxStacks)
        {
            using (var writer = factory.CreateWriter(_excelPath, NewOptions()))
            {
                writer.WriteHeaders(headers, 1);
                writer.WriteRow(2, BuildRowValues(headers, RowAValues(durationMs, maxStacks)));
                writer.Save();
            }
        }

        private List<string> ReadHeaders()
        {
            var factory = new EpplusTableReaderWriterFactory();
            using (var reader = factory.CreateReader(_excelPath, NewOptions()))
            {
                return reader.GetHeaders().Where(h => !string.IsNullOrWhiteSpace(h)).ToList();
            }
        }

        /// <summary>IReadOnlyList 没有 FindIndex，用等价的显式循环（不区分大小写匹配表头）。</summary>
        private static int HeaderIndex(IReadOnlyList<string> headers, string name)
        {
            for (var i = 0; i < headers.Count; i++)
            {
                if (string.Equals(headers[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return -1;
        }

        private int CountDataRows()
        {
            var factory = new EpplusTableReaderWriterFactory();
            using (var reader = factory.CreateReader(_excelPath, NewOptions()))
            {
                var headers = reader.GetHeaders();
                var pkIndex = HeaderIndex(headers, "Id");
                if (pkIndex < 0)
                {
                    return 0;
                }

                var count = 0;
                foreach (var row in reader.ReadRows(2))
                {
                    if (pkIndex < row.Count && !string.IsNullOrWhiteSpace(row[pkIndex]?.ToString()))
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        private string ReadCellValue(List<string> headers, string columnName, string primaryKey)
        {
            var factory = new EpplusTableReaderWriterFactory();
            using (var reader = factory.CreateReader(_excelPath, NewOptions()))
            {
                var pkIndex = HeaderIndex(headers, "Id");
                var colIndex = HeaderIndex(headers, columnName);
                Assert.That(pkIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(colIndex, Is.GreaterThanOrEqualTo(0));

                foreach (var row in reader.ReadRows(2))
                {
                    if (colIndex < row.Count && pkIndex < row.Count
                        && string.Equals(row[pkIndex]?.ToString(), primaryKey))
                    {
                        return row[colIndex]?.ToString();
                    }
                }
            }

            return null;
        }
    }
}
