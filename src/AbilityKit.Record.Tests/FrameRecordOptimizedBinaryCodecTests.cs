using System.IO.Compression;
using AbilityKit.Core.Recording.FrameRecord;
using Xunit;

namespace AbilityKit.Record.Tests;

public sealed class FrameRecordOptimizedBinaryCodecTests
{
    private const uint Magic = 0x52464B41;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Version4RoundTripsStateHashSchemaVersions(bool useCompression)
    {
        var path = NewRecordPath();
        try
        {
            using (var writer = new FrameRecordOptimizedBinaryWriter(
                       path,
                       CreateMeta(),
                       useCompression,
                       CompressionLevel.Fastest))
            {
                writer.AppendStateHash(10, 7, 100u);
                writer.AppendStateHash(20, 7, uint.MaxValue);
                writer.AppendStateHash(35, 42, 3u);
            }

            var data = FrameRecordOptimizedBinaryReader.Load(path);

            Assert.Equal(3, data.StateHashCount);
            Assert.Equal(new[] { 10, 20, 35 }, data.StateHashFrames);
            Assert.Equal(new[] { 7, 7, 42 }, data.StateHashVersions);
            Assert.Equal(new uint[] { 100u, uint.MaxValue, 3u }, data.StateHashValues);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReaderPreservesLegacyVersion3LayoutCompatibility()
    {
        var path = NewRecordPath();
        try
        {
            WriteRawRecord(path, version: 3, payloadWriter =>
            {
                payloadWriter.Write(0); // inputs
                payloadWriter.Write(0); // players
                payloadWriter.Write(0); // snapshots
                payloadWriter.Write(2); // state hashes
                WriteSignedVarInt(payloadWriter, 10);
                WriteSignedVarInt(payloadWriter, 100);
                WriteSignedVarInt(payloadWriter, 10);
                WriteSignedVarInt(payloadWriter, 50);
            });

            var data = FrameRecordOptimizedBinaryReader.Load(path);

            Assert.Equal(new[] { 10, 20 }, data.StateHashFrames);
            Assert.Equal(new[] { 0, 1 }, data.StateHashVersions);
            Assert.Equal(new uint[] { 100u, 150u }, data.StateHashValues);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReaderRejectsUnsupportedFutureVersion()
    {
        var path = NewRecordPath();
        try
        {
            WriteRawRecord(path, version: 5, _ => { });

            var error = Assert.Throws<InvalidDataException>(() => FrameRecordOptimizedBinaryReader.Load(path));
            Assert.Contains("Unsupported", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReaderRejectsNegativeTrackCount()
    {
        var path = NewRecordPath();
        try
        {
            WriteRawRecord(path, version: 4, payloadWriter => payloadWriter.Write(-1));

            var error = Assert.Throws<InvalidDataException>(() => FrameRecordOptimizedBinaryReader.Load(path));
            Assert.Contains("input count", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReaderRejectsTruncatedPayload()
    {
        var path = NewRecordPath();
        try
        {
            WriteRawRecord(path, version: 4, payloadWriter =>
            {
                payloadWriter.Write(1); // inputs
                payloadWriter.Write(1); // players
                payloadWriter.Write("player-1");
                WriteSignedVarInt(payloadWriter, 1);
                WriteSignedVarInt(payloadWriter, 2);
                WriteUnsignedVarInt(payloadWriter, 0);
                WriteUnsignedVarInt(payloadWriter, 4);
                payloadWriter.Write(new byte[] { 1, 2 });
            });

            Assert.Throws<EndOfStreamException>(() => FrameRecordOptimizedBinaryReader.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static FrameRecordMeta CreateMeta()
    {
        return new FrameRecordMeta
        {
            WorldId = "world-1",
            WorldType = "test",
            TickRate = 30,
            RandomSeed = 42,
            PlayerId = "player-1",
            StartedAtUnixMs = 1L,
        };
    }

    private static string NewRecordPath()
    {
        return Path.Combine(Path.GetTempPath(), $"abilitykit-record-{Guid.NewGuid():N}.bin");
    }

    private static void WriteRawRecord(string path, int version, Action<BinaryWriter> writePayload)
    {
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new BinaryWriter(stream);
        writer.Write(Magic);
        writer.Write(version);
        writer.Write(false);
        WriteMeta(writer, CreateMeta());
        writer.Write(0);
        writer.Write(0);
        writePayload(writer);
    }

    private static void WriteMeta(BinaryWriter writer, FrameRecordMeta meta)
    {
        writer.Write(meta.WorldId);
        writer.Write(meta.WorldType);
        writer.Write(meta.TickRate);
        writer.Write(meta.RandomSeed);
        writer.Write(meta.PlayerId);
        writer.Write(meta.StartedAtUnixMs);
    }

    private static void WriteSignedVarInt(BinaryWriter writer, int value)
    {
        WriteUnsignedVarInt(writer, (uint)((value << 1) ^ (value >> 31)));
    }

    private static void WriteUnsignedVarInt(BinaryWriter writer, uint value)
    {
        while (value >= 0x80u)
        {
            writer.Write((byte)(value | 0x80u));
            value >>= 7;
        }

        writer.Write((byte)value);
    }
}
