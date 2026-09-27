using AbilityKit.Game.Flow;
using AbilityKit.Game.View.Presentation;
using Xunit;

namespace AbilityKit.Demo.Moba.View.Runtime.Tests;

public sealed class MobaBattleViewBatchTests
{
    [Fact]
    public void Batch_ImplementsGenericPresentationContract()
    {
        var batch = new MobaBattleViewBatch(
            worldId: 42UL,
            frame: 120,
            sequence: 7UL,
            source: MobaBattleViewBatchSource.InterpolatedSnapshot,
            actorCount: 12,
            visibleActorCount: 8,
            presentationCueCount: 3);

        Assert.IsAssignableFrom<IViewBatch>(batch);
        Assert.Equal(42UL, batch.WorldId);
        Assert.Equal(120, batch.Frame);
        Assert.Equal(7UL, batch.Sequence);
        Assert.Equal(MobaBattleViewBatchSource.InterpolatedSnapshot, batch.Source);
        Assert.Equal(12, batch.ActorCount);
        Assert.Equal(8, batch.VisibleActorCount);
        Assert.Equal(3, batch.PresentationCueCount);
        Assert.True(batch.HasActors);
        Assert.True(batch.HasPresentationCues);
    }

    [Fact]
    public void Batch_ClampsNegativeCounts()
    {
        var batch = new MobaBattleViewBatch(1UL, 2, 3UL, MobaBattleViewBatchSource.Snapshot, -1, -2, -3);

        Assert.Equal(0, batch.ActorCount);
        Assert.Equal(0, batch.VisibleActorCount);
        Assert.Equal(0, batch.PresentationCueCount);
        Assert.False(batch.HasActors);
        Assert.False(batch.HasPresentationCues);
    }

    [Fact]
    public void Sink_BridgesGenericApplyBatchToMobaSpecificApplyBattleView()
    {
        var sink = new RecordingMobaBattleViewSink();
        IViewSink<MobaBattleViewBatch> genericSink = sink;
        var batch = new MobaBattleViewBatch(9UL, 10, 11UL, MobaBattleViewBatchSource.TriggerEvent, 1, 1, 2);

        genericSink.ApplyBatch(in batch);

        Assert.Equal(batch, sink.LastApplied);
        Assert.Equal(1, sink.ApplyCount);
    }

    [Fact]
    public void Sink_ClearResetsAppliedBatch()
    {
        var sink = new RecordingMobaBattleViewSink();
        sink.ApplyBattleView(new MobaBattleViewBatch(9UL, 10, 11UL, MobaBattleViewBatchSource.TriggerEvent, 1, 1, 2));

        sink.Clear();

        Assert.Equal(MobaBattleViewBatch.Empty, sink.LastApplied);
        Assert.Equal(0, sink.ApplyCount);
    }

    private sealed class RecordingMobaBattleViewSink : IMobaBattleViewSink
    {
        public MobaBattleViewBatch LastApplied { get; private set; } = MobaBattleViewBatch.Empty;

        public int ApplyCount { get; private set; }

        public void ApplyBattleView(in MobaBattleViewBatch batch)
        {
            LastApplied = batch;
            ApplyCount++;
        }

        public void Clear()
        {
            LastApplied = MobaBattleViewBatch.Empty;
            ApplyCount = 0;
        }
    }
}
