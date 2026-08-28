using System;
using System.Threading.Tasks;
using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Demo.Shooter.View;
using AbilityKit.Demo.Shooter.View.PlayMode;
using AbilityKit.Protocol.Shooter;
using Xunit;
using Xunit.Abstractions;

namespace AbilityKit.Demo.Shooter.Runtime.Tests.Client;

/// <summary>
/// 诊断用（默认跳过）：对真实网关复刻正式流程的 暂停→恢复，捕获 Resume 静默失败的真实异常。
/// 运行前置：本地网关在 127.0.0.1:4000（start_abilitykit.ps1）。启用：ABILITYKIT_SHOOTER_GATEWAY_REPRO=1。
/// 步骤：双账号建房开局 → 战斗数秒 → A 关闭连接（=Pause）→ 新世界+新连接 RestoreRoomAsync（=Resume）。
/// </summary>
public sealed class ShooterPauseResumeReproTests
{
    private readonly ITestOutputHelper _output;

    public ShooterPauseResumeReproTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task PauseThenResumeAgainstLiveGateway()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("ABILITYKIT_SHOOTER_GATEWAY_REPRO"),
                "1",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var host = "127.0.0.1";
        var port = 4000;
        var endpoint = new ShooterClientNetworkEndpoint(host, port);
        var timeout = TimeSpan.FromSeconds(10);
        var suffix = Guid.NewGuid().ToString("N").Substring(0, 8);

        var loginA = await DemoRoomGatewayAccountClient.LoginTcpAsync(host, port, $"repro-a-{suffix}", timeout);
        Assert.True(loginA.Success, $"login A failed: {loginA.Message}");
        var loginB = await DemoRoomGatewayAccountClient.LoginTcpAsync(host, port, $"repro-b-{suffix}", timeout);
        Assert.True(loginB.Success, $"login B failed: {loginB.Message}");

        var template = ShooterAcceptanceCatalog.GetSyncTemplate(ShooterRoomLaunchSpec.DefaultSyncTemplateId);
        var sessionOptionsA = BuildOptions(template, controlledPlayerId: 1);
        var sessionOptionsB = BuildOptions(template, controlledPlayerId: 2);

        var runtimeA = ShooterGameplayScenarioWorldHostFactory.CreateBattleWorld($"repro-a-{suffix}", sessionOptionsA);
        var runtimeB = ShooterGameplayScenarioWorldHostFactory.CreateBattleWorld($"repro-b-{suffix}", sessionOptionsB);
        var launcherA = ShooterClientNetworkLauncher.Create(ShooterClientConnectionFactory.Tcp());
        var launcherB = ShooterClientNetworkLauncher.Create(ShooterClientConnectionFactory.Tcp());
        try
        {
            var startA = BuildStartPayload(sessionOptionsA);
            var startB = BuildStartPayload(sessionOptionsB);
            var specA = ShooterRoomLaunchSpec.CreateDefault($"repro-a-{suffix}");
            var specB = ShooterRoomLaunchSpec.CreateDefault($"repro-b-{suffix}");

            var presentationA = new ShooterPresentationFacade { ControlledPlayerId = 1 };
            var presentationB = new ShooterPresentationFacade { ControlledPlayerId = 2 };
            var resultA = await launcherA.CreateReadyStartAndSubscribeAsync(
                endpoint, runtimeA.Runtime, presentationA,
                startA, loginA.SessionToken, specA, 1, sessionOptionsA.TickRate, timeout);
            _output.WriteLine($"A created room {resultA.Flow.RoomId} entry={resultA.Flow.EntryKind}");

            var resultB = await launcherB.JoinReadyStartAndSubscribeAsync(
                endpoint, runtimeB.Runtime, presentationB,
                startB, loginB.SessionToken, resultA.Flow.RoomId, specB, 2, sessionOptionsB.TickRate, timeout);
            _output.WriteLine($"B joined room {resultB.Flow.RoomId} entry={resultB.Flow.EntryKind}");

            await Task.Delay(TimeSpan.FromSeconds(4));
            _output.WriteLine($"battle running: frameA={resultA.Session.CurrentFrame} frameB={resultB.Session.CurrentFrame}");

            // Pause 语义：关闭 A 的连接（宿主 Pause() 即 state.Launcher.Close()）。
            launcherA.Close();
            await Task.Delay(TimeSpan.FromSeconds(1));
            _output.WriteLine("A connection closed (paused).");

            // Resume 语义：新世界 + 新连接 + RestoreRoomAsync（宿主 StartSessionAsync 的核心步骤）。
            var runtimeA2 = ShooterGameplayScenarioWorldHostFactory.CreateBattleWorld($"repro-a2-{suffix}", sessionOptionsA);
            var launcherA2 = ShooterClientNetworkLauncher.Create(ShooterClientConnectionFactory.Tcp());
            try
            {
                var resumed = await launcherA2.RestoreRoomAsync(
                    endpoint,
                    runtimeA2.Runtime,
                    ShooterPresentationSessionContext.CreateDefault(),
                    startA,
                    loginA.SessionToken,
                    specA.Region,
                    specA.ServerId,
                    specA,
                    1,
                    sessionOptionsA.TickRate,
                    timeout);
                _output.WriteLine(
                    $"RESUME OK: room={resumed.Flow.RoomId} entry={resumed.Flow.EntryKind} frame={resumed.Session.CurrentFrame}");
                Assert.Equal(resultA.Flow.RoomId, resumed.Flow.RoomId);
            }
            catch (Exception ex)
            {
                _output.WriteLine($"RESUME FAILED: {ex}");
                throw;
            }
            finally
            {
                launcherA2.Dispose();
                runtimeA2.Dispose();
            }
        }
        finally
        {
            launcherA.Dispose();
            launcherB.Dispose();
            runtimeA.Dispose();
            runtimeB.Dispose();
        }
    }

    private static ShooterPlayModeSessionOptions BuildOptions(ShooterSyncTemplate template, int controlledPlayerId)
    {
        var templateOptions = ShooterPlayModeSessionOptions.FromTemplateForNetwork(
            template,
            "ideal",
            randomSeed: 12345,
            controlledPlayerId,
            worldScale: 1f);
        return new ShooterPlayModeSessionOptions(
            templateOptions.SyncModel,
            templateOptions.TickRate,
            2,
            templateOptions.RandomSeed,
            Math.Min(controlledPlayerId, 2),
            enableAuthoritativeWorld: false,
            templateOptions.LatencyMs,
            templateOptions.JitterMs,
            templateOptions.PacketLossRate,
            templateOptions.ReorderRate,
            templateOptions.BandwidthKbps,
            templateOptions.WorldScale,
            templateOptions.NetworkName,
            templateOptions.SyncTemplateId,
            ShooterPlayModeSessionOptions.CreatePlayModeScenario(64)).Normalized();
    }

    private static ShooterStartGamePayload BuildStartPayload(ShooterPlayModeSessionOptions options)
    {
        return new ShooterStartGamePayload(
            $"repro-{options.ControlledPlayerId}",
            options.TickRate,
            options.RandomSeed,
            new[] { new ShooterStartPlayer(options.ControlledPlayerId, "P", 0f, 0f) });
    }
}
