using System;
using System.Collections.Generic;
using System.Diagnostics;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Shooter;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Protocol.Shooter;
using Xunit;
using Xunit.Abstractions;

namespace AbilityKit.Demo.Shooter.Runtime.Tests.Runtime;

/// <summary>
/// 纯客户端路径的权威阶段基准：用 ShooterWorldModule（客户端默认世界模块，
/// 注册 Null RVO 加速服务，即串行 managed 求解器）直接驱动本地战斗，
/// 测量本地模式（无服务端加速注入）下各阶段单帧成本。
/// 与 Grains 侧 ShooterAuthorityStageBenchmarkTests（服务端加速路径）对照，
/// 用于回答"本地 2K 卡顿是逻辑层还是表现层"的归因问题。
/// 规模可用 ABILITYKIT_SHOOTER_LOCAL_BENCH_UNITS / _FRAMES 扩展。
/// </summary>
public sealed class ShooterLocalWorldStageBenchmarkTests
{
    private const string UnitsVariable = "ABILITYKIT_SHOOTER_LOCAL_BENCH_UNITS";
    private const string FramesVariable = "ABILITYKIT_SHOOTER_LOCAL_BENCH_FRAMES";
    private const int DefaultFrames = 420;
    private const int TickRate = 30;

    private readonly ITestOutputHelper _output;

    public ShooterLocalWorldStageBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void LocalWorldStageBenchmarkReportsManagedSolverTimings()
    {
        var unitCount = ParseUnits();
        var totalFrames = ParseFrames();
        var windowStartFrame = Math.Max(1, totalFrames / 2);

        // 并发波次堆填充速率：每帧每个波次刷 1 只，40 波并发 ≈ 每帧 40 只，
        // 2000 预算约 50 帧填满。超高血量防止玩家清怪把负载平衡在预算之下。
        const int enemiesPerWave = 50;
        var waveCount = Math.Max(1, unitCount / enemiesPerWave);
        var waves = new ShooterSveltoGameplayWaveConfig[waveCount];
        for (var i = 0; i < waveCount; i++)
        {
            waves[i] = new ShooterSveltoGameplayWaveConfig(
                waveId: i + 1,
                startFrame: 1,
                spawnFrameInterval: 1,
                enemyCount: enemiesPerWave,
                enemyHp: 100000,
                spawnRadius: 40f);
        }

        var flow = new ShooterSveltoGameplayBattleFlowConfig(
            durationFrames: totalFrames + 900,
            victoryTargetDefeats: 0,
            maxActiveEnemies: unitCount,
            waves);

        var container = new WorldContainerBuilder()
            .TryRegister<ShooterEnemyWaveOptions>(
                WorldLifetime.Singleton,
                _ => new ShooterEnemyWaveOptions(true, flow))
            .AddModule(new ShooterWorldModule())
            .Build();
        var runtime = container.Resolve<IShooterBattleRuntimePort>();
        var performance = runtime as IShooterBattlePerformancePort;
        Assert.NotNull(performance);

        var start = new ShooterStartGamePayload(
            "local-stage-bench",
            TickRate,
            3901,
            new[] { new ShooterStartPlayer(1, "P1", 0f, 0f) });
        Assert.True(runtime.StartGame(in start));

        var stageTotals = new Dictionary<string, double>(StringComparer.Ordinal);
        var stageMaxima = new Dictionary<string, double>(StringComparer.Ordinal);
        var stageCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var currentFrame = 0;
        performance!.StageTimingSink = (stage, milliseconds) =>
        {
            if (currentFrame < windowStartFrame)
            {
                return;
            }

            stageTotals.TryGetValue(stage, out var total);
            stageTotals[stage] = total + milliseconds;
            stageMaxima.TryGetValue(stage, out var max);
            stageMaxima[stage] = Math.Max(max, milliseconds);
            stageCounts.TryGetValue(stage, out var count);
            stageCounts[stage] = count + 1;
        };

        var stableWindow = Stopwatch.StartNew();
        var stableTicks = 0;
        var stableMinEnemies = int.MaxValue;
        var stableMaxEnemies = 0;
        var interval = 1f / TickRate;
        var commands = new ShooterPlayerCommand[1];
        for (var frame = 1; frame <= totalFrames; frame++)
        {
            if (frame == windowStartFrame)
            {
                stableWindow.Restart();
            }

            currentFrame = frame;
            var angle = frame * 0.13f;
            commands[0] = new ShooterPlayerCommand(1, MathF.Cos(angle) * 0.7f, MathF.Sin(angle) * 0.7f, MathF.Cos(angle), MathF.Sin(angle), true);
            runtime.SubmitInput(frame, commands);
            Assert.True(runtime.Tick(interval));
            if (frame >= windowStartFrame)
            {
                stableTicks++;
            }

            if (frame % 60 == 0)
            {
                var snapshot = runtime.GetSnapshot();
                var enemies = 0;
                foreach (var enemy in snapshot.Enemies ?? Array.Empty<ShooterEnemySnapshot>())
                {
                    if (enemy.Alive)
                    {
                        enemies++;
                    }
                }

                if (frame >= windowStartFrame)
                {
                    stableMinEnemies = Math.Min(stableMinEnemies, enemies);
                    stableMaxEnemies = Math.Max(stableMaxEnemies, enemies);
                }
            }
        }

        stableWindow.Stop();
        _output.WriteLine($"=== Local world (managed RVO) benchmark: {unitCount} units, {totalFrames} frames ===");
        _output.WriteLine($"stable enemy count: min={stableMinEnemies}, max={stableMaxEnemies} (budget {unitCount})");
        _output.WriteLine($"stable window achieved {stableTicks / stableWindow.Elapsed.TotalSeconds:F2} Hz (sim only)");
        foreach (var stage in stageTotals.Keys.OrderBy(key => key, StringComparer.Ordinal))
        {
            _output.WriteLine($"{stage}: n={stageCounts[stage]}, mean={stageTotals[stage] / stageCounts[stage]:F3} ms, max={stageMaxima[stage]:F3} ms");
        }

        Assert.Contains("ShooterEnemyRvoSolveBattleSystem", stageTotals.Keys);
    }

    private static int ParseUnits()
    {
        var raw = Environment.GetEnvironmentVariable(UnitsVariable);
        return int.TryParse(raw, out var units) && units > 0 ? units : 512;
    }

    private static int ParseFrames()
    {
        var raw = Environment.GetEnvironmentVariable(FramesVariable);
        return int.TryParse(raw, out var frames) && frames >= 120 ? frames : DefaultFrames;
    }
}
