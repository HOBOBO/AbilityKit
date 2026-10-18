using AbilityKit.Entitas.CodeGen.Compare;
using AbilityKit.Entitas.CodeGen.Configuration;
using AbilityKit.Entitas.CodeGen.Diagnostics;
using AbilityKit.Entitas.CodeGen.Model;
using AbilityKit.Entitas.CodeGen.Output;
using AbilityKit.Entitas.CodeGen.Pipeline;

namespace AbilityKit.Entitas.CodeGen.Runner;

/// <summary>一次完整运行的选项（CLI 与测试共用）。</summary>
public sealed record RunOptions(
    string? ConfigPath = null,
    string? Project = null,
    bool Write = false,
    string? OutDir = null,
    bool UpdatePin = false,
    bool ValidateOnly = false,
    bool Verbose = false,
    bool ForceGeneratedDir = false,
    string? ExportConfigPath = null,
    bool UpdateBaseline = false,
    bool DiffGeneratedDir = false);

/// <summary>编排：定位/加载配置 → 模型构建 → 管线 → 对比/写盘/导出。可从测试直接调用。</summary>
public static class CodeGenRunner
{
    public static int Run(RunOptions options, IConsoleSink sink)
    {
        try
        {
            var configPath = ConfigLocator.ResolveConfigPath(options.Project, options.ConfigPath);
            var (repoRoot, _, config) = ConfigLocator.Load(configPath);
            var generatedDir = config.Resolve(repoRoot, config.Output.GeneratedDir);
            var pinPath = config.Resolve(repoRoot, config.Pin);

            if (options.ExportConfigPath is not null)
            {
                var exportPath = Path.GetFullPath(options.ExportConfigPath, repoRoot);
                Directory.CreateDirectory(Path.GetDirectoryName(exportPath)!);
                File.WriteAllText(exportPath, ConfigLocator.ExportEffective(config, repoRoot));
                sink.Info($"[export] 有效配置已导出: {Path.GetRelativePath(repoRoot, exportPath)}（默认值已填满、路径为仓库相对）");
            }

            // --update-pin：先跑一遍拿漂移清单，追加后重跑。
            if (options.UpdatePin)
            {
                var pin0 = PinFile.LoadOrEmpty(pinPath);
                var probe = ModelBuilder.Build(config, repoRoot, pin0, sink);
                if (probe.HasDrift)
                {
                    foreach (var ctx in config.Contexts)
                    {
                        var discovered = ExtractDiscoveredNames(probe, ctx);
                        pin0.Contexts.TryGetValue(ctx, out var pc);
                        pc ??= pin0.Contexts[ctx] = new PinFile.PinContext();
                        foreach (var name in discovered.Except(pc.Components.Select(p => p.Name)))
                        {
                            pc.Components.Add(new PinFile.PinComponent { Name = name });
                            sink.Info($"[pin] 追加 {ctx}.{name}");
                        }
                    }
                    pin0.Save(pinPath);
                    sink.Info($"[pin] 已写入 {pinPath}");
                }
                else
                {
                    sink.Info("[pin] 无漂移，未改动。");
                }
            }

            var pin = PinFile.LoadOrEmpty(pinPath);
            var build = ModelBuilder.Build(config, repoRoot, pin, sink);
            if (build.HasDrift)
            {
                sink.Error("== pin 对账失败 ==");
                foreach (var f in build.DriftFailures) sink.Error("  " + f);
                return ExitCodes.DriftOrDiff;
            }

            var model = build.Model!;

            if (options.ValidateOnly)
            {
                foreach (var ctx in model.Contexts)
                {
                    var generated = ctx.Components.Count(c => c.Generate && !c.Custom);
                    var custom = ctx.Components.Count(c => c.Custom);
                    sink.Info($"  {ctx.Name}: {ctx.Components.Count} 组件（{generated} 生成 / {custom} custom 手写）");

                    // 序列化策略与覆盖矩阵（auto 模式 = 豁免注册表）。
                    sink.Info($"    序列化策略: {config.SerializationMode}" +
                        (config.SerializationMode switch
                        {
                            "auto" => "（默认全量 + coverage 豁免制）",
                            "opt-in" => "（pin serialize:true 主动加入）",
                            _ => "（未启用）",
                        }));
                    var byCoverage = ctx.Components
                        .GroupBy(c => c.Serialize ? "serialized" : string.IsNullOrEmpty(c.Coverage) ? "-" : (c.Coverage!.StartsWith("provider:", StringComparison.Ordinal) ? "provider" : c.Coverage))
                        .OrderBy(g => g.Key, StringComparer.Ordinal);
                    foreach (var group in byCoverage)
                    {
                        sink.Info($"    {group.Key}: {group.Count()}（{string.Join(", ", group.Select(c => c.Name))}）");
                    }
                }
                sink.Info($"  editor: observer={(model.Editor.EmitContextObserver ? "on" : "off")}, feature={(model.Editor.EmitFeatureClass ? "on" : "off")}, guard={model.Editor.GuardExpression}");
                sink.Info("校验通过（--validate 不生成、不对比）。");
                return ExitCodes.Ok;
            }

            sink.Info("[3/4] 运行生成管线…");
            var files = Pipeline.Pipeline.CreateDefault().Run(model, new PipelineContext(repoRoot, config));
            sink.Info($"      产出 {files.Count} 个文件（另 {model.PreservedFiles.Count} 个 custom 手写文件做存在性校验）。");

            // 主校验：基线清单（生成器自洽）——与示例包目录解耦。
            int checkExit;
            if (!string.IsNullOrWhiteSpace(config.Baseline))
            {
                var baselinePath = config.Resolve(repoRoot, config.Baseline);
                if (options.UpdateBaseline)
                {
                    BaselineStore.FromFiles(config.Project, files).Save(baselinePath);
                    sink.Info($"[baseline] 已写入 {config.Baseline}（{files.Count} 个文件哈希）");
                    checkExit = ExitCodes.Ok;
                }
                else
                {
                    var baseline = BaselineStore.Load(baselinePath);
                    var (clean, differences) = baseline.Diff(files);
                    sink.Info($"[4/4] 基线校验 {config.Baseline} …");
                    if (clean)
                    {
                        sink.Info("  基线一致 ✅");
                    }
                    else
                    {
                        sink.Error($"  基线差异 {differences.Count} 项（模板/模型有变化；有意的变更用 --update-baseline 固化）:");
                        foreach (var d in differences.Take(options.Verbose ? differences.Count : 10)) sink.Error("    " + d);
                    }
                    checkExit = clean ? ExitCodes.Ok : ExitCodes.DriftOrDiff;
                }
            }
            else
            {
                // 未配基线：退回直接对比 generatedDir（小项目/沙盒路径）。
                sink.Info($"[4/4] 对比基准 {config.Output.GeneratedDir} …");
                var report = CompareReport.Compare(generatedDir, files, model.PreservedFiles, options.Verbose);
                sink.Info(report.Summary);
                checkExit = report.IsClean ? ExitCodes.Ok : ExitCodes.DriftOrDiff;
            }

            // 按需报告：与示例包（generatedDir）的落地差异——接入替换前的预期增量，不影响退出码。
            if (options.DiffGeneratedDir)
            {
                var report = CompareReport.Compare(generatedDir, files, model.PreservedFiles, options.Verbose);
                sink.Info($"[diff] 示例包目录 {config.Output.GeneratedDir}: {report.Matches.Count} 一致 / {report.ContentDiffs.Count} 内容差异 / {report.Missing.Count} 缺失 / {report.Extra.Count} 多余（接入替换时消化，不影响退出码）");
            }

            if (options.Write)
            {
                var target = options.OutDir is null
                    ? Path.Combine(repoRoot, "local", "Logs", "entitas-codegen", "out")
                    : Path.GetFullPath(options.OutDir, repoRoot);

                if (PathsEqual(target, generatedDir) && !options.ForceGeneratedDir)
                {
                    sink.Error($"拒绝直接写入 GeneratedDir（{target}）。如确要替换示例包产物，请显式传 --out 与 --force-generated-dir。");
                    return ExitCodes.UsageOrConfig;
                }

                sink.Info($"[写盘] {target} …");
                OutputWriter.Write(target, files, config, model.PreservedFiles.ToHashSet(StringComparer.OrdinalIgnoreCase), sink);
                sink.Info($"      已写 {files.Count} 个文件。");
            }

            return checkExit;
        }
        catch (CodeGenException ex)
        {
            sink.Error(ex.Message);
            return ex.ExitCode;
        }
        catch (Exception ex)
        {
            sink.Error($"内部错误: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            return ExitCodes.Internal;
        }
    }

    private static HashSet<string> ExtractDiscoveredNames(ModelBuildResult probe, string ctx) =>
        // 漂移清单里 "源码发现 {ctx}.{name} 但 pin 里没有" 的条目就是要追加的名字。
        probe.DriftFailures
            .Where(f => f.StartsWith($"源码发现 {ctx}.") && f.Contains("但 pin 里没有"))
            .Select(f => f.Substring($"源码发现 {ctx}.".Length, f.IndexOf(" 但 pin") - $"源码发现 {ctx}.".Length))
            .ToHashSet(StringComparer.Ordinal);

    private static bool PathsEqual(string a, string b) =>
        string.Equals(
            Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
}
