using System.Text;
using AbilityKit.Core.Recording.Core;
using Xunit;

namespace AbilityKit.Record.Tests;

public sealed class RecordIdHashTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("record-player-input")]
    [InlineData("录像-玩家-输入")]
    [InlineData("emoji-😀-record")]
    public void Fnv1a32MatchesUtf8ReferenceImplementation(string? value)
    {
        Assert.Equal(Fnv1a32Reference(value), RecordIdHash.Fnv1a32(value!));
    }

    [Fact]
    public void Fnv1a32MatchesUtf8FallbackForInvalidSurrogates()
    {
        string[] values = ["\uD800", "\uDC00", "left-\uD800-right"];

        foreach (var value in values)
            Assert.Equal(Fnv1a32Reference(value), RecordIdHash.Fnv1a32(value));
    }

    [Fact]
    public void Fnv1a32HasZeroSteadyStateAllocations()
    {
        const string value = "record-录像-😀-player-input";
        var checksum = 0;

        for (var index = 0; index < 256; index++)
            checksum ^= RecordIdHash.Fnv1a32(value);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 1024; index++)
            checksum ^= RecordIdHash.Fnv1a32(value);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, checksum);
        Assert.Equal(0, allocated);
    }

    private static int Fnv1a32Reference(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return 0;

        var bytes = Encoding.UTF8.GetBytes(value);
        unchecked
        {
            uint hash = 2166136261u;
            foreach (var item in bytes)
            {
                hash ^= item;
                hash *= 16777619u;
            }
            return (int)hash;
        }
    }
}
