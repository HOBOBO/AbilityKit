using System.Diagnostics;
using System.Numerics;
using AbilityKit.Core.Identifiers;
using AbilityKit.Core.Timing;
using Xunit;

namespace AbilityKit.Core.Tests;

/*
 * 固定编码、零标识与真实哈希冲突向量，避免注册状态和持久化标识在整理时漂移。
 * 时间换算采用任意精度整数公式作独立参照，覆盖中间溢出、向零截断、向上取整和饱和。
 */
public sealed class IdentifiersAndTimingContractTests
{
    [Theory]
    [InlineData(0x0, 84696351)]
    [InlineData(0x7F, -99857458)]
    [InlineData(0x80, 1537247071)]
    [InlineData(0x7FF, 281926787)]
    [InlineData(0x800, -1150314057)]
    [InlineData(0xFFFF, 21469476)]
    [InlineData(0x10000, 364204981)]
    [InlineData(0x10FFFF, 1103215674)]
    public void Utf8_golden_vectors_cover_encoding_width_boundaries(int codePoint, int expected)
    {
        Assert.Equal(expected, StableHashV1.Fnv1a32Utf8(char.ConvertFromUtf32(codePoint)));
    }

    [Fact]
    public void Utf8_replacement_preserves_the_following_valid_surrogate_pair()
    {
        var value = new string(new[] { (char)0xD800, (char)0xD800, (char)0xDC00, (char)0xDC00 });

        Assert.Equal(1811481799, StableHashV1.Fnv1a32Utf8(value));
        Assert.Equal(55024714, StableHashV1.Fnv1a32Utf8(new string((char)0xDC00, 1)));
        Assert.Equal(55024714, StableHashV1.Fnv1a32Utf8(new string((char)0xFFFD, 1)));
    }

    [Theory]
    [InlineData(0xD800, 75617567)]
    [InlineData(0xDC00, 75204895)]
    public void Utf16_preserves_unpaired_surrogate_code_units(int codeUnit, int expected)
    {
        var value = new string((char)codeUnit, 1);

        Assert.Equal(expected, StableHashV1.Fnv1a32Utf16(value));
        Assert.Equal(expected & int.MaxValue, StableHashV1.Fnv1a32Utf16NonNegative(value));
    }

    [Fact]
    public void Nonnegative_hash_entry_rejects_null()
    {
        Assert.Equal("value",
            Assert.Throws<ArgumentNullException>(() => StableHashV1.Fnv1a32Utf16NonNegative(null!)).ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ability.cast")]
    [InlineData("能力")]
    public void Repeated_registration_preserves_both_lookup_directions(string name)
    {
        var registry = new StableStringIdRegistry();
        var id = registry.GetOrRegister(name);

        Assert.Equal(id, registry.GetOrRegister(new string(name.ToCharArray())));
        Assert.True(registry.TryGetId(name, out var registeredId));
        Assert.Equal(id, registeredId);
        Assert.True(registry.TryGetName(id, out var registeredName));
        Assert.Equal(name, registeredName);
    }

    [Fact]
    public void Missing_lookups_return_documented_defaults_without_registering_names()
    {
        var registry = new StableStringIdRegistry();

        Assert.False(registry.TryGetId(null!, out var nullId));
        Assert.Equal(0, nullId);
        Assert.False(registry.TryGetId("missing", out var missingId));
        Assert.Equal(0, missingId);
        Assert.False(registry.TryGetName(StableHashV1.Fnv1a32Utf16("missing"), out var missingName));
        Assert.Equal(string.Empty, missingName);
        Assert.Equal("name", Assert.Throws<ArgumentNullException>(() => registry.GetOrRegister(null!)).ParamName);
    }

    [Fact]
    public void Zero_and_negative_ids_are_successful_values_rather_than_failure_sentinels()
    {
        var registry = new StableStringIdRegistry();
        var zeroName = new string(new[] { (char)0xCB33, (char)0xA1D9, (char)0x0001 });

        Assert.Equal(0, StableHashV1.Fnv1a32Utf16(zeroName));
        Assert.Equal(0, registry.GetOrRegister(zeroName));
        Assert.True(registry.TryGetId(zeroName, out var zeroId));
        Assert.Equal(0, zeroId);
        Assert.True(registry.TryGetName(0, out var name));
        Assert.Equal(zeroName, name);

        Assert.Equal(-2128831035, registry.GetOrRegister(string.Empty));
        Assert.True(registry.TryGetName(-2128831035, out var emptyName));
        Assert.Equal(string.Empty, emptyName);
    }

    [Theory]
    [InlineData("costarring", "liquid")]
    [InlineData("liquid", "costarring")]
    public void Hash_collision_preserves_the_first_name_and_rejects_the_second(string first, string second)
    {
        var registry = new StableStringIdRegistry();

        Assert.Equal(1582148253, registry.GetOrRegister(first));
        Assert.Equal(1582148253, StableHashV1.Fnv1a32Utf16(second));
        Assert.Throws<InvalidOperationException>(() => registry.GetOrRegister(second));
        Assert.True(registry.TryGetName(1582148253, out var name));
        Assert.Equal(first, name);
        Assert.True(registry.TryGetId(first, out var originalId));
        Assert.Equal(1582148253, originalId);
        Assert.False(registry.TryGetId(second, out var rejectedId));
        Assert.Equal(0, rejectedId);
        Assert.Throws<InvalidOperationException>(() => registry.GetOrRegister(second));
        Assert.Equal(1868853449, registry.GetOrRegister("能力"));
    }

    [Fact]
    public void Names_use_ordinal_case_sensitive_comparison_without_unicode_normalization()
    {
        var registry = new StableStringIdRegistry();
        var composed = "é";
        var decomposed = string.Concat("e", (char)0x0301);

        Assert.NotEqual(registry.GetOrRegister("I"), registry.GetOrRegister("i"));
        Assert.NotEqual(registry.GetOrRegister(composed), registry.GetOrRegister(decomposed));
        Assert.NotEqual(StableHashV1.Fnv1a32Utf8(composed), StableHashV1.Fnv1a32Utf8(decomposed));
        Assert.True(registry.TryGetId(composed, out var composedId));
        Assert.True(registry.TryGetName(composedId, out var name));
        Assert.Equal(composed, name);
    }

    [Fact]
    public void Independent_registries_have_order_independent_ids_and_local_reverse_mappings()
    {
        var first = new StableStringIdRegistry();
        var second = new StableStringIdRegistry();

        var firstId = first.GetOrRegister("ability.cast");
        Assert.False(second.TryGetName(firstId, out _));
        second.GetOrRegister("能力");
        Assert.Equal(firstId, second.GetOrRegister("ability.cast"));
        Assert.Equal(first.GetOrRegister("能力"), second.GetOrRegister("能力"));

        Assert.Equal(first.GetOrRegister("costarring"), second.GetOrRegister("liquid"));
    }

    [Fact]
    public void Millisecond_conversion_matches_exact_integer_arithmetic_at_extreme_values()
    {
        long[] timestamps =
        {
            0, 1, -1, long.MaxValue, long.MinValue,
            long.MaxValue / 1000, long.MaxValue / 1000 + 1,
            long.MinValue / 1000, long.MinValue / 1000 - 1
        };
        long[] frequencies = { 1, 3, 999, 1000, 1001, 10_000_000, long.MaxValue };

        foreach (var timestamp in timestamps)
        foreach (var frequency in frequencies)
        {
            var expected = (BigInteger)timestamp * 1000 / frequency;
            if (expected > long.MaxValue || expected < long.MinValue)
            {
                Assert.Throws<OverflowException>(() => MonotonicTime.ToMilliseconds(timestamp, frequency));
            }
            else
            {
                Assert.Equal((long)expected, MonotonicTime.ToMilliseconds(timestamp, frequency));
            }
        }
    }

    [Fact]
    public void Duration_conversion_matches_exact_ceiling_and_saturation_at_extreme_frequencies()
    {
        long[] durationTicks = { 0, 1, 9_999_999, 10_000_000, 10_000_001, 19_999_999, long.MaxValue };
        long[] frequencies = { 1, 3, 9_999_999, 10_000_000, 10_000_001, long.MaxValue / 2, long.MaxValue };

        foreach (var ticks in durationTicks)
        foreach (var frequency in frequencies)
        {
            var numerator = (BigInteger)ticks * frequency;
            var expected = (numerator + TimeSpan.TicksPerSecond - 1) / TimeSpan.TicksPerSecond;
            var saturated = expected > long.MaxValue ? long.MaxValue : (long)expected;

            Assert.Equal(saturated, MonotonicTime.DurationToTimestampTicks(TimeSpan.FromTicks(ticks), frequency));
        }
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(long.MinValue)]
    public void Invalid_frequency_is_rejected_even_for_zero_inputs(long frequency)
    {
        Assert.Equal("frequency",
            Assert.Throws<ArgumentOutOfRangeException>(() => MonotonicTime.ToMilliseconds(0, frequency)).ParamName);
        Assert.Equal("frequency",
            Assert.Throws<ArgumentOutOfRangeException>(
                () => MonotonicTime.DurationToTimestampTicks(TimeSpan.Zero, frequency)).ParamName);
    }

    [Fact]
    public void Negative_duration_reports_its_parameter_and_invalid_frequency_is_checked_first()
    {
        Assert.Equal("duration",
            Assert.Throws<ArgumentOutOfRangeException>(
                () => MonotonicTime.DurationToTimestampTicks(TimeSpan.MinValue, 1)).ParamName);
        Assert.Equal("frequency",
            Assert.Throws<ArgumentOutOfRangeException>(
                () => MonotonicTime.DurationToTimestampTicks(TimeSpan.MinValue, 0)).ParamName);
    }

    [Fact]
    public void Shared_clock_and_static_readers_use_the_stopwatch_time_source()
    {
        var clock = StopwatchMonotonicClock.Instance;

        Assert.Equal(Stopwatch.Frequency, clock.Frequency);
        Assert.Equal(clock.Frequency, MonotonicTime.TimestampFrequency);

        var before = clock.Timestamp;
        var timestamp = MonotonicTime.GetTimestamp();
        var after = clock.Timestamp;
        Assert.InRange(timestamp, before, after);

        var beforeMilliseconds = MonotonicTime.ToMilliseconds(clock.Timestamp, clock.Frequency);
        var milliseconds = MonotonicTime.GetMilliseconds();
        var afterMilliseconds = MonotonicTime.ToMilliseconds(clock.Timestamp, clock.Frequency);
        Assert.InRange(milliseconds, beforeMilliseconds, afterMilliseconds);
    }
}

