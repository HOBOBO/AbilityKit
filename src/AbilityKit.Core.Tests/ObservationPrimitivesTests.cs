using AbilityKit.Core.Observability;
using System.Collections.Generic;
using Xunit;

namespace AbilityKit.Core.Tests;

public sealed class ObservationPrimitivesTests
{
    [Fact]
    public void RuntimeObjectKey_UsesRuntimeIdAndGenerationForIdentity()
    {
        var first = new RuntimeObjectKey(42L, 3);
        var same = new RuntimeObjectKey(42L, 3);
        var reused = new RuntimeObjectKey(42L, 4);

        Assert.True(first.IsValid);
        Assert.Equal(first, same);
        Assert.True(first == same);
        Assert.NotEqual(first, reused);
        Assert.True(first != reused);
        Assert.False(default(RuntimeObjectKey).IsValid);
    }

    [Fact]
    public void DefinitionAndTraceReferences_PreserveTypedCorrelation()
    {
        var definition = new ObservationDefinitionRef(2, 1001L);
        var trace = new ObservationTraceRef(500L, 501L, 499L);

        Assert.True(definition.IsValid);
        Assert.Equal(2, definition.Kind);
        Assert.Equal(1001L, definition.Id);
        Assert.True(trace.IsValid);
        Assert.Equal(500L, trace.RootId);
        Assert.Equal(501L, trace.ContextId);
        Assert.Equal(499L, trace.ParentId);
    }

    [Fact]
    public void NullSink_IsReusableAndRejectsWrites()
    {
        var value = new RuntimeObjectKey(1L);
        var sink = NullObservationSink<RuntimeObjectKey>.Instance;

        Assert.Same(sink, NullObservationSink<RuntimeObjectKey>.Instance);
        Assert.False(sink.IsEnabled);
        Assert.False(sink.TryWrite(in value));
    }

    [Fact]
    public void ValueReferences_RejectOnlyReservedInvalidCombinations()
    {
        Assert.False(new RuntimeObjectKey(1L, -1).IsValid);
        Assert.False(new RuntimeObjectKey(0L, 0).IsValid);
        Assert.True(new RuntimeObjectKey(-1L, 0).IsValid);

        Assert.False(new ObservationDefinitionRef(0, 1L).IsValid);
        Assert.False(new ObservationDefinitionRef(1, 0L).IsValid);
        Assert.True(new ObservationDefinitionRef(-1, -1L).IsValid);

        Assert.False(default(ObservationTraceRef).IsValid);
        Assert.True(new ObservationTraceRef(rootId: 1L, contextId: 0L).IsValid);
        Assert.True(new ObservationTraceRef(rootId: 0L, contextId: 1L).IsValid);
    }

    [Fact]
    public void ValueReferences_HaveStableDictionaryIdentityAndNullEquality()
    {
        var runtime = new RuntimeObjectKey(42L, 3);
        var definitions = new Dictionary<ObservationDefinitionRef, string>
        {
            [new ObservationDefinitionRef(2, 1001L)] = "definition",
        };
        var traces = new Dictionary<ObservationTraceRef, string>
        {
            [new ObservationTraceRef(500L, 501L, 499L)] = "trace",
        };

        Assert.False(runtime.Equals(null));
        Assert.Equal("definition", definitions[new ObservationDefinitionRef(2, 1001L)]);
        Assert.Equal("trace", traces[new ObservationTraceRef(500L, 501L, 499L)]);
    }

    [Fact]
    public void ObservationSink_IsSynchronousAndDoesNotImplyEventOwnership()
    {
        IObservationSink<RuntimeObjectKey> sink = new RecordingSink();
        var value = new RuntimeObjectKey(7L, 2);

        Assert.True(sink.IsEnabled);
        Assert.True(sink.TryWrite(in value));

        var recording = Assert.IsType<RecordingSink>(sink);
        Assert.Equal(value, recording.LastValue);
        Assert.Equal(1, recording.WriteCount);
    }

    private sealed class RecordingSink : IObservationSink<RuntimeObjectKey>
    {
        public bool IsEnabled => true;

        public RuntimeObjectKey LastValue { get; private set; }

        public int WriteCount { get; private set; }

        public bool TryWrite(in RuntimeObjectKey value)
        {
            LastValue = value;
            WriteCount++;
            return true;
        }
    }
}
