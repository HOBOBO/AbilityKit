using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Demo.Shooter.View;
using Xunit;
using Xunit.Abstractions;

namespace AbilityKit.Demo.Shooter.Runtime.Tests.Client;

/// <summary>
/// 诊断用（默认跳过）：对真实网关复刻"关游戏→重登→自动重连回进行中对局"，
/// 定位重连失败的确切环节。启用：ABILITYKIT_SHOOTER_GATEWAY_REPRO=1（需本地网关 127.0.0.1:4000）。
/// 流程：双账号建房开局 → 战斗运行 → A 关闭连接 → A 重登 + RestoreRoom + Join + GetSnapshot，逐项打印结果。
/// </summary>
public sealed class ShooterReconnectReproTests
{
    private const string Host = "127.0.0.1";
    private const int Port = 4000;
    private const string Region = "local";
    private const string ServerId = "dev";

    private readonly ITestOutputHelper _output;

    public ShooterReconnectReproTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task ReconnectIntoRunningBattle()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("ABILITYKIT_SHOOTER_GATEWAY_REPRO"),
                "1",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var timeout = TimeSpan.FromSeconds(20);
        var suffix = Guid.NewGuid().ToString("N").Substring(0, 8);

        var loginA = await DemoRoomGatewayAccountClient.LoginTcpAsync(Host, Port, $"repro-a-{suffix}", timeout);
        Assert.True(loginA.Success, $"login A failed: {loginA.Message}");
        var loginB = await DemoRoomGatewayAccountClient.LoginTcpAsync(Host, Port, $"repro-b-{suffix}", timeout);
        Assert.True(loginB.Success, $"login B failed: {loginB.Message}");

        var specA = ShooterRoomLaunchSpec.CreateDefault($"repro-a-{suffix}");
        var specB = ShooterRoomLaunchSpec.CreateDefault($"repro-b-{suffix}");

        var launcherA = ShooterClientNetworkLauncher.Create(ShooterClientConnectionFactory.Tcp());
        var launcherB = ShooterClientNetworkLauncher.Create(ShooterClientConnectionFactory.Tcp());
        try
        {
            await launcherA.EnsureConnectedAsync(Host, Port, timeout);
            await launcherB.EnsureConnectedAsync(Host, Port, timeout);
            var flowA = new ShooterRoomGatewayFlow(new ShooterRoomGatewayRoomClient(launcherA.GatewayConnection));
            var flowB = new ShooterRoomGatewayFlow(new ShooterRoomGatewayRoomClient(launcherB.GatewayConnection));
            var roomClientB = new ShooterRoomGatewayRoomClient(launcherB.GatewayConnection);

            // A 建房（一步式：创建+加入+准备+等开局，未等待——会阻塞在等 B 加入）。
            var createA = flowA.CreateReadyStartAndSubscribeAsync(loginA.SessionToken, specA, 1, timeout, CancellationToken.None);
            _output.WriteLine("A: create+join+ready started (awaiting B)...");

            // B 轮询房间列表发现 A 的房间。
            string roomId = string.Empty;
            for (var i = 0; i < 20 && string.IsNullOrEmpty(roomId); i++)
            {
                await Task.Delay(500);
                var list = await roomClientB.ListRoomsAsync(
                    new ShooterGatewayListRoomsRequest(loginB.SessionToken, Region, ServerId, 0, 10),
                    timeout);
                if (list.Success && list.Rooms.Count > 0)
                {
                    roomId = list.Rooms[0].RoomId;
                }
            }

            Assert.False(string.IsNullOrEmpty(roomId), "B did not discover A's room.");
            _output.WriteLine($"B: discovered room {roomId}");

            var joinB = await flowB.JoinReadyStartAndSubscribeAsync(loginB.SessionToken, roomId, specB, 2, timeout, CancellationToken.None);
            _output.WriteLine($"B: joined, entry={joinB.EntryKind} battle={joinB.BattleId}");
            var createAResult = await createA;
            _output.WriteLine($"A: battle entry={createAResult.EntryKind} room={createAResult.RoomId} battle={createAResult.BattleId}");

            // 战斗运行数秒。
            await Task.Delay(TimeSpan.FromSeconds(3));
            _output.WriteLine("battle running.");

            // A 关闭连接（模拟关游戏）。
            launcherA.Dispose();
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            _output.WriteLine("A: connection closed.");

            // A 重登（同一账号）+ 恢复。
            var launcherA2 = ShooterClientNetworkLauncher.Create(ShooterClientConnectionFactory.Tcp());
            try
            {
                await launcherA2.EnsureConnectedAsync(Host, Port, timeout);
                var roomClientA2 = new ShooterRoomGatewayRoomClient(launcherA2.GatewayConnection);

                var restore = await roomClientA2.RestoreRoomAsync(
                    new ShooterGatewayRestoreRoomRequest(loginA.SessionToken, Region, ServerId),
                    timeout);
                _output.WriteLine($"A2 restore: success={restore.Success} hasActiveRoom={restore.HasActiveRoom} isInBattle={restore.IsInBattle} room={restore.RoomId} msg={restore.Message}");

                Assert.True(restore.Success, $"restore failed: {restore.Message}");
                Assert.True(restore.HasActiveRoom, "restore reported no active room.");
                Assert.False(string.IsNullOrEmpty(restore.RoomId));

                var joinA2 = await roomClientA2.JoinRoomAsync(
                    new ShooterGatewayJoinRoomRequest(loginA.SessionToken, Region, ServerId, restore.RoomId),
                    timeout);
                _output.WriteLine($"A2 join: success={joinA2.Success} room={joinA2.RoomId} battleId={joinA2.BattleId} canStart={joinA2.CanStart} msg={joinA2.Message}");

                var snapshot = await roomClientA2.GetSnapshotAsync(
                    new ShooterGatewayGetRoomSnapshotRequest(loginA.SessionToken, restore.RoomId),
                    timeout);
                _output.WriteLine($"A2 snapshot: success={snapshot.Success} phase={snapshot.Snapshot?.Phase} reason={snapshot.Snapshot?.PhaseReason} msg={snapshot.Message}");

                Assert.True(joinA2.Success, $"re-join failed: {joinA2.Message}");

                // 正式控制器实际走会话层（ShooterGatewayRoomSession.JoinAsync → RefreshAsync → store）。
                // 复现并验证"no authoritative snapshot"竞态已修复。
                var storeA2 = new ShooterRoomSessionStore(roomClientA2 as IShooterRoomGatewaySnapshotFeed);
                var sessionA2 = new ShooterGatewayRoomSession(roomClientA2, storeA2);
                var specA2 = new ShooterRoomSessionLaunchSpec(loginA.SessionToken, specA, 1u, timeout);
                var sessionJoin = await sessionA2.JoinAsync(specA2, restore.RoomId);
                _output.WriteLine($"A2 session join: room={sessionJoin.RoomId} player={sessionJoin.PlayerId} battle={sessionJoin.BattleId} kind={sessionJoin.EntryKind} runningBattle={sessionJoin.JoinedRunningBattle}");
                Assert.False(string.IsNullOrEmpty(sessionJoin.RoomId), "session-level re-join returned no room.");
            }
            finally
            {
                launcherA2.Dispose();
            }
        }
        finally
        {
            launcherA.Dispose();
            launcherB.Dispose();
        }
    }
}
