#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using AbilityKit.Ability.StateSync.Aoi;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Demo.Shooter.View.PlayMode;
using AbilityKit.Network.Runtime;
using AbilityKit.Protocol.Shooter;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace AbilityKit.Demo.Shooter.View.Editor
{
    /// <summary>
    /// 无头编辑器本地模式性能基准：复刻 PlayMode 菜单 "Start Local Frame Sync" 的会话组装，
    /// 拆分模拟 tick / 表现快照发布 / 端到端（含视图后端）三层成本，定位编辑器下大单位量掉帧归属。
    ///
    /// 用法（Unity batchmode -executeMethod AbilityKit.Demo.Shooter.View.Editor.ShooterLocalEditorPerfBenchCommand.Run）：
    ///   -localPerfUnits 2048        敌人预算（默认 2048，对应菜单 Stress 2k）
    ///   -localPerfSeconds 8         每阶段驱动秒数（默认 8）
    ///   -localPerfTemplate id       同步模板（默认 mass-battle-lod-aoi-sample-block）
    ///   -localPerfBackend name      视图后端（gameobject|gpu，默认 gameobject）
    ///   -localPerfResultPath path   JSON 报告输出路径（必填）
    /// </summary>
    public static class ShooterLocalEditorPerfBenchCommand
    {
        private const float RenderDeltaSeconds = 1f / 60f;
        private const int MaxCatchUpTicksPerRender = 2;

        private static readonly Dictionary<string, double> StageTotals = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, double> StageMaxima = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, int> StageCounts = new(StringComparer.Ordinal);

        public static void Run()
        {
            var exitCode = 0;
            try
            {
                RunCore();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[LocalEditorPerfBench] failed: {exception}");
                exitCode = 1;
            }

            EditorApplication.Exit(exitCode);
        }

        private static void RunCore()
        {
            var args = Environment.GetCommandLineArgs();
            var units = ParseInt(args, "-localPerfUnits") ?? 2048;
            var seconds = Math.Max(2f, ParseInt(args, "-localPerfSeconds") ?? 8);
            var templateId = ParseString(args, "-localPerfTemplate") ?? ShooterRoomLaunchSpec.DefaultSyncTemplateId;
            var backendName = ParseString(args, "-localPerfBackend") ?? "gameobject";
            var resultPath = ParseString(args, "-localPerfResultPath");
            if (string.IsNullOrWhiteSpace(resultPath))
            {
                throw new InvalidOperationException("-localPerfResultPath is required.");
            }

            var template = ShooterAcceptanceCatalog.GetSyncTemplate(templateId);
            var options = BuildOptions(template, units);
            var report = new StringBuilder();
            report.AppendLine("{");
            report.AppendLine($"  \"templateId\": \"{template.Id}\",");
            report.AppendLine($"  \"units\": {units},");
            report.AppendLine($"  \"backend\": \"{backendName}\"");

            TryPhase(report, "lab", () => RunLabPhase(report, template, options, seconds));
            TryPhase(report, "host", () => RunHostPhase(report, template, options, backendName, seconds));

            report.AppendLine("}");
            System.IO.File.WriteAllText(resultPath!, report.ToString());
            Debug.Log($"[LocalEditorPerfBench] report written to {resultPath}");
        }

        private static void TryPhase(StringBuilder report, string phase, Action run)
        {
            try
            {
                run();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[LocalEditorPerfBench] phase '{phase}' failed: {exception}");
                report.AppendLine($"  \"{phase}Error\": \"{exception.GetType().Name}: {exception.Message.Replace("\"", "'")}\"");
            }
        }

        private static ShooterPlayModeSessionOptions BuildOptions(
            ShooterSyncTemplate template,
            int units)
        {
            var templateOptions = ShooterPlayModeSessionOptions.FromTemplateForNetwork(
                template,
                ShooterRoomLaunchSpec.DefaultNetworkEnvironmentId,
                randomSeed: 12345,
                controlledPlayerId: 1,
                worldScale: 1f);
            return new ShooterPlayModeSessionOptions(
                templateOptions.SyncModel,
                templateOptions.TickRate,
                Math.Max(1, templateOptions.PlayerCount),
                templateOptions.RandomSeed,
                templateOptions.ControlledPlayerId,
                enableAuthoritativeWorld: false,
                templateOptions.LatencyMs,
                templateOptions.JitterMs,
                templateOptions.PacketLossRate,
                templateOptions.ReorderRate,
                templateOptions.BandwidthKbps,
                templateOptions.WorldScale,
                templateOptions.NetworkName,
                templateOptions.SyncTemplateId,
                ShooterPlayModeSessionOptions.CreatePlayModeScenario(units)).Normalized();
        }

        /// <summary>阶段 A：直接驱动验收会话（与菜单本地同源），拆模拟/发布成本并采样系统级阶段耗时。</summary>
        private static void RunLabPhase(
            StringBuilder report,
            ShooterSyncTemplate template,
            ShooterPlayModeSessionOptions options,
            float seconds)
        {
            StageTotals.Clear();
            StageMaxima.Clear();
            StageCounts.Clear();

            var players = new List<ShooterStartPlayer>(options.PlayerCount);
            for (var i = 0; i < options.PlayerCount; i++)
            {
                players.Add(new ShooterStartPlayer(i + 1, $"P{i + 1}", i * 4f, 0f));
            }

            var profile = ShooterAcceptanceCatalog.GetNetworkEnvironment(
                ShooterRoomLaunchSpec.DefaultNetworkEnvironmentId).Profile;
            var session = ShooterAcceptanceLab.CreateForTemplate(
                options.SyncTemplateId,
                options.SyncModel,
                profile,
                options.NetworkName,
                options.TickRate,
                players,
                options.RandomSeed,
                options.EnableAuthoritativeWorld,
                options.GameplayScenario);
            try
            {
                session.Presentation.ControlledPlayerId = options.ControlledPlayerId;
                if (session.Runtime is IShooterBattlePerformancePort performance)
                {
                    performance.StageTimingSink = RecordStage;
                }

                var sendPolicy = template.SendPolicy;
                var pureStateSettings = sendPolicy.AoiRadius > 0f
                    ? sendPolicy.ToPureStateSettings()
                    : CreateFallbackPureStateSettings(options);
                var usesAoi = sendPolicy.AoiRadius > 0f;
                var aoiInterestSet = usesAoi ? new AoiInterestSet() : null;
                var hasBaseline = false;
                var baselineFrame = 0;
                var baselineHash = 0u;
                var snapshotInterval = Math.Max(1, sendPolicy.SnapshotIntervalFrames);

                var tickInterval = 1f / options.TickRate;
                var accumulator = 0f;
                var frames = 0;
                var ticks = 0;
                var publishes = 0;
                var stepCount = 0L;
                var tickTotalMs = 0d;
                var exportTotalMs = 0d;
                var applyTotalMs = 0d;
                var gcBefore = GC.GetTotalMemory(false);
                var gen0Before = GC.CollectionCount(0);
                var startedAt = Stopwatch.StartNew();
                var totalFrames = (int)(seconds / RenderDeltaSeconds);
                for (var frame = 0; frame < totalFrames; frame++)
                {
                    frames++;
                    accumulator += RenderDeltaSeconds;
                    var ticksThisRender = 0;
                    var shouldPublish = false;
                    while (accumulator >= tickInterval && ticksThisRender++ < MaxCatchUpTicksPerRender)
                    {
                        accumulator -= tickInterval;
                        var command = ShooterClientInputBuilder.CreateCommand(
                            options.ControlledPlayerId, 0f, 0f, 1f, 0f, false, 0);
                        session.Controller.SubmitLocalInput(in command);
                        var tickStartedAt = Stopwatch.GetTimestamp();
                        session.Controller.Tick(tickInterval);
                        tickTotalMs += ElapsedMs(tickStartedAt);
                        ticks++;
                        stepCount++;
                        shouldPublish |= stepCount % snapshotInterval == 0;
                    }

                    if (accumulator >= tickInterval)
                    {
                        accumulator = 0f;
                    }

                    if (shouldPublish)
                    {
                        publishes++;
                        var isFullBaseline = !hasBaseline ||
                            session.Runtime.CurrentFrame - baselineFrame >= Math.Max(1, pureStateSettings.BaselineIntervalFrames);
                        var scope = CreateInterestScope(session, options, sendPolicy);
                        var exportStartedAt = Stopwatch.GetTimestamp();
                        var snapshot = session.Runtime.ExportPureStateSnapshotTransient(
                            worldId: 0,
                            isFullBaseline: isFullBaseline,
                            settings: pureStateSettings,
                            baselineFrame: baselineFrame,
                            baselineHash: baselineHash,
                            interestScope: scope,
                            aoiInterestSet: aoiInterestSet,
                            computeStateHash: false);
                        exportTotalMs += ElapsedMs(exportStartedAt);
                        if (isFullBaseline)
                        {
                            hasBaseline = true;
                            baselineFrame = snapshot.Frame;
                            baselineHash = snapshot.StateHash;
                        }

                        var applyStartedAt = Stopwatch.GetTimestamp();
                        session.Presentation.ApplyPureStateSnapshot(in snapshot);
                        applyTotalMs += ElapsedMs(applyStartedAt);
                    }
                }

                startedAt.Stop();
                var gcAfter = GC.GetTotalMemory(false);
                var gen0After = GC.CollectionCount(0);
                var aliveEnemies = CountAliveEnemies(session);
                var viewModelTransforms = session.Presentation.ViewModel.Current.TransformChanges.Count;

                AppendPhase(report, "lab", startedAt.Elapsed.TotalSeconds, frames, gcAfter - gcBefore, gen0After - gen0Before);
                report.AppendLine($"    \"ticks\": {ticks},");
                report.AppendLine($"    \"publishes\": {publishes},");
                report.AppendLine($"    \"simTickMeanMs\": {Round(tickTotalMs / Math.Max(1, ticks))},");
                report.AppendLine($"    \"publishExportMeanMs\": {Round(exportTotalMs / Math.Max(1, publishes))},");
                report.AppendLine($"    \"publishApplyMeanMs\": {Round(applyTotalMs / Math.Max(1, publishes))},");
                report.AppendLine($"    \"aliveEnemies\": {aliveEnemies},");
                report.AppendLine($"    \"viewModelTransforms\": {viewModelTransforms},");
                report.AppendLine("    \"stages\": {");
                var first = true;
                foreach (var stage in StageTotals.Keys)
                {
                    if (!first)
                    {
                        report.AppendLine(",");
                    }

                    first = false;
                    report.Append($"      \"{stage}\": {{ \"n\": {StageCounts[stage]}, \"meanMs\": {Round(StageTotals[stage] / StageCounts[stage])}, \"maxMs\": {Round(StageMaxima[stage])} }}");
                }

                report.AppendLine();
                report.AppendLine("    },");
            }
            finally
            {
                session.Dispose();
            }
        }

        /// <summary>阶段 B：走真实 ShooterPlayModeSessionHost（含视图后端与播放器循环钩子），取端到端帧成本。</summary>
        private static void RunHostPhase(
            StringBuilder report,
            ShooterSyncTemplate template,
            ShooterPlayModeSessionOptions options,
            string backendName,
            float seconds)
        {
            var backend = ShooterUnityViewRenderBackendCatalog.Get(
                ShooterUnityViewRenderBackendCatalog.Normalize(ParseBackend(backendName)));
            ShooterPlayModeSessionHost.SetViewBackend(backend.Backend);

            var gcBefore = GC.GetTotalMemory(false);
            var gen0Before = GC.CollectionCount(0);
            var startedAt = Stopwatch.StartNew();
            ShooterPlayModeSessionHost.Start(options);
            var frames = 0;
            var totalFrames = (int)(seconds / RenderDeltaSeconds);
            for (var frame = 0; frame < totalFrames; frame++)
            {
                var frameStartedAt = Stopwatch.GetTimestamp();
                ShooterPlayModeSessionHost.Tick(RenderDeltaSeconds);
                FrameMaxMs = Math.Max(FrameMaxMs, ElapsedMs(frameStartedAt));
                frames++;
            }

            startedAt.Stop();
            var gcAfter = GC.GetTotalMemory(false);
            var gen0After = GC.CollectionCount(0);
            AppendPhase(report, "host", startedAt.Elapsed.TotalSeconds, frames, gcAfter - gcBefore, gen0After - gen0Before);
            report.AppendLine($"    \"frameMeanMs\": {Round(startedAt.Elapsed.TotalMilliseconds / Math.Max(1, frames))},");
            report.AppendLine($"    \"frameMaxMs\": {Round(FrameMaxMs)},");
            report.AppendLine($"    \"derivedFps\": {Round(frames / startedAt.Elapsed.TotalSeconds, 1)}");
            report.AppendLine("  }");
            ShooterPlayModeSessionHost.Stop();
        }

        private static double FrameMaxMs;

        private static ShooterUnityViewRenderBackend ParseBackend(string name) => name.ToLowerInvariant() switch
        {
            "gpu" or "gpuinstanced" => ShooterUnityViewRenderBackend.GpuInstancedDotsReady,
            _ => ShooterUnityViewRenderBackend.GameObject
        };

        private static ShooterPureStateInterestScope? CreateInterestScope(
            ShooterAcceptanceSession session,
            ShooterPlayModeSessionOptions options,
            ShooterSyncTemplateSendPolicy sendPolicy)
        {
            if (sendPolicy.AoiRadius <= 0f)
            {
                return null;
            }

            var centerX = 0f;
            var centerY = 0f;
            if (session.Runtime.TryGetPlayer(options.ControlledPlayerId, out var observer))
            {
                centerX = observer.X;
                centerY = observer.Y;
            }

            return new ShooterPureStateInterestScope(
                options.ControlledPlayerId,
                centerX,
                centerY,
                sendPolicy.AoiRadius,
                sendPolicy.AoiBoundaryRadius,
                sendPolicy.ActiveEntityBudget);
        }

        private static ShooterPureStateSyncSettings CreateFallbackPureStateSettings(
            ShooterPlayModeSessionOptions options)
        {
            var defaults = ShooterPureStateSyncSettings.Default;
            var maxEntities = Math.Max(
                defaults.MaxEntityCount,
                options.GameplayScenario.BattleFlow.MaxActiveEnemies + options.PlayerCount + 1024);
            return new ShooterPureStateSyncSettings(maxEntities, defaults.ActiveSyncBudget, 150, 1, 15, 1);
        }

        private static void RecordStage(string stage, double milliseconds)
        {
            StageTotals.TryGetValue(stage, out var total);
            StageTotals[stage] = total + milliseconds;
            StageMaxima.TryGetValue(stage, out var max);
            StageMaxima[stage] = Math.Max(max, milliseconds);
            StageCounts.TryGetValue(stage, out var count);
            StageCounts[stage] = count + 1;
        }

        private static int CountAliveEnemies(ShooterAcceptanceSession session)
        {
            var snapshot = session.Runtime.GetSnapshot();
            var count = 0;
            foreach (var enemy in snapshot.Enemies ?? Array.Empty<ShooterEnemySnapshot>())
            {
                if (enemy.Alive)
                {
                    count++;
                }
            }

            return count;
        }

        private static void AppendPhase(
            StringBuilder report,
            string phase,
            double elapsedSeconds,
            int frames,
            long gcDeltaBytes,
            int gen0Collections)
        {
            report.AppendLine($"  \"{phase}\": {{");
            report.AppendLine($"    \"wallSeconds\": {Round(elapsedSeconds)},");
            report.AppendLine($"    \"frames\": {frames},");
            report.AppendLine($"    \"gcDeltaBytes\": {gcDeltaBytes},");
            report.AppendLine($"    \"gen0Collections\": {gen0Collections},");
        }

        private static double Round(double value, int digits = 3) => Math.Round(value, digits);

        private static double ElapsedMs(long startedAt) =>
            (Stopwatch.GetTimestamp() - startedAt) * 1000d / Stopwatch.Frequency;

        private static int? ParseInt(string[] args, string name)
        {
            var value = ParseString(args, name);
            return int.TryParse(value, out var parsed) ? parsed : null;
        }

        private static string? ParseString(string[] args, string name)
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
    }
}
