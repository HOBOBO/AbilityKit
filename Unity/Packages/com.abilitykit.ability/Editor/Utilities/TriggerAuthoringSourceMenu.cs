#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using AbilityKit.Ability.Config.Authoring;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace AbilityKit.Ability.Editor.Utilities
{
    internal static class TriggerAuthoringSourceMenu
    {
        private const string ExportMenu = "Assets/AbilityKit/触发器编辑/导出 Source JSON";
        private const string ExportRuntimeMenu = "Assets/AbilityKit/触发器编辑/导出 Runtime Plan JSON";
        private const string ImportMenu = "Assets/AbilityKit/触发器编辑/导入 Source JSON";
        private const string ValidateMenu = "Assets/AbilityKit/触发器编辑/校验";
        private const string ExportProjectRuntimeMenu = "Assets/AbilityKit/触发器编辑/导出项目 Runtime Plan";
        private const string ExportSchemasMenu = "Tools/AbilityKit/触发器编辑/导出 Source Schema";
        private const string Benchmark1000Menu = "Tools/AbilityKit/触发器编辑/规模基准/1000 条";
        private const string Benchmark5000Menu = "Tools/AbilityKit/触发器编辑/规模基准/5000 条";

        [MenuItem(Benchmark1000Menu)]
        private static void Benchmark1000()
        {
            TriggerAuthoringScaleBenchmark.RunInteractive(1000);
        }

        [MenuItem(Benchmark5000Menu)]
        private static void Benchmark5000()
        {
            TriggerAuthoringScaleBenchmark.RunInteractive(5000);
        }

        [MenuItem(ExportMenu)]
        private static void Export()
        {
            var asset = Selection.activeObject as TriggerAuthoringModuleAsset;
            if (asset == null)
            {
                ExportTemplate();
                return;
            }

            var path = asset.SourceJsonPath;
            if (string.IsNullOrWhiteSpace(path))
            {
                var defaultName = asset.Module != null && !string.IsNullOrWhiteSpace(asset.Module.ModuleId)
                    ? asset.Module.ModuleId
                    : asset.name;
                path = EditorUtility.SaveFilePanel(
                    "导出触发器 Source JSON", Application.dataPath, defaultName,
                    TriggerSourceCodecs.ModuleDefault.FileExtension);
                if (string.IsNullOrWhiteSpace(path)) return;
            }

            var result = TriggerAuthoringSourceSync.Export(asset, path);
            if (!result.Success && result.CanForce && EditorUtility.DisplayDialog(
                    "触发器源文件冲突",
                    result.Message + "\n\n是否强制导出并覆盖 Source JSON？",
                    "强制导出",
                    "取消"))
            {
                result = TriggerAuthoringSourceSync.Export(asset, path, true);
            }

            ShowResult("导出", result);
            if (result.Success) AssetDatabase.SaveAssets();
        }

        [MenuItem(ExportRuntimeMenu)]
        private static void ExportRuntime()
        {
            var asset = Selection.activeObject as TriggerAuthoringModuleAsset;
            if (asset == null) return;

            var defaultName = asset.Module != null && !string.IsNullOrWhiteSpace(asset.Module.ModuleId)
                ? asset.Module.ModuleId + ".runtime"
                : asset.name + ".runtime";
            var path = EditorUtility.SaveFilePanel("导出 Runtime Plan JSON", Application.dataPath, defaultName, "json");
            if (string.IsNullOrWhiteSpace(path)) return;

            var result = TriggerAuthoringRuntimeExporter.Export(asset, path);
            if (result.Success)
            {
                Debug.Log($"[TriggerAuthoring] Runtime Plan export succeeded. path='{path}', {result.BuildMessage()}");
                AssetDatabase.Refresh();
                return;
            }

            var message = result.BuildMessage();
            Debug.LogError("[TriggerAuthoring] Runtime Plan export failed. " + message);
            EditorUtility.DisplayDialog("Runtime Plan 导出失败", message, "确定");
        }

        [MenuItem(ImportMenu)]
        private static void Import()
        {
            var asset = Selection.activeObject as TriggerAuthoringModuleAsset;
            if (asset == null)
            {
                ImportTemplate();
                return;
            }

            var path = asset.SourceJsonPath;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                path = EditorUtility.OpenFilePanel(
                    "导入触发器 Source JSON", Application.dataPath,
                    TriggerSourceCodecs.ModuleDefault.FileExtension);
                if (string.IsNullOrWhiteSpace(path)) return;
            }

            var preview = TriggerAuthoringSourceSync.PreviewImport(asset, path);
            if (!TriggerAuthoringSourceImportPreviewDialog.Confirm(preview)) return;

            var result = TriggerAuthoringSourceSync.Import(asset, path, preview.RequiresForce);
            if (!result.Success && result.CanForce && EditorUtility.DisplayDialog(
                    "触发器资产冲突",
                    result.Message + "\n\n是否强制导入并覆盖资产内容？",
                    "强制导入",
                    "取消"))
                result = TriggerAuthoringSourceSync.Import(asset, path, true);

            ShowResult("导入", result);
            if (result.Success) AssetDatabase.SaveAssets();
        }

        [MenuItem(ExportProjectRuntimeMenu)]
        private static void ExportProjectRuntime()
        {
            var project = Selection.activeObject as TriggerAuthoringProjectAsset;
            if (project == null) return;
            var result = TriggerAuthoringProjectExport.ExportAll(project);
            var message = "[TriggerAuthoring] Project runtime export " +
                          (result.Success ? "succeeded. " : "failed. ") + result.BuildMessage();
            if (result.Success) Debug.Log(message, project);
            else Debug.LogError(message, project);
            EditorUtility.DisplayDialog("项目运行时导出", result.BuildMessage(), "确定");
        }

        [MenuItem(ExportProjectRuntimeMenu, true)]
        private static bool CanExportProjectRuntime()
        {
            return Selection.activeObject is TriggerAuthoringProjectAsset;
        }

        [MenuItem(ExportSchemasMenu)]
        private static void ExportSchemas()
        {
            var directory = EditorUtility.OpenFolderPanel(
                "导出触发器 Source Schema",
                Application.dataPath,
                string.Empty);
            if (string.IsNullOrWhiteSpace(directory)) return;

            var result = TriggerAuthoringSourceSchema.ExportAll(directory);
            AssetDatabase.Refresh();
            Debug.Log(
                $"[TriggerAuthoring] Source schema export completed. directory='{result.DirectoryPath}', " +
                $"written={result.WrittenPaths.Count}, unchanged={result.UnchangedPaths.Count}.");
            EditorUtility.DisplayDialog(
                "触发器 Source Schema 导出",
                $"共导出 {result.TotalCount} 个 Schema 文件。\n已写入：{result.WrittenPaths.Count}\n未变化：{result.UnchangedPaths.Count}",
                "确定");
        }

        [MenuItem(ValidateMenu)]
        private static void Validate()
        {
            var asset = Selection.activeObject as TriggerAuthoringModuleAsset;
            if (asset == null)
            {
                ValidateTemplate();
                return;
            }
            var diagnostics = TriggerAuthoringValidator.Validate(
                asset.Module,
                TriggerAuthoringValidationContext.Create(asset));
            if (diagnostics.Count == 0)
            {
                EditorUtility.DisplayDialog("触发器数据校验", "暂无诊断。", "确定");
                return;
            }

            var message = string.Empty;
            for (var i = 0; i < diagnostics.Count; i++)
            {
                var diagnostic = diagnostics[i];
                message += $"{diagnostic.Severity} {diagnostic.Code} {diagnostic.Path}: {diagnostic.Message}\n";
            }
            EditorUtility.DisplayDialog("触发器数据校验", message, "确定");
        }

        [MenuItem(ExportMenu, true)]
        [MenuItem(ImportMenu, true)]
        [MenuItem(ValidateMenu, true)]
        private static bool ValidateSelection()
        {
            return Selection.activeObject is TriggerAuthoringModuleAsset ||
                   Selection.activeObject is TriggerAuthoringTemplateAsset;
        }

        [MenuItem(ExportRuntimeMenu, true)]
        private static bool ValidateRuntimeSelection()
        {
            return Selection.activeObject is TriggerAuthoringModuleAsset;
        }

        private static void ExportTemplate()
        {
            var asset = Selection.activeObject as TriggerAuthoringTemplateAsset;
            if (asset == null) return;
            var path = asset.SourceJsonPath;
            if (string.IsNullOrWhiteSpace(path))
            {
                var defaultName = asset.Template != null && !string.IsNullOrWhiteSpace(asset.Template.TemplateId)
                    ? asset.Template.TemplateId
                    : asset.name;
                path = EditorUtility.SaveFilePanel(
                    "导出触发器模板 Source JSON", Application.dataPath, defaultName,
                    TriggerSourceCodecs.TemplateDefault.FileExtension);
                if (string.IsNullOrWhiteSpace(path)) return;
            }
            var result = TriggerAuthoringTemplateSourceSync.Export(asset, path);
            if (!result.Success && result.CanForce && EditorUtility.DisplayDialog(
                    "触发器模板源文件冲突",
                    result.Message + "\n\n是否强制导出并覆盖 Source JSON？",
                    "强制导出",
                    "取消"))
                result = TriggerAuthoringTemplateSourceSync.Export(asset, path, true);
            ShowResult("模板导出", result);
            if (result.Success) AssetDatabase.SaveAssets();
        }

        private static void ImportTemplate()
        {
            var asset = Selection.activeObject as TriggerAuthoringTemplateAsset;
            if (asset == null) return;
            var path = asset.SourceJsonPath;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                path = EditorUtility.OpenFilePanel(
                    "导入触发器模板 Source JSON", Application.dataPath,
                    TriggerSourceCodecs.TemplateDefault.FileExtension);
                if (string.IsNullOrWhiteSpace(path)) return;
            }
            var preview = TriggerAuthoringTemplateSourceSync.PreviewImport(asset, path);
            if (!TriggerAuthoringSourceImportPreviewDialog.Confirm(preview)) return;

            var result = TriggerAuthoringTemplateSourceSync.Import(asset, path, preview.RequiresForce);
            if (!result.Success && result.CanForce && EditorUtility.DisplayDialog(
                    "触发器模板资产冲突",
                    result.Message + "\n\n是否强制导入并覆盖资产内容？",
                    "强制导入",
                    "取消"))
                result = TriggerAuthoringTemplateSourceSync.Import(asset, path, true);
            ShowResult("模板导入", result);
            if (result.Success) AssetDatabase.SaveAssets();
        }

        private static void ValidateTemplate()
        {
            var asset = Selection.activeObject as TriggerAuthoringTemplateAsset;
            if (asset == null) return;
            var diagnostics = TriggerAuthoringTemplateValidator.Validate(
                asset.Template,
                TriggerAuthoringValidationContext.Create(asset));
            var message = diagnostics.Count == 0 ? "暂无诊断。" : TriggerAuthoringTemplateValidator.BuildMessage(diagnostics);
            EditorUtility.DisplayDialog("触发器模板校验", message, "确定");
        }

        private static void ShowResult(string operation, TriggerAuthoringSyncResult result)
        {
            if (result.Success)
            {
                Debug.Log($"[TriggerAuthoring] {operation} succeeded. hash={result.ContentHash}");
                return;
            }
            Debug.LogError($"[TriggerAuthoring] {operation} failed. state={result.State}, message={result.Message}");
            EditorUtility.DisplayDialog($"触发器源文件{operation}失败", result.Message, "确定");
        }
    }

    [Serializable]
    internal sealed class TriggerAuthoringScaleBenchmarkStage
    {
        public string Name;
        public double ElapsedMilliseconds;
        public int ItemCount;
    }

    [Serializable]
    internal sealed class TriggerAuthoringScaleBenchmarkReport
    {
        public string TimestampUtc;
        public string UnityVersion;
        public string OperatingSystem;
        public string Processor;
        public int ProcessorCount;
        public int SystemMemoryMb;
        public int TriggerCount;
        public int ActionsPerTrigger;
        public int NodeCount;
        public int ValidationDiagnosticCount;
        public int ValidationErrorCount;
        public int IndexDiagnosticCount;
        public int SourceJsonBytes;
        public int RuntimeJsonBytes;
        public bool RuntimeBuildSuccess;
        public int RuntimeDiagnosticCount;
        public int RuntimeErrorCount;
        public string RuntimeBuildMessage;
        public string ReportPath;
        public List<TriggerAuthoringScaleBenchmarkStage> Stages =
            new List<TriggerAuthoringScaleBenchmarkStage>();

        public TriggerAuthoringScaleBenchmarkStage FindStage(string name)
        {
            return Stages.Find(stage => string.Equals(stage.Name, name, StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// 可重复的编辑器规模基准。生成数据只存在于内存，报告写入 Library，不创建项目资产。
    /// </summary>
    internal static class TriggerAuthoringScaleBenchmark
    {
        internal const int DefaultActionsPerTrigger = 20;
        private const string ReportDirectory = "Library/AbilityKit/TriggerAuthoringBenchmarks";
        private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(false);

        public static void RunInteractive(int triggerCount)
        {
            var nodeCount = triggerCount * (DefaultActionsPerTrigger + 1);
            if (!EditorUtility.DisplayDialog(
                    "触发器规模基准",
                    $"将以内存数据运行 {triggerCount:N0} 条触发器、{nodeCount:N0} 个行为节点的同步基准。\n\n" +
                    "期间 Unity 编辑器可能暂时无响应，结果将写入 Library。是否继续？",
                    "运行",
                    "取消"))
                return;

            try
            {
                var report = Run(
                    triggerCount,
                    DefaultActionsPerTrigger,
                    true,
                    (phase, progress) => EditorUtility.DisplayProgressBar(
                        "触发器规模基准",
                        phase,
                        progress));
                var summary = BuildSummary(report);
                Debug.Log("[TriggerAuthoring Benchmark] " + summary.Replace("\n", "; "));
                EditorUtility.DisplayDialog("触发器规模基准完成", summary, "确定");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("触发器规模基准失败", exception.Message, "确定");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        public static void Run1000Batch()
        {
            RunBatch(1000);
        }

        public static void Run5000Batch()
        {
            RunBatch(5000);
        }

        internal static TriggerAuthoringScaleBenchmarkReport Run(
            int triggerCount,
            int actionsPerTrigger = DefaultActionsPerTrigger,
            bool writeReport = true,
            Action<string, float> progress = null)
        {
            if (triggerCount <= 0) throw new ArgumentOutOfRangeException(nameof(triggerCount));
            if (actionsPerTrigger <= 0) throw new ArgumentOutOfRangeException(nameof(actionsPerTrigger));

            var report = CreateReport(triggerCount, actionsPerTrigger);
            var stageIndex = 0;
            const int stageCount = 14;
            TriggerAuthoringModuleData module = null;
            ReportProgress(progress, "生成确定性测试数据", stageIndex++, stageCount);
            Measure(report, "dataGeneration", () => module = CreateModule(triggerCount, actionsPerTrigger),
                value => value?.Triggers?.Count ?? 0);
            report.NodeCount = CountNodes(module);

            var context = new TriggerAuthoringValidationContext
            {
                Types = TriggerTypeDescriptorCatalog.CreateProjectDefaults()
            };
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            List<TriggerAuthoringDiagnostic> validation = null;
            ReportProgress(progress, "完整校验", stageIndex++, stageCount);
            Measure(report, "validation", () => validation = TriggerAuthoringValidator.Validate(module, context),
                value => value?.Count ?? 0);
            report.ValidationDiagnosticCount = validation.Count;
            report.ValidationErrorCount = CountDiagnostics(validation, TriggerAuthoringDiagnosticSeverity.Error);

            var indexDiagnostics = CreateIndexDiagnostics(triggerCount, validation);
            report.IndexDiagnosticCount = indexDiagnostics.Count;
            MeasureIndex(report, progress, "indexFlat", "构建平铺索引", stageIndex++, stageCount,
                module, indexDiagnostics, TriggerAuthoringTriggerGroupMode.Flat, string.Empty);
            MeasureIndex(report, progress, "indexEvent", "按事件分组", stageIndex++, stageCount,
                module, indexDiagnostics, TriggerAuthoringTriggerGroupMode.Event, string.Empty);
            MeasureIndex(report, progress, "indexGroupPath", "按业务分组", stageIndex++, stageCount,
                module, indexDiagnostics, TriggerAuthoringTriggerGroupMode.GroupPath, string.Empty);

            TriggerAuthoringTriggerIndex.PreparedSearchIndex preparedSearch = null;
            ReportProgress(progress, "准备嵌套搜索索引", stageIndex++, stageCount);
            Measure(report, "searchIndexPrepare", () => preparedSearch = TriggerAuthoringTriggerIndex.PrepareSearch(
                    module.Triggers,
                    indexDiagnostics),
                value => value?.Count ?? 0);
            var hitText = "Benchmark Trigger " + (triggerCount / 2).ToString("D6", CultureInfo.InvariantCulture);
            MeasureIndex(report, progress, "searchHit", "搜索命中项", stageIndex++, stageCount,
                module, indexDiagnostics, TriggerAuthoringTriggerGroupMode.Flat, hitText,
                TriggerAuthoringTriggerQuickFilter.All, preparedSearch);
            MeasureIndex(report, progress, "searchMiss", "搜索无命中项", stageIndex++, stageCount,
                module, indexDiagnostics, TriggerAuthoringTriggerGroupMode.Flat, "benchmark-no-match-sentinel",
                TriggerAuthoringTriggerQuickFilter.All, preparedSearch);
            MeasureIndex(report, progress, "filterErrors", "筛选错误项", stageIndex++, stageCount,
                module, indexDiagnostics, TriggerAuthoringTriggerGroupMode.Flat, string.Empty,
                TriggerAuthoringTriggerQuickFilter.Errors);
            MeasureIndex(report, progress, "filterWarnings", "筛选警告项", stageIndex++, stageCount,
                module, indexDiagnostics, TriggerAuthoringTriggerGroupMode.Flat, string.Empty,
                TriggerAuthoringTriggerQuickFilter.Warnings);

            string sourceJson = null;
            ReportProgress(progress, "编码 Source JSON", stageIndex++, stageCount);
            Measure(report, "sourceJsonSerialize", () => sourceJson = TriggerSourceCodecs.ModuleDefault.Serialize(
                    new TriggerAuthoringSourceDocument
                    {
                        Metadata = new TriggerAuthoringSourceMetadata
                        {
                            Author = "AbilityKit benchmark",
                            Description = "Deterministic trigger authoring scale benchmark"
                        },
                        Module = module
                    }),
                value => Encoding.UTF8.GetByteCount(value ?? string.Empty));
            report.SourceJsonBytes = Encoding.UTF8.GetByteCount(sourceJson);

            TriggerAuthoringRuntimeCompileResult runtimeResult = null;
            ReportProgress(progress, "构建 Runtime Plan（含再次校验）", stageIndex++, stageCount);
            Measure(report, "runtimePlanBuild", () => runtimeResult = TriggerAuthoringRuntimeExporter.Build(module, context),
                value => value?.ExportedTriggerCount ?? 0);
            report.RuntimeBuildSuccess = runtimeResult.Success;
            report.RuntimeDiagnosticCount = runtimeResult.Diagnostics.Count;
            report.RuntimeErrorCount = CountDiagnostics(
                runtimeResult.Diagnostics,
                TriggerAuthoringDiagnosticSeverity.Error);
            report.RuntimeBuildMessage = runtimeResult.BuildMessage();

            TriggerAuthoringRuntimeCompileResult prevalidatedRuntimeResult = null;
            ReportProgress(progress, "构建 Runtime Plan（复用校验）", stageIndex++, stageCount);
            Measure(
                report,
                "runtimePlanBuildPrevalidated",
                () => prevalidatedRuntimeResult = TriggerAuthoringRuntimeExporter.BuildPrevalidated(
                    module,
                    context,
                    validation),
                value => value?.ExportedTriggerCount ?? 0);
            if (!prevalidatedRuntimeResult.Success)
                throw new InvalidOperationException(prevalidatedRuntimeResult.BuildMessage());

            string runtimeJson = null;
            ReportProgress(progress, "编码 Runtime Plan JSON", stageIndex, stageCount);
            if (runtimeResult.Success)
            {
                Measure(report, "runtimePlanSerialize",
                    () => runtimeJson = TriggerAuthoringRuntimeExporter.Serialize(runtimeResult.Database),
                    value => Encoding.UTF8.GetByteCount(value ?? string.Empty));
                report.RuntimeJsonBytes = Encoding.UTF8.GetByteCount(runtimeJson);
            }
            else
            {
                report.Stages.Add(new TriggerAuthoringScaleBenchmarkStage
                {
                    Name = "runtimePlanSerialize",
                    ElapsedMilliseconds = 0d,
                    ItemCount = 0
                });
            }

            if (writeReport) WriteReport(report);
            ReportProgress(progress, "完成", stageCount, stageCount);
            return report;
        }

        internal static TriggerAuthoringModuleData CreateModule(
            int triggerCount,
            int actionsPerTrigger = DefaultActionsPerTrigger)
        {
            if (triggerCount <= 0) throw new ArgumentOutOfRangeException(nameof(triggerCount));
            if (actionsPerTrigger <= 0) throw new ArgumentOutOfRangeException(nameof(actionsPerTrigger));

            var module = new TriggerAuthoringModuleData
            {
                ModuleId = "benchmark.trigger_authoring.scale",
                DisplayName = "Trigger Authoring Scale Benchmark",
                Kind = TriggerModuleKind.Custom,
                Triggers = new List<TriggerDefinitionData>(triggerCount)
            };
            long nodeOrdinal = 1;
            for (var triggerIndex = 0; triggerIndex < triggerCount; triggerIndex++)
            {
                var actions = new TriggerNodeData
                {
                    NodeId = CreateNodeId(nodeOrdinal++),
                    Kind = TriggerNodeKind.Action,
                    Type = "seq",
                    Children = new List<TriggerNodeData>(actionsPerTrigger)
                };
                for (var actionIndex = 0; actionIndex < actionsPerTrigger; actionIndex++)
                {
                    actions.Children.Add(new TriggerNodeData
                    {
                        NodeId = CreateNodeId(nodeOrdinal++),
                        Kind = TriggerNodeKind.Action,
                        Type = "debug_log",
                        Arguments =
                        {
                            new TriggerArgumentData
                            {
                                Name = "message",
                                Value = new TriggerValueRefData
                                {
                                    Source = TriggerValueSource.Constant,
                                    Type = TriggerValueType.String,
                                    StringValue = "Benchmark action " +
                                                  actionIndex.ToString(CultureInfo.InvariantCulture)
                                }
                            }
                        }
                    });
                }

                module.Triggers.Add(new TriggerDefinitionData
                {
                    Id = triggerIndex + 1,
                    Name = "Benchmark Trigger " + triggerIndex.ToString("D6", CultureInfo.InvariantCulture),
                    GroupPath = "Benchmark/Group " + (triggerIndex % 32).ToString("D2", CultureInfo.InvariantCulture),
                    Tags = new List<string> { "benchmark", triggerIndex % 2 == 0 ? "even" : "odd" },
                    EntryMode = TriggerEntryMode.Event,
                    Event = "benchmark.event." + (triggerIndex % 16).ToString(CultureInfo.InvariantCulture),
                    Priority = triggerIndex % 10,
                    Actions = actions
                });
            }
            return module;
        }

        internal static int CountNodes(TriggerAuthoringModuleData module)
        {
            if (module?.Triggers == null) return 0;
            var count = 0;
            for (var i = 0; i < module.Triggers.Count; i++)
            {
                count += CountNodes(module.Triggers[i]?.Condition);
                count += CountNodes(module.Triggers[i]?.Actions);
            }
            return count;
        }

        private static int CountNodes(TriggerNodeData node)
        {
            if (node == null) return 0;
            var count = 1 + CountNodes(node.Condition);
            count += CountNodes(node.Children);
            count += CountNodes(node.ElseChildren);
            return count;
        }

        private static int CountNodes(IReadOnlyList<TriggerNodeData> nodes)
        {
            var count = 0;
            for (var i = 0; i < (nodes?.Count ?? 0); i++) count += CountNodes(nodes[i]);
            return count;
        }

        private static string CreateNodeId(long ordinal)
        {
            return "node_" + ordinal.ToString("x32", CultureInfo.InvariantCulture);
        }

        private static TriggerAuthoringScaleBenchmarkReport CreateReport(int triggerCount, int actionsPerTrigger)
        {
            return new TriggerAuthoringScaleBenchmarkReport
            {
                TimestampUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                UnityVersion = Application.unityVersion,
                OperatingSystem = SystemInfo.operatingSystem,
                Processor = SystemInfo.processorType,
                ProcessorCount = SystemInfo.processorCount,
                SystemMemoryMb = SystemInfo.systemMemorySize,
                TriggerCount = triggerCount,
                ActionsPerTrigger = actionsPerTrigger
            };
        }

        private static List<TriggerAuthoringDiagnostic> CreateIndexDiagnostics(
            int triggerCount,
            IReadOnlyList<TriggerAuthoringDiagnostic> validation)
        {
            var result = validation == null
                ? new List<TriggerAuthoringDiagnostic>()
                : new List<TriggerAuthoringDiagnostic>(validation);
            for (var i = 0; i < triggerCount; i += 100)
            {
                result.Add(new TriggerAuthoringDiagnostic(
                    "BENCH_ERROR",
                    TriggerAuthoringDiagnosticSeverity.Error,
                    "module.triggers[" + i.ToString(CultureInfo.InvariantCulture) + "].actions",
                    "Synthetic benchmark error"));
                if (i + 1 < triggerCount)
                {
                    result.Add(new TriggerAuthoringDiagnostic(
                        "BENCH_WARNING",
                        TriggerAuthoringDiagnosticSeverity.Warning,
                        "module.triggers[" + (i + 1).ToString(CultureInfo.InvariantCulture) + "].actions",
                        "Synthetic benchmark warning"));
                }
            }
            return result;
        }

        private static void MeasureIndex(
            TriggerAuthoringScaleBenchmarkReport report,
            Action<string, float> progress,
            string stageName,
            string displayName,
            int stageIndex,
            int stageCount,
            TriggerAuthoringModuleData module,
            IReadOnlyList<TriggerAuthoringDiagnostic> diagnostics,
            TriggerAuthoringTriggerGroupMode groupMode,
            string searchText,
            TriggerAuthoringTriggerQuickFilter quickFilter = TriggerAuthoringTriggerQuickFilter.All,
            TriggerAuthoringTriggerIndex.PreparedSearchIndex preparedSearch = null)
        {
            ReportProgress(progress, displayName, stageIndex, stageCount);
            Measure(
                report,
                stageName,
                () => TriggerAuthoringTriggerIndex.Build(
                    module.Triggers,
                    diagnostics,
                    null,
                    groupMode,
                    searchText,
                    quickFilter,
                    null,
                    preparedSearch),
                CountEntries);
        }

        private static int CountEntries(IReadOnlyList<TriggerAuthoringTriggerIndex.Group> groups)
        {
            var count = 0;
            for (var i = 0; i < (groups?.Count ?? 0); i++) count += groups[i].Entries.Count;
            return count;
        }

        private static int CountDiagnostics(
            IReadOnlyList<TriggerAuthoringDiagnostic> diagnostics,
            TriggerAuthoringDiagnosticSeverity severity)
        {
            var count = 0;
            for (var i = 0; i < (diagnostics?.Count ?? 0); i++)
                if (diagnostics[i].Severity == severity)
                    count++;
            return count;
        }

        private static T Measure<T>(
            TriggerAuthoringScaleBenchmarkReport report,
            string name,
            Func<T> operation,
            Func<T, int> count)
        {
            var stopwatch = Stopwatch.StartNew();
            var value = operation();
            stopwatch.Stop();
            report.Stages.Add(new TriggerAuthoringScaleBenchmarkStage
            {
                Name = name,
                ElapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds,
                ItemCount = count != null ? count(value) : 0
            });
            return value;
        }

        private static void WriteReport(TriggerAuthoringScaleBenchmarkReport report)
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var directory = Path.Combine(projectRoot, ReportDirectory.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(directory);
            var fileName = "trigger-authoring-" + report.TriggerCount.ToString(CultureInfo.InvariantCulture) + "-" +
                           DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".json";
            report.ReportPath = Path.Combine(directory, fileName);
            File.WriteAllText(
                report.ReportPath,
                JsonConvert.SerializeObject(report, Formatting.Indented) + Environment.NewLine,
                Utf8WithoutBom);
        }

        private static string BuildSummary(TriggerAuthoringScaleBenchmarkReport report)
        {
            var builder = new StringBuilder();
            builder.Append(report.TriggerCount.ToString("N0", CultureInfo.InvariantCulture))
                .Append(" 条触发器 / ")
                .Append(report.NodeCount.ToString("N0", CultureInfo.InvariantCulture))
                .AppendLine(" 个节点");
            for (var i = 0; i < report.Stages.Count; i++)
            {
                var stage = report.Stages[i];
                builder.Append(stage.Name).Append(": ")
                    .Append(stage.ElapsedMilliseconds.ToString("F1", CultureInfo.InvariantCulture))
                    .AppendLine(" ms");
            }
            builder.Append("Source / Runtime: ")
                .Append(FormatBytes(report.SourceJsonBytes)).Append(" / ")
                .AppendLine(FormatBytes(report.RuntimeJsonBytes));
            builder.Append("Runtime Plan: ").AppendLine(report.RuntimeBuildSuccess ? "成功" : "失败");
            builder.Append("报告: ").Append(report.ReportPath);
            return builder.ToString();
        }

        private static void RunBatch(int triggerCount)
        {
            var report = Run(triggerCount);
            Debug.Log("[TriggerAuthoring Benchmark] " + BuildSummary(report).Replace("\n", "; "));
            if (!report.RuntimeBuildSuccess)
                throw new InvalidOperationException(report.RuntimeBuildMessage);
        }

        private static string FormatBytes(int byteCount)
        {
            return (byteCount / (1024d * 1024d)).ToString("F2", CultureInfo.InvariantCulture) + " MB";
        }

        private static void ReportProgress(
            Action<string, float> progress,
            string phase,
            int stageIndex,
            int stageCount)
        {
            progress?.Invoke(phase, Mathf.Clamp01(stageIndex / (float)stageCount));
        }
    }
}
#endif
