using AbilityKit.Core.Observability;
using Xunit;

namespace AbilityKit.Core.Tests;

public sealed class ObservationPrimitivesContractTests
{
    [Theory]
    [InlineData(0L, 0, false)]
    [InlineData(0L, int.MaxValue, false)]
    [InlineData(1L, -1, false)]
    [InlineData(long.MinValue, int.MinValue, false)]
    [InlineData(-1L, 0, true)]
    [InlineData(long.MinValue, int.MaxValue, true)]
    [InlineData(long.MaxValue, 0, true)]
    public void Runtime_key_preserves_unchecked_input_and_validity_is_separate(long id, int generation, bool valid)
    {
        var key = new RuntimeObjectKey(id, generation);
        Assert.Equal(id, key.RuntimeId);
        Assert.Equal(generation, key.Generation);
        Assert.Equal(valid, key.IsValid);
    }

    [Theory]
    [InlineData(0, 0L, false)]
    [InlineData(0, long.MinValue, false)]
    [InlineData(int.MinValue, 0L, false)]
    [InlineData(-1, -1L, true)]
    [InlineData(int.MinValue, long.MaxValue, true)]
    [InlineData(int.MaxValue, long.MinValue, true)]
    public void Definition_preserves_unchecked_input_and_requires_both_fields(int kind, long id, bool valid)
    {
        var definition = new ObservationDefinitionRef(kind, id);
        Assert.Equal(kind, definition.Kind);
        Assert.Equal(id, definition.Id);
        Assert.Equal(valid, definition.IsValid);
    }

    [Theory]
    [InlineData(0L, 0L, 0L, false)]
    [InlineData(0L, 0L, long.MinValue, false)]
    [InlineData(0L, 0L, long.MaxValue, false)]
    [InlineData(-1L, 0L, 0L, true)]
    [InlineData(0L, long.MinValue, -1L, true)]
    [InlineData(long.MaxValue, long.MinValue, long.MaxValue, true)]
    [InlineData(7L, 7L, 7L, true)]
    public void Trace_validity_ignores_parent_and_does_not_validate_relationships(long root, long context, long parent, bool valid)
    {
        var trace = new ObservationTraceRef(root, context, parent);
        Assert.Equal(root, trace.RootId);
        Assert.Equal(context, trace.ContextId);
        Assert.Equal(parent, trace.ParentId);
        Assert.Equal(valid, trace.IsValid);
    }

    [Fact]
    public void Defaults_are_zero_invalid_and_optional_arguments_preserve_zero_generation_and_parent()
    {
        Assert.Equal(new RuntimeObjectKey(0, 0), default);
        Assert.Equal(new ObservationDefinitionRef(0, 0), default);
        Assert.Equal(new ObservationTraceRef(0, 0, 0), default);
        Assert.False(default(RuntimeObjectKey).IsValid);
        Assert.False(default(ObservationDefinitionRef).IsValid);
        Assert.False(default(ObservationTraceRef).IsValid);
        Assert.Equal(0, new RuntimeObjectKey(7).Generation);
        Assert.Equal(0, new ObservationTraceRef(7, 8).ParentId);
    }

    [Fact]
    public void Runtime_key_identity_includes_every_field_even_when_invalid()
    {
        var values = new[] { default(RuntimeObjectKey), new(0, 1), new(1, -1), new(1, 0), new(1, 1), new(long.MinValue, int.MaxValue) };
        AssertIdentity(values, (a, b) => a == b, (a, b) => a != b);
        Assert.False(new RuntimeObjectKey(1).Equals((object)new ObservationDefinitionRef(1, 0)));
    }

    [Fact]
    public void Definition_identity_includes_kind_and_id_even_when_invalid()
    {
        var values = new[] { default(ObservationDefinitionRef), new(0, 1), new(1, 0), new(1, 1), new(2, 1), new(1, 2), new(int.MinValue, long.MaxValue) };
        AssertIdentity(values, (a, b) => a == b, (a, b) => a != b);
        Assert.False(new ObservationDefinitionRef(1, 1).Equals((object)new RuntimeObjectKey(1, 1)));
    }

    [Fact]
    public void Trace_identity_includes_parent_even_when_parent_alone_is_invalid()
    {
        var values = new[] { default(ObservationTraceRef), new(0, 0, 1), new(0, 0, 2), new(1, 2, 3), new(2, 2, 3), new(1, 3, 3), new(1, 2, 4), new(long.MinValue, long.MaxValue, -1) };
        AssertIdentity(values, (a, b) => a == b, (a, b) => a != b);
        Assert.False(new ObservationTraceRef(1, 2).Equals((object)new ObservationDefinitionRef(1, 2)));
    }

    [Fact]
    public void Null_sink_has_one_instance_per_closed_event_type_and_accepts_no_event()
    {
        var sink = NullObservationSink<ReferenceEvent>.Instance;
        IObservationSink<ReferenceEvent> contract = sink;
        var payload = new Probe();
        var value = new ReferenceEvent(payload);

        Assert.Same(sink, NullObservationSink<ReferenceEvent>.Instance);
        Assert.NotSame((object)sink, NullObservationSink<RuntimeObjectKey>.Instance);
        Assert.False(contract.IsEnabled);
        Assert.False(contract.TryWrite(in value));
        Assert.Same(payload, value.Payload);
        Assert.Equal(0, payload.Touches);
        var empty = default(ReferenceEvent);
        Assert.False(contract.TryWrite(in empty));
        Assert.False(NullObservationSink<RuntimeObjectKey>.Instance.TryWrite(default));
    }

    [Fact]
    public void Null_sink_can_be_shared_by_concurrent_writers_without_mutating_payloads()
    {
        var sink = NullObservationSink<ReferenceEvent>.Instance;
        var results = new bool[128];
        var probes = Enumerable.Range(0, results.Length).Select(_ => new Probe()).ToArray();
        Parallel.For(0, results.Length, i =>
        {
            var value = new ReferenceEvent(probes[i]);
            results[i] = sink.IsEnabled || sink.TryWrite(in value);
        });
        Assert.All(results, accepted => Assert.False(accepted));
        Assert.All(probes, probe => Assert.Equal(0, probe.Touches));
        Assert.Same(sink, NullObservationSink<ReferenceEvent>.Instance);
    }

    private static void AssertIdentity<T>(T[] distinctValues, Func<T, T, bool> equal, Func<T, T, bool> different)
        where T : struct, IEquatable<T>
    {
        /*
         * 在实际集合中验证所有身份字段的区分，不把具体哈希数字固化为持久化协议。
         * 包含无效值，以防相等比较被未来的有效性检查错误折叠。
         */
        var dictionary = distinctValues.Select((value, index) => (value, index)).ToDictionary(pair => pair.value, pair => pair.index);
        for (var i = 0; i < distinctValues.Length; i++)
        {
            var value = distinctValues[i];
            object boxed = value;
            Assert.False(value.Equals(null));
            Assert.False(value.Equals("无关类型"));
            Assert.True(value.Equals(boxed));
            Assert.Equal(value.GetHashCode(), boxed.GetHashCode());
            Assert.Equal(i, dictionary[(T)boxed]);
            for (var j = 0; j < distinctValues.Length; j++)
            {
                Assert.Equal(i == j, value.Equals(distinctValues[j]));
                Assert.Equal(i == j, equal(value, distinctValues[j]));
                Assert.Equal(i != j, different(value, distinctValues[j]));
            }
        }
    }

    private readonly struct ReferenceEvent(Probe? payload)
    {
        public Probe? Payload { get; } = payload;
        public override bool Equals(object? obj) => throw new InvalidOperationException("空 sink 不应比较事件");
        public override int GetHashCode() => throw new InvalidOperationException("空 sink 不应计算事件哈希");
        public override string ToString() => throw new InvalidOperationException("空 sink 不应格式化事件");
    }

    private sealed class Probe
    {
        public int Touches { get; private set; }
        public override string ToString()
        {
            Touches++;
            return "载荷";
        }
    }
}
