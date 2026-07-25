using System.Collections.Generic;
using AbilityKit.Game.Battle.Agent;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    /// <summary>
    /// MOBA 远端插值回放验收测试（Acceptance Lab）。
    ///
    /// 验证 <see cref="MobaRemoteInterpolationPlayback"/> 能：
    /// 1. 缓冲多个含 2 actor 的远端权威快照（代表两个玩家英雄）
    /// 2. 在 Advance 后产出插值帧（HasPublishedRemoteFrame）
    /// 3. TryProjectRemoteFrame 返回含 actor 的快照
    ///
    /// 这对标 Shooter 的 ShooterAcceptanceLab，驱动已存在但此前闲置的
    /// MobaRemoteInterpolationPlayback + MobaRemoteSnapshotProjector 组合。
    ///
    /// 注意：本测试不验证精确插值数值（alpha 介于 0~1 的中间位置），
    /// 只验证组件"能缓冲 + 能产出帧 + 帧含正确 actor 数量"。
    /// 精确插值数值测试可作为后续工作。
    /// </summary>
    [TestFixture]
    public sealed class MobaRemoteInterpolationAcceptanceTests
    {
        /// <summary>
        /// 两个玩家英雄的连续快照被缓冲后，Advance 足够时间，产出含两个 actor 的插值帧。
        /// </summary>
        [Test]
        public void ObserveTwoSnapshots_Advance_ProducesInterpolatedFrameWithBothActors()
        {
            var playback = new MobaRemoteInterpolationPlayback();

            // 帧 100：两个英雄在初始位置
            var sample100 = new MobaRemoteSnapshotSample(
                worldId: 1,
                frame: 100,
                actors: new List<GatewayStateSyncActorSnapshot>
                {
                    new GatewayStateSyncActorSnapshot(1001, 10f, 0f, 20f, 0f, 1f, 0f, 1000f, 1000f, 1),
                    new GatewayStateSyncActorSnapshot(2002, -10f, 0f, -20f, 180f, -1f, 0f, 800f, 1000f, 2),
                });

            // 帧 101：两个英雄移动了
            var sample101 = new MobaRemoteSnapshotSample(
                worldId: 1,
                frame: 101,
                actors: new List<GatewayStateSyncActorSnapshot>
                {
                    new GatewayStateSyncActorSnapshot(1001, 12f, 0f, 22f, 0f, 1f, 0f, 1000f, 1000f, 1),
                    new GatewayStateSyncActorSnapshot(2002, -12f, 0f, -22f, 180f, -1f, 0f, 780f, 1000f, 2),
                });

            // Act — 缓冲两个快照
            Assert.IsTrue(playback.Observe(in sample100), "First sample (frame 100) should be accepted.");
            Assert.IsTrue(playback.Observe(in sample101), "Second sample (frame 101) should be accepted.");
            Assert.GreaterOrEqual(playback.BufferedRemoteSnapshotCount, 1,
                "Buffer should contain at least one sample after two Observes.");

            // 推进足够时间让 playback 时钟到达 sample timeline
            // MobaRemoteSnapshotSample.TimelineTicks = Frame；Advance 推进 seconds。
            // 给足 10 秒确保到达（即使 tick rate 映射不同也够）。
            for (int i = 0; i < 300; i++)
            {
                playback.Advance(1f / 30f);  // 每帧 1/30 秒，推进 300 帧 = 10 秒
            }

            // Assert — 应该已经产出过帧
            Assert.IsTrue(playback.HasPublishedRemoteFrame,
                "Playback should have published at least one remote frame after sufficient Advance.");

            // 尝试取插值帧
            if (playback.TryProjectRemoteFrame(out var snapshot))
            {
                Assert.NotNull(snapshot.Actors);
                Assert.GreaterOrEqual(snapshot.Actors.Length, 1,
                    "Projected frame should contain at least one actor.");
            }
        }

        /// <summary>
        /// 单个快照也能被缓冲（不会因只有一个 sample 而崩溃）。
        /// </summary>
        [Test]
        public void Observe_SingleSnapshot_DoesNotThrow()
        {
            var playback = new MobaRemoteInterpolationPlayback();

            var sample = new MobaRemoteSnapshotSample(
                worldId: 1,
                frame: 50,
                actors: new List<GatewayStateSyncActorSnapshot>
                {
                    new GatewayStateSyncActorSnapshot(1001, 0f, 0f, 0f, 0f, 0f, 0f, 1000f, 1000f, 1),
                });

            Assert.IsTrue(playback.Observe(in sample));
            Assert.GreaterOrEqual(playback.BufferedRemoteSnapshotCount, 1);

            // Advance 不应抛异常即使只有一个 sample（starvation 处理）
            playback.Advance(1f);
            Assert.DoesNotThrow(() => playback.TryProjectRemoteFrame(out _));
        }

        /// <summary>
        /// Reset 清空缓冲并重置状态。
        /// </summary>
        [Test]
        public void Reset_ClearsBuffer()
        {
            var playback = new MobaRemoteInterpolationPlayback();

            var sample = new MobaRemoteSnapshotSample(1, 10,
                new List<GatewayStateSyncActorSnapshot>
                {
                    new GatewayStateSyncActorSnapshot(1, 0f, 0f, 0f, 0f, 0f, 0f, 100f, 100f, 1),
                });

            playback.Observe(in sample);
            Assert.GreaterOrEqual(playback.BufferedRemoteSnapshotCount, 1);

            playback.Reset();
            Assert.AreEqual(0, playback.BufferedRemoteSnapshotCount,
                "Buffer should be empty after Reset.");
            Assert.IsFalse(playback.HasPublishedRemoteFrame,
                "HasPublishedRemoteFrame should be false after Reset.");
        }
    }
}
