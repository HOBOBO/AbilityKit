using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Recording.FrameRecord;
using AbilityKit.Game.Flow;
using AbilityKit.Game.Flow.Battle.Replay;
using Xunit;

namespace AbilityKit.Demo.Moba.View.Runtime.Tests;

[CollectionDefinition(nameof(BattleRecordCodecCollection), DisableParallelization = true)]
public sealed class BattleRecordCodecCollection { }

[Collection(nameof(BattleRecordCodecCollection))]
public sealed class BattleRecordFilesTests
{
    [Theory]
    [InlineData(BattleRecordCoverage.InputsAndHashes, ".BIN", 0, nameof(FrameRecordOptimizedBinaryCodec))]
    [InlineData(BattleRecordCoverage.WithSnapshots, ".bin", 2, nameof(FrameRecordOptimizedBinaryCodec))]
    [InlineData(BattleRecordCoverage.WithSnapshots, ".json", 2, nameof(FrameRecordJsonCodec))]
    public void RoundTripIsIndependentOfGlobalCodecAndReportsPersistedTracks(
        BattleRecordCoverage coverage, string extension, int snapshotCount, string codecName)
    {
        var path = Path.Combine(Path.GetTempPath(), $"abilitykit-moba-record-{Guid.NewGuid():N}{extension}");
        var reports = new List<string>();
        var previous = FrameRecordCodecs.Current;
        var poisoned = new UnexpectedCodec();
        FrameRecordCodecs.Current = poisoned;
        try
        {
            using (var writer = BattleRecordFiles.OpenWriter(path, coverage, new FrameRecordMeta { WorldId = "local" }, reports.Add))
            {
                writer.Append(new PlayerInputCommand(new FrameIndex(5), new PlayerId("1"), 71, [11]));
                writer.AppendStateHash(5, 3, 42u);
                writer.AppendSnapshot(5, 72, [21]);
                writer.AppendSnapshot(5, 73, [22]);
                Assert.Empty(reports);
                writer.Dispose();
                writer.AppendSnapshot(6, 74, [23]);
            }
            var report = Assert.Single(reports);
            Assert.Contains($"codec={codecName}", report);
            Assert.Contains($"coverage={coverage} inputs=1 hashes=1 snapshots={snapshotCount}", report);
            var record = BattleRecordFiles.Load(path);
            Assert.Equal(71, Assert.Single(record.Inputs).OpCode);
            var hash = Assert.Single(record.StateHashes);
            Assert.Equal((5, 3, 42u), (hash.Frame, hash.Version, hash.Hash));
            Assert.Equal(snapshotCount, record.Snapshots.Count);
            if (snapshotCount != 0) Assert.Equal(new[] { 72, 73 }, record.Snapshots.Select(snapshot => snapshot.OpCode));
            Assert.Same(poisoned, FrameRecordCodecs.Current);
        }
        finally
        {
            FrameRecordCodecs.Current = previous;
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(BattleRecordCoverage.InputsAndHashes, "record.json")]
    [InlineData(BattleRecordCoverage.WithSnapshots, "record.pmlr")]
    [InlineData(BattleRecordCoverage.WithSnapshots, " ")]
    [InlineData((BattleRecordCoverage)99, "record.bin")]
    public void StartPlanRejectsInvalidCoverageOrFileType(BattleRecordCoverage coverage, string path)
    {
        var builder = BattleStartPlanBuilder.ForWorld("world-1", "battle", "client-1", "1", 30, 0)
            .WithRunMode(BattleRunMode.Record, true, path, false, string.Empty, coverage);

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void ExplicitCoverageSurvivesPlanCopyAndTheLegacyOverloadDefaultsToMinimum()
    {
        var builder = BattleStartPlanBuilder.ForWorld("world-1", "battle", "client-1", "1", 30, 0);
        var minimum = builder.WithRunMode(BattleRunMode.Record, true, "record.bin", false, string.Empty).Build();
        var enhanced = builder.WithRunMode(BattleRunMode.Record, true, "record.json", false, string.Empty,
            BattleRecordCoverage.WithSnapshots).Build().WithGatewaySessionToken("token");

        Assert.Equal(BattleRecordCoverage.InputsAndHashes, minimum.RunModeOptions.RecordCoverage);
        Assert.Equal(BattleRecordCoverage.WithSnapshots, enhanced.RunModeOptions.RecordCoverage);
    }

    private sealed class UnexpectedCodec : IFrameRecordCodec
    {
        public IFrameRecordWriter CreateWriter(string outputPath, FrameRecordMeta meta) => throw new NotSupportedException();
        public FrameRecordFile Load(string path) => throw new NotSupportedException();
    }
}
