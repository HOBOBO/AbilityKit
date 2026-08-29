using System;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Demo.Shooter.View;
using AbilityKit.Protocol.Shooter;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests.Client;

/// <summary>
/// 正式远程纯状态模式的合成渲染批契约：
/// ① 己方角色由本地预测独占（随输入前进，不被权威批覆盖/剔除、永不回拉）；
/// ② 远端实体的变换只来自插值播放流（每个实体一个变换，不与原始权威批重复）。
/// 复刻服务端行为：观察者自己的角色从纯状态导出中剔除。
/// </summary>
public sealed class ShooterRemotePureStateCompositionTests
{
    [Fact]
    public void ComposedBatchKeepsOwnPlayerPredictionAndSingleTransformPerRemoteEntity()
    {
        var runtime = new ShooterBattleRuntimePort();
        var presentation = new ShooterPresentationFacade { ControlledPlayerId = 1 };
        var controller = new ShooterClientAuthoritativeInterpolationSyncController(
            runtime, presentation, tickRate: 30, decoder: null, gateway: null);
        var start = new ShooterStartGamePayload(
            "remote-pure-composition",
            30,
            7,
            new[]
            {
                new ShooterStartPlayer(1, "P1", 0f, 0f),
                new ShooterStartPlayer(2, "P2", 3f, 0f)
            });
        Assert.True(controller.StartGame(in start));

        // 全量基线（frame 5）：只含远端玩家 P2；服务端剔除观察者自己的 P1。
        // 基线自带 (frame, stateHash) 锚，与真实服务端一致，后续增量引用该锚。
        controller.BufferRemoteSnapshot(PureStateSnapshot(frame: 5, remoteX: 3f, isFull: true));

        // 真实节奏交错：60fps 渲染 × 30Hz 模拟 × 每 3 帧一次权威推送。
        // 逐渲染帧即时断言（RenderBatch 的组合列表是复用实例，离线快照不可靠）：
        // 远端 P2 至多一个变换；己方 P1 一旦出现就单调前进、永不回拉。
        var authorityFrame = 5;
        var remoteX = 3f;
        var ownProgressed = false;
        var lastOwnX = -1f;
        var ownSeen = false;
        for (var render = 0; render < 24; render++)
        {
            if (render % 2 == 0)
            {
                controller.SubmitLocalInput(1, 1f, 0f, 1f, 0f, false);
            }

            if (render % 6 == 0)
            {
                authorityFrame += 3;
                remoteX += 0.5f;
                controller.BufferRemoteSnapshot(PureStateSnapshot(
                    frame: authorityFrame,
                    remoteX,
                    isFull: false,
                    baselineFrame: 5,
                    baselineHash: 123u));
            }

            controller.Tick(1f / 60f);
            var batch = presentation.RenderBatch;
            var remoteCount = CountTransforms(batch, 2);
            Assert.True(remoteCount <= 1,
                $"render #{render}: remote entity P2 must have at most one transform in the composed batch, got {remoteCount} (raw authority batch must not double-write over the interpolation playback)");

            var own = FindTransform(batch, 1);
            if (own.HasValue)
            {
                ownSeen = true;
                Assert.True(own.Value.X >= lastOwnX - 0.001f,
                    $"render #{render}: own player must never be pulled behind its prediction, lastX={lastOwnX} now={own.Value.X}");
                lastOwnX = own.Value.X;
                ownProgressed |= own.Value.X > 0.5f;
            }
            else
            {
                // 首个本地预测 tick 之前允许缺席；此后任何渲染帧（含权威推送帧）都必须在场。
                Assert.True(!ownSeen && controller.CurrentFrame == 0,
                    $"render #{render}: own player disappeared after prediction started (authority push must not evict the predicted local batch)");
            }
        }

        Assert.True(ownProgressed, $"own player must advance with input across the session, finalX={lastOwnX}");

        // 播放流确实收到了权威变换（确保上面的断言不是在空播放流上平凡通过）。
        Assert.True(controller.PureStatePlaybackDiagnostics.PublishedSnapshotCount >= 3);
    }

    private static ShooterViewTransformComponentChange? FindTransform(
        in ShooterSnapshotViewBatch batch,
        int entityId)
    {
        for (var i = 0; i < batch.TransformChanges.Count; i++)
        {
            if (batch.TransformChanges[i].Key.EntityId == entityId)
            {
                return batch.TransformChanges[i];
            }
        }

        return null;
    }

    private static int CountTransforms(in ShooterSnapshotViewBatch batch, int entityId)
    {
        var count = 0;
        for (var i = 0; i < batch.TransformChanges.Count; i++)
        {
            if (batch.TransformChanges[i].Key.EntityId == entityId)
            {
                count++;
            }
        }

        return count;
    }

    private static ShooterGatewaySnapshot PureStateSnapshot(
        int frame,
        float remoteX,
        bool isFull,
        int baselineFrame = 0,
        uint baselineHash = 0u)
    {
        var settings = new ShooterPureStateSyncSettings(
            maxEntityCount: 20,
            activeSyncBudget: 20,
            baselineIntervalFrames: 450,
            deltaIntervalFrames: 3,
            lowFrequencyIntervalFrames: 90,
            interpolationDelayFrames: 3,
            nearLodIntervalFrames: 3,
            midLodIntervalFrames: 9,
            farLodIntervalFrames: 30);
        var pureState = new ShooterPureStateSnapshotPayload(
            ShooterPureStateSyncCodec.CurrentVersion,
            9001ul,
            frame,
            frame * 100L,
            isFull ? ShooterPureStateSnapshotKinds.FullBaseline : ShooterPureStateSnapshotKinds.Delta,
            // 服务端行为：全量基线自带 (frame, stateHash) 作为后续增量的基线锚。
            isFull ? frame : baselineFrame,
            isFull ? 123u : baselineHash,
            123u,
            settings,
            new[]
            {
                new ShooterPureStateEntityDelta(
                    2,
                    ShooterPackedEntityKinds.Player,
                    ShooterPureStateEntityLayers.KeyInteraction,
                    isFull ? ShooterPureStateDeltaKinds.Spawn : ShooterPureStateDeltaKinds.Update,
                    2,
                    (int)(remoteX * 1000f),
                    0,
                    0,
                    0,
                    0,
                    0,
                    1000,
                    0,
                    0,
                    (byte)(ShooterPureStateEntityFlags.Alive | ShooterPureStateEntityFlags.Visible))
            },
            Array.Empty<ShooterPureStateVisibilityHint>());
        return new ShooterGatewaySnapshot(
            9001ul,
            frame,
            0d,
            frame * 100L,
            isFull,
            Array.Empty<ShooterGatewayActorSnapshot>(),
            pureStateSnapshot: pureState);
    }
}
