using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using AbilityKit.Diagnostics;
using AbilityKit.Diagnostics.Exporters;
using AbilityKit.Demo.Moba.Console.Platform;
using AbilityKit.Demo.Moba.Services;

namespace AbilityKit.Demo.Moba.Console
{
    /// <summary>
    /// Console MOBA 冒烟战斗的性能分析开关。
    ///
    /// 设置 ABILITYKIT_MOBA_PROFILE=1 后，ConsoleBattleBootstrapper 会为每次战斗
    /// 安装 EditorProfiler 并在战斗结束时导出火焰图产物（speedscope / folded / json）
    /// 到 local/Logs/moba-console-profile。默认关闭，冒烟路径零开销（NullProfiler）。
    ///
    /// 注意：ProfilerHub 是进程级单例。对 xunit 冒烟做采样时建议过滤到单个用例
    /// （如 --filter "FullyQualifiedName~Console_entry_full_battle_scenario"），
    /// 避免并行用例共用同一个 profiler 会话。
    /// </summary>
    public static class MobaConsoleProfiling
    {
        public const string EnabledEnvironmentVariable = "ABILITYKIT_MOBA_PROFILE";
        public const string DetailEnvironmentVariable = "ABILITYKIT_MOBA_PROFILE_DETAIL";
        public const string DirectoryEnvironmentVariable = "ABILITYKIT_MOBA_PROFILE_DIR";
        public const string DefaultArtifactFolder = "moba-console-profile";

        /// <summary>导出格式：speedscope 交互查看（真实时间线）、chrome trace（带帧号/关联上下文的时间轴）、flamegraph SVG、folded、json 完整快照。</summary>
        internal static readonly string[] ExportFormats = { "speedscope", "chrome", "flamegraph", "folded", "json" };

        public static bool IsEnabledFromEnvironment()
        {
            var value = Environment.GetEnvironmentVariable(EnabledEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(value)) return false;

            return value.Trim().ToLowerInvariant() switch
            {
                "1" or "on" or "true" or "yes" or "always" => true,
                _ => false
            };
        }

        /// <summary>环境变量开启时为一次战斗创建性能分析会话；未开启返回 null。</summary>
        public static MobaConsoleProfileSession? TryBeginSession(string label)
        {
            if (!IsEnabledFromEnvironment()) return null;

            // 明细模式：探针名带配置 ID/动作名，火焰图细化到 moba.effect.action[803] 粒度。
            var detail = Environment.GetEnvironmentVariable(DetailEnvironmentVariable);
            MobaPerformanceProfiling.DetailMarkers =
                !string.IsNullOrWhiteSpace(detail) && detail.Trim() is "1" or "on" or "true" or "yes";
            return MobaConsoleProfileSession.Begin(label);
        }

        /// <summary>
        /// 解析产物目录：优先环境变量 ABILITYKIT_MOBA_PROFILE_DIR，
        /// 否则定位仓库根下的 local/Logs/moba-console-profile（.gitignore 已覆盖）。
        /// </summary>
        public static string ResolveArtifactDirectory()
        {
            var overrideDirectory = Environment.GetEnvironmentVariable(DirectoryEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(overrideDirectory)) return overrideDirectory.Trim();

            var root = FindRepositoryRoot();
            return root == null
                ? Path.Combine(Directory.GetCurrentDirectory(), "local", "Logs", DefaultArtifactFolder)
                : Path.Combine(root, "local", "Logs", DefaultArtifactFolder);
        }

        private static string? FindRepositoryRoot()
        {
            // xunit/dotnet run 的工作目录不固定（bin、项目目录都有可能），
            // 从程序集位置向上找 .git 或 tools/test-gates.json 才能稳定落到仓库根。
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, ".git")) ||
                    File.Exists(Path.Combine(directory.FullName, "tools", "test-gates.json")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            return null;
        }
    }

    /// <summary>
    /// 一次战斗的性能分析会话：安装 EditorProfiler，结束时导出火焰图产物并恢复 NullProfiler。
    /// </summary>
    public sealed class MobaConsoleProfileSession : IDisposable
    {
        private readonly EditorProfiler _profiler;
        private readonly string _label;
        private readonly string _directory;
        private bool _exported;

        private MobaConsoleProfileSession(EditorProfiler profiler, string label, string directory)
        {
            _profiler = profiler;
            _label = label;
            _directory = directory;
        }

        public static MobaConsoleProfileSession Begin(string label, string? artifactDirectory = null)
        {
            var profiler = new EditorProfiler();
            profiler.Configure(new ProfilerOptions
            {
                Enabled = true,
                DefaultSampleRate = 1d,
                MaxSamplesPerMetric = 4096,
                MaxDiagnosticEvents = 1024,
                // 完整战斗冒烟约产生 2 万+ span，默认 16384 会被环形覆盖；65536 覆盖整场。
                MaxSpanEvents = 65536
            });
            ProfilerHub.SetProfiler(profiler);
            profiler.Start();

            return new MobaConsoleProfileSession(
                profiler,
                string.IsNullOrWhiteSpace(label) ? "moba-console" : label.Trim(),
                artifactDirectory ?? MobaConsoleProfiling.ResolveArtifactDirectory());
        }

        /// <summary>停止采样并导出全部产物。重复调用安全（只导出一次）。</summary>
        public MobaConsoleProfileExport Export()
        {
            if (_exported) throw new InvalidOperationException("Profile session has already been exported.");
            _exported = true;

            _profiler.Stop();
            var snapshot = _profiler.GetSnapshot();
            Directory.CreateDirectory(_directory);

            var baseName = $"{_label}-{DateTime.Now:yyyyMMdd-HHmmss}-{snapshot.SessionId.GetHashCode():x}";
            var files = new List<string>(MobaConsoleProfiling.ExportFormats.Length);
            foreach (var format in MobaConsoleProfiling.ExportFormats)
            {
                var exporter = ExporterFactory.Get(format);
                var path = Path.Combine(_directory, baseName + exporter.Extension);
                exporter.Export(snapshot, path);
                files.Add(path);
            }

            var export = new MobaConsoleProfileExport(
                _label,
                _directory,
                files,
                CountFlameNodes(snapshot),
                snapshot.Counters.Count,
                snapshot.Durations.Count);
            Log.System($"[Profile] {Describe(export)}");
            foreach (var line in BuildTopSelfTimeLines(snapshot, 12))
            {
                Log.System($"[Profile] {line}");
            }

            return export;
        }

        public void Dispose()
        {
            try
            {
                if (!_exported) Export();
            }
            catch (Exception ex)
            {
                // 性能分析失败不允许影响战斗冒烟本身的结果。
                Log.Warn($"[Profile] Failed to export profile artifacts: {ex.Message}");
            }
            finally
            {
                ProfilerHub.SetProfiler(null);
            }
        }

        private static int CountFlameNodes(ProfilerSnapshot snapshot)
        {
            if (snapshot.Root == null) return 0;

            var count = 0;
            foreach (var root in snapshot.Root.Roots.Values)
            {
                count += CountNodes(root);
            }

            return count;
        }

        private static int CountNodes(FlameNode node)
        {
            var count = 1;
            foreach (var child in node.Children.Values)
            {
                count += CountNodes(child);
            }

            return count;
        }

        private static string Describe(MobaConsoleProfileExport export)
        {
            var fileList = string.Join(", ", export.Files.Select(Path.GetFileName));
            return $"session='{export.Label}' flameNodes={export.FlameNodeCount} counters={export.CounterCount} durations={export.DurationCount} -> {export.Directory} [{fileList}]";
        }

        private static IEnumerable<string> BuildTopSelfTimeLines(ProfilerSnapshot snapshot, int top)
        {
            if (snapshot.Root == null) yield break;

            var nodes = new List<FlameNode>(64);
            foreach (var root in snapshot.Root.Roots.Values)
            {
                CollectNodes(root, nodes);
            }

            nodes.Sort((x, y) => y.SelfNanoseconds.CompareTo(x.SelfNanoseconds));
            var rank = 0;
            foreach (var node in nodes)
            {
                if (node.SelfNanoseconds <= 0) yield break;
                rank++;
                if (rank > top) yield break;

                var selfMs = (node.SelfNanoseconds / 1000000d).ToString("F3", CultureInfo.InvariantCulture);
                var totalMs = (node.TotalNanoseconds / 1000000d).ToString("F3", CultureInfo.InvariantCulture);
                yield return $"top {rank:D2}  self={selfMs}ms  total={totalMs}ms  hits={node.HitCount}  {node.Name}";
            }
        }

        private static void CollectNodes(FlameNode node, List<FlameNode> sink)
        {
            sink.Add(node);
            foreach (var child in node.Children.Values)
            {
                CollectNodes(child, sink);
            }
        }
    }

    /// <summary>一次性能分析导出的产物描述。</summary>
    public sealed class MobaConsoleProfileExport
    {
        public MobaConsoleProfileExport(
            string label,
            string directory,
            IReadOnlyList<string> files,
            int flameNodeCount,
            int counterCount,
            int durationCount)
        {
            Label = label;
            Directory = directory;
            Files = files;
            FlameNodeCount = flameNodeCount;
            CounterCount = counterCount;
            DurationCount = durationCount;
        }

        public string Label { get; }
        public string Directory { get; }
        public IReadOnlyList<string> Files { get; }
        public int FlameNodeCount { get; }
        public int CounterCount { get; }
        public int DurationCount { get; }
    }
}
