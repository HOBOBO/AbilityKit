using AbilityKit.Game.Battle.Agent;
using AbilityKit.Game.Flow;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Sync;
using Xunit;

namespace AbilityKit.Demo.Moba.View.Runtime.Tests;

/// <summary>
/// 断线重连（P1 端到端）的客户端行为验证：
/// - 重连退避节奏（指数退避 + 上限）
/// - 重连后插值缓冲重置——旧时间线失效，直到新快照到达前不再投影
/// </summary>
public sealed class BattleSessionReconnectTests
{
    private static InterpolationConfig Config() =>
        new InterpolationConfig(
            ticksPerSecond: 1L,
            interpolationDelayTicks: 1L,
            bufferCapacity: 16,
            catchUpRate: 0d,
            maxExtrapolationTicks: 50L);

    private static GatewayStateSyncSnapshot Snapshot(int frame)
    {
        var actors = new[]
        {
            new GatewayStateSyncActorSnapshot(
                actorId: 1, x: frame, y: 0f, z: 0f,
                rotation: 0f, velocityX: 0f, velocityZ: 0f,
                hp: 100f, hpMax: 100f, teamId: 1),
        };

        return new GatewayStateSyncSnapshot(worldId: 7UL, frame: frame, timestamp: 0d, isFullSnapshot: true, actors: actors);
    }

    [Fact]
    public void ReconnectDelay_ProgressesExponentially_AndCapsAtMax()
    {
        Assert.Equal(1f, ReconnectBackoffPolicy.ResolveDelay(0));
        Assert.Equal(2f, ReconnectBackoffPolicy.ResolveDelay(1));
        Assert.Equal(4f, ReconnectBackoffPolicy.ResolveDelay(2));
        Assert.Equal(8f, ReconnectBackoffPolicy.ResolveDelay(3));

        // 16s 超过 15s 上限，后续所有尝试都封顶
        Assert.Equal(15f, ReconnectBackoffPolicy.ResolveDelay(4));
        Assert.Equal(15f, ReconnectBackoffPolicy.ResolveDelay(10));
    }

    [Fact]
    public void PlaybackReset_ClearsBufferedSnapshots_UntilNewSnapshotArrives()
    {
        var playback = new MobaRemoteInterpolationPlayback(Config());

        // 模拟断线前：缓冲了两个快照，播放正常
        var snap1 = Snapshot(1);
        var snap2 = Snapshot(2);
        playback.Observe(in snap1);
        playback.Observe(in snap2);
        playback.Advance(1f);
        Assert.True(playback.BufferedRemoteSnapshotCount > 0);

        // 重连重置：缓冲与时间线清空
        playback.Reset();

        Assert.Equal(0, playback.BufferedRemoteSnapshotCount);
        Assert.False(playback.HasPublishedRemoteFrame);
        Assert.False(playback.TryProjectRemoteFrame(out _));

        // 重连后新快照到达（服务端 FullSnapshot），播放恢复
        var snap100 = Snapshot(100);
        var snap101 = Snapshot(101);
        playback.Observe(in snap100);
        playback.Observe(in snap101);
        playback.Advance(1f);

        Assert.True(playback.BufferedRemoteSnapshotCount > 0);
    }
}
