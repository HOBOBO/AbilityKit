using System.Diagnostics;
using AbilityKit.Demo.Shooter;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Grains.Battle;
using AbilityKit.Orleans.Grains.Battle.Gameplay;
using AbilityKit.Orleans.Grains.Gameplays.Shooter.Battle;
using AbilityKit.Protocol.Shooter;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace AbilityKit.Orleans.Grains.Tests.Battle;

/// <summary>
/// 权威 tick 阶段基准：不经过网络与 Unity，直接驱动 ShooterBattleRuntimeAdapter，
/// 按 mass-battle-lod-aoi-sample-block + ideal 的推送形态逐 tick 采样各系统阶段耗时，
/// 输出稳定窗口（后半程）的 mean/max 与权威达成帧率。
/// 默认规模保持在门禁可承受范围；设置 ABILITYKIT_SHOOTER_BENCH_UNIT_COUNTS
/// （如 "512,1000,2000"）与 ABILITYKIT_SHOOTER_BENCH_FRAMES 可按需扩大测量。
/// </summary>
public sealed class ShooterAuthorityStageBenchmarkTests
{
    private const string UnitCountsVariable = "ABILITYKIT_SHOOTER_BENCH_UNIT_COUNTS";
    private const string FramesVariable = "ABILITYKIT_SHOOTER_BENCH_FRAMES";
    private const int DefaultUnitCount = 512;
    private const int DefaultFrames = 360;
    private const int TickRate = 30;

    private readonly ITestOutputHelper _output;

    public ShooterAuthorityStageBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void AuthorityStageBenchmarkReportsStableWindowTimings()
    {
        foreach (var unitCount in ParseUnitCounts())
        {
            RunStageBenchmark(unitCount, ParseFrames());
        }
    }

    private void RunStageBenchmark(int unitCount, int totalFrames)
    {
        var template = ShooterServerSyncTemplateCatalog.Resolve(
            AbilityKit.Orleans.Contracts.Shooter.ShooterServerProtocol.MassBattleLodAoiSampleBlockTemplate);
        var pushOptions = template.CreatePushOptions("ideal");

        using var worldManager = new ServerBattleWorldManager(NullLogger.Instance);
        var battleAdapter = new ShooterBattleRuntimeAdapter(worldManager, pushOptions);
        var session = battleAdapter.CreateSession($"shooter-stage-bench-{unitCount}");
        var observerAwareSession = (IObserverAwareBattleRuntimeSession)session;

        var initParams = new BattleInitParams
        {
            WorldId = 1513ul,
            TickRate = TickRate,
            RandomSeed = 3901,
            WorldType = ShooterGameplay.WorldType,
            DurationFrames = totalFrames + 600,
            ContinueAfterAllPlayersDefeated = true,
            EnemyBudget = unitCount,
            SyncOptions = new BattleSyncStartOptions(
                template.TemplateId,
                SyncModel: 5,
                NetworkEnvironmentId: "ideal",
                CarrierName: null,
                EnableAuthoritativeWorld: false,
                InterpolationEnabled: true,
                InputDelayFrames: 0),
            Players = new List<PlayerInitInfo>
            {
                new() { PlayerId = 1, AccountId = "bench-owner", PosX = -2f, PosZ = 0f },
                new() { PlayerId = 2, AccountId = "bench-member", PosX = 2f, PosZ = 0f }
            }
        };

        var start = session.Start(initParams);
        Assert.True(start.Succeeded, start.Error);

        var observers = new[]
        {
            new BattleStateSyncObserverContext("bench-owner", "bench-owner", "bench-room"),
            new BattleStateSyncObserverContext("bench-member", "bench-member", "bench-room")
        };

        var stageTotals = new Dictionary<string, double>(StringComparer.Ordinal);
        var stageMaxima = new Dictionary<string, double>(StringComparer.Ordinal);
        var stageCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var windowStartFrame = Math.Max(1, totalFrames / 2);
        var stableTicks = 0;
        var snapshotBuildTotalMs = 0d;
        var snapshotBuildMaxMs = 0d;
        var snapshotBuildCount = 0;
        var snapshotBytes = 0L;
        var currentSimFrame = 0;

        ((IBattleRuntimeStageDiagnostics)session).SetStageTimingSink((stage, milliseconds) =>
        {
            if (currentSimFrame < windowStartFrame)
            {
                return;
            }

            stageTotals.TryGetValue(stage, out var total);
            stageTotals[stage] = total + milliseconds;
            stageMaxima.TryGetValue(stage, out var max);
            stageMaxima[stage] = Math.Max(max, milliseconds);
            stageCounts.TryGetValue(stage, out var count);
            stageCounts[stage] = count + 1;
        });

        var stableWindow = Stopwatch.StartNew();
        var tickInterval = 1f / TickRate;
        var stableMinEnemyCount = int.MaxValue;
        var stableMaxEnemyCount = 0;
        for (var frame = 1; frame <= totalFrames; frame++)
        {
            currentSimFrame = frame;
            Assert.True(session.Tick(frame, TickRate, tickInterval));
            session.SubmitInputs(frame, CreateMoveInputs(frame));
            if (frame >= windowStartFrame)
            {
                stableTicks++;
            }

            // 负载自证：周期性读取实体数，确认预算单位真实在场（而不是玩家早死后负载塌缩）。
            if (frame % 60 == 0)
            {
                var enemyCount = CountAliveEnemies(session, initParams.WorldId, frame);
                if (frame >= windowStartFrame && enemyCount >= 0)
                {
                    stableMinEnemyCount = Math.Min(stableMinEnemyCount, enemyCount);
                    stableMaxEnemyCount = Math.Max(stableMaxEnemyCount, enemyCount);
                }
            }

            // 模板节奏：每 3 帧对每个观察者推一次纯状态增量（每 450 帧一次全量基线）。
            if (frame % 3 == 0)
            {
                foreach (var observer in observers)
                {
                    var pushStartedAt = Stopwatch.GetTimestamp();
                    var push = observerAwareSession.CreateStateSyncPush(
                        initParams.WorldId,
                        frame,
                        isFullSnapshot: frame % 450 == 0,
                        in observer);
                    var pushElapsedMs = (Stopwatch.GetTimestamp() - pushStartedAt) * 1000d / Stopwatch.Frequency;
                    if (frame >= windowStartFrame)
                    {
                        snapshotBuildTotalMs += pushElapsedMs;
                        snapshotBuildMaxMs = Math.Max(snapshotBuildMaxMs, pushElapsedMs);
                        snapshotBuildCount++;
                        snapshotBytes += push.Payload?.Length ?? 0;
                    }
                }
            }
        }

        stableWindow.Stop();
        Assert.True(stableTicks > 0, "Stable window must contain at least one tick.");

        _output.WriteLine($"=== Authority stage benchmark: {unitCount} units, {totalFrames} frames ===");
        _output.WriteLine($"stable enemy count: min={stableMinEnemyCount}, max={stableMaxEnemyCount} (budget {unitCount})");
        _output.WriteLine(
            $"stable window: frames {windowStartFrame}-{totalFrames}, achieved {stableTicks / stableWindow.Elapsed.TotalSeconds:F2} Hz (sim only)");
        _output.WriteLine(
            $"snapshot build+serialize: n={snapshotBuildCount}, mean={snapshotBuildTotalMs / Math.Max(1, snapshotBuildCount):F3} ms, max={snapshotBuildMaxMs:F3} ms, avg payload={snapshotBytes / 1024d / Math.Max(1, snapshotBuildCount):F2} KB");
        foreach (var stage in stageTotals.Keys.OrderBy(key => key, StringComparer.Ordinal))
        {
            _output.WriteLine(
                $"{stage}: n={stageCounts[stage]}, mean={stageTotals[stage] / stageCounts[stage]:F3} ms, max={stageMaxima[stage]:F3} ms");
        }

        Assert.Contains("ShooterEnemyRvoSolveBattleSystem", stageTotals.Keys);
        Assert.Contains("ShooterEnemyMovementIntentBattleSystem", stageTotals.Keys);

        session.Dispose();
    }

    private static BattleInputItem[] CreateMoveInputs(int frame)
    {
        var inputs = new BattleInputItem[2];
        for (var i = 0; i < inputs.Length; i++)
        {
            var playerId = i + 1;
            var direction = playerId % 2 == 0 ? -0.7f : 0.7f;
            // 持续开火 + 环绕瞄准：保持玩家存活（否则敌人失去目标、RVO 平凡化，测量失真）。
            var angle = frame * 0.13f + playerId;
            inputs[i] = new BattleInputItem
            {
                PlayerId = (uint)playerId,
                OpCode = ShooterOpCodes.Input.PlayerCommand,
                Payload = ShooterInputCodec.Serialize(new[]
                {
                    new ShooterPlayerCommand(playerId, direction, 0.3f, MathF.Cos(angle), MathF.Sin(angle), true)
                })
            };
        }

        return inputs;
    }

    private static int CountAliveEnemies(IBattleRuntimeSession session, ulong worldId, int frame)
    {
        var diagnostics = session.GetWorldDiagnostics(worldId, frame);
        if (diagnostics?.Entities == null)
        {
            return -1;
        }

        var count = 0;
        foreach (var entity in diagnostics.Entities)
        {
            if (string.Equals(entity.EntityKind, "Enemy", StringComparison.Ordinal) && entity.Alive)
            {
                count++;
            }
        }

        return count;
    }

    private static IReadOnlyList<int> ParseUnitCounts()
    {
        var raw = Environment.GetEnvironmentVariable(UnitCountsVariable);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new[] { DefaultUnitCount };
        }

        var counts = raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ParsePositive)
            .Where(count => count > 0)
            .Distinct()
            .ToList();
        return counts.Count > 0 ? counts : new[] { DefaultUnitCount };
    }

    private static int ParseFrames()
    {
        var raw = Environment.GetEnvironmentVariable(FramesVariable);
        return int.TryParse(raw, out var frames) && frames >= 120 ? frames : DefaultFrames;
    }

    private static int ParsePositive(string value)
    {
        return int.TryParse(value, out var parsed) && parsed > 0 ? parsed : 0;
    }
}
