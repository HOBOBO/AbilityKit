using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Recording.FrameRecord;
using Newtonsoft.Json;
using Xunit;

namespace AbilityKit.Record.Tests;

[CollectionDefinition(nameof(FrameRecordCodecSelectionCollection), DisableParallelization = true)]
public sealed class FrameRecordCodecSelectionCollection
{
}

[Collection(nameof(FrameRecordCodecSelectionCollection))]
public sealed class FrameRecordDefaultCodecTests
{
    [Theory]
    [InlineData(".bin", true)]
    [InlineData(".BIN", true)]
    [InlineData(".json", false)]
    [InlineData("", false)]
    public void DefaultCodec_RoundTripsMetadataAndAllTracks(string extension, bool binary)
    {
        var path = NewRecordPath(extension);
        var meta = new FrameRecordMeta
        {
            WorldId = "local-world",
            WorldType = "roundtrip",
            TickRate = 30,
            RandomSeed = -42,
            PlayerId = "player-a",
            StartedAtUnixMs = 1_700_000_000_123L,
        };

        try
        {
            using (var writer = FrameRecordCodecs.Current.CreateWriter(path, meta))
            {
                writer.Append(new PlayerInputCommand(new FrameIndex(9), new PlayerId("player-a"), 201, new byte[] { 1, 2, 3 }));
                writer.Append(new PlayerInputCommand(new FrameIndex(9), new PlayerId("player-b"), 202, Array.Empty<byte>()));
                writer.Append(new PlayerInputCommand(new FrameIndex(305), new PlayerId("player-a"), 203, new byte[] { 4 }));
                writer.AppendStateHash(9, 7, 0xDEADBEEFu);
                writer.AppendStateHash(305, 2, 0u);
                writer.AppendSnapshot(9, 301, new byte[] { 5 });
                writer.AppendSnapshot(9, 302, Array.Empty<byte>());
                writer.AppendSnapshot(305, 303, new byte[] { 6 });
            }

            var record = FrameRecordCodecs.Current.Load(path);
            Assert.Equal((meta.WorldId, meta.WorldType, meta.TickRate, meta.RandomSeed, meta.PlayerId, meta.StartedAtUnixMs),
                (record.Meta.WorldId, record.Meta.WorldType, record.Meta.TickRate, record.Meta.RandomSeed, record.Meta.PlayerId, record.Meta.StartedAtUnixMs));
            Assert.Equal(new[] { (9, "player-a", 201, "AQID"), (9, "player-b", 202, ""), (305, "player-a", 203, "BA==") },
                record.Inputs.Select(input => (input.Frame, input.PlayerId, input.OpCode, input.PayloadBase64)));
            Assert.Equal(new[] { (9, 7, 0xDEADBEEFu), (305, 2, 0u) },
                record.StateHashes.Select(hash => (hash.Frame, hash.Version, hash.Hash)));
            Assert.Equal(new[] { (9, 301, "BQ=="), (9, 302, ""), (305, 303, "Bg==") },
                record.Snapshots.Select(snapshot => (snapshot.Frame, snapshot.OpCode, snapshot.PayloadBase64)));

            if (binary)
            {
                using var reader = new BinaryReader(File.OpenRead(path));
                Assert.Equal(0x52464B41u, reader.ReadUInt32());
                Assert.Equal(4, reader.ReadInt32());
                Assert.Null(record.Index);
            }
            else
            {
                Assert.StartsWith("{", File.ReadAllText(path).TrimStart());
                Assert.Equal(new[] { (0, 299, 0, 2, 0, 1, 0, 2), (300, 599, 2, 3, 1, 2, 2, 3) },
                    record.Index.Select(chunk => (chunk.StartFrame, chunk.EndFrame, chunk.InputStart, chunk.InputEnd,
                        chunk.StateHashStart, chunk.StateHashEnd, chunk.SnapshotStart, chunk.SnapshotEnd)));
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void DefaultCodec_DoesNotRetryJsonForBinExtension()
    {
        var path = NewRecordPath(".bin");
        try
        {
            File.WriteAllText(path, "{\"Meta\":{}}");
            Assert.Throws<InvalidDataException>(() => FrameRecordCodecs.Current.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void DefaultCodec_RejectsTruncatedJson()
    {
        var path = NewRecordPath(".json");
        try
        {
            File.WriteAllText(path, "{\"Meta\":{");
            Assert.ThrowsAny<JsonException>(() => FrameRecordCodecs.Current.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Current_ExplicitOverrideCanBeResetToTheBuiltInDispatcher()
    {
        var initial = FrameRecordCodecs.Current;
        var replacement = new FrameRecordJsonCodec();
        try
        {
            FrameRecordCodecs.Current = replacement;
            Assert.Same(replacement, FrameRecordCodecs.Current);

            FrameRecordCodecs.Current = null;
            Assert.Same(initial, FrameRecordCodecs.Current);
        }
        finally
        {
            FrameRecordCodecs.Current = null;
        }
    }

    private static string NewRecordPath(string extension) =>
        Path.Combine(Path.GetTempPath(), $"abilitykit-record-default-{Guid.NewGuid():N}{extension}");
}
