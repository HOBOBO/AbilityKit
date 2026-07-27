using AbilityKit.Core.Recording.FrameRecord;
using AbilityKit.Game.Flow.Battle.Replay;
using Xunit;

namespace AbilityKit.Demo.Moba.View.Runtime.Tests;

public sealed class BattleReplayManifestTests
{
    [Fact]
    public void TryCreate_BuildsOrderedSeekAnchorsAndClampsTarget()
    {
        var file = CreateFile();

        var created = BattleReplayManifest.TryCreate(file, out var manifest, out var error);

        Assert.True(created, error);
        Assert.Equal(BattleReplayManifest.CurrentSchemaVersion, manifest.SchemaVersion);
        Assert.Equal(10, manifest.FirstFrame);
        Assert.Equal(899, manifest.LastFrame);
        Assert.Equal(2, manifest.SeekAnchors.Count);
        Assert.Equal(600, manifest.ResolveSeekAnchor(742).StartFrame);
        Assert.Equal(600, manifest.ResolveSeekAnchor(999).StartFrame);
    }

    [Fact]
    public void IsCompatibleWith_RejectsWorldAndTickRateMismatch()
    {
        var created = BattleReplayManifest.TryCreate(CreateFile(), out var manifest, out var error);
        Assert.True(created, error);

        Assert.True(manifest.IsCompatibleWith("room-1", "moba", 30, out error), error);

        Assert.False(manifest.IsCompatibleWith("room-2", "moba", 60, out error));
        Assert.Contains("WorldId", error);
    }

    private static FrameRecordFile CreateFile()
    {
        return new FrameRecordFile
        {
            Meta = new FrameRecordMeta
            {
                WorldId = "room-1",
                WorldType = "moba",
                PlayerId = "player-1",
                TickRate = 30,
                RandomSeed = 42,
                StartedAtUnixMs = 1,
            },
            Inputs = new List<FrameRecordInputFrame>
            {
                new() { Frame = 10, PlayerId = "player-1" },
                new() { Frame = 650, PlayerId = "player-1" },
            },
            StateHashes = new List<FrameRecordStateHashFrame>
            {
                new() { Frame = 899, Version = 1, Hash = 1 },
            },
            Index = new List<FrameRecordChunkIndex>
            {
                new() { StartFrame = 600, EndFrame = 899 },
                new() { StartFrame = 300, EndFrame = 599 },
            },
        };
    }
}
