using System.Reflection;
using AbilityKit.Context;
using Xunit;

namespace AbilityKit.Context.Tests;

public sealed class ContextEntityReferenceTests
{
    [Fact]
    public void Destroy_restore_and_clear_reuse_ids_but_not_local_identity()
    {
        var registry = new ContextRegistry();
        var id = registry.Create().Build();
        Assert.True(registry.TryGetReference(id, out var original));
        Assert.True(registry.Destroy(id));
        Assert.Equal(ContextEntityQueryStatus.Unavailable, registry.Query(original));
        registry.RestoreEntity(id).Build();
        Assert.True(registry.TryGetReference(id, out var restored));
        Assert.True(restored.Generation > original.Generation);
        Assert.Equal(ContextEntityQueryStatus.IdentityMismatch, registry.Query(original));
        registry.Clear();
        Assert.Equal(id, registry.Create().Build());
        Assert.True(registry.TryGetReference(id, out var afterClear));
        Assert.True(afterClear.Generation > restored.Generation);
        Assert.Equal(ContextEntityQueryStatus.IdentityMismatch, registry.Query(restored));
    }

    [Fact]
    public void Registry_scope_missing_reference_and_default_result_are_explicit()
    {
        var first = new ContextRegistry();
        var second = new ContextRegistry();
        var id = first.Create().Build();
        second.Create().Build();
        Assert.True(first.TryGetReference(id, out var reference));
        Assert.Equal(ContextEntityQueryStatus.IdentityMismatch, second.Query(reference));
        Assert.Equal(ContextEntityQueryStatus.InvalidReference, first.Query(default));
        Assert.False(first.TryGetReference(999, out var absent));
        Assert.False(absent.IsValid);
        Assert.False(default(ContextRealtimeValue<int>).Found);
    }

    [Fact]
    public void Rollback_preserves_confirmed_generation_and_invalidates_reused_prediction_id()
    {
        var registry = new ContextRegistry();
        var confirmed = registry.Create().Build();
        registry.TryGetReference(confirmed, out var confirmedRef);
        var cursor = registry.NextEntityId;
        var predicted = registry.Create().Build();
        registry.TryGetReference(predicted, out var predictedRef);
        registry.RestoreRollbackEntityCursor(cursor, new[] { confirmed });
        Assert.Equal(predicted, registry.Create().Build());
        Assert.Equal(ContextEntityQueryStatus.Found, registry.Query(confirmedRef));
        Assert.Equal(ContextEntityQueryStatus.IdentityMismatch, registry.Query(predictedRef));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public void Invalid_rollback_is_rejected_before_any_destruction(long cursor, bool missingConfirmed)
    {
        var registry = new ContextRegistry();
        var first = registry.Create().Build();
        var second = registry.Create().Build();
        var events = new List<ContextEvent>();
        registry.Subscribe(events.Add);
        Assert.ThrowsAny<Exception>(() => registry.RestoreRollbackEntityCursor(cursor,
            new[] { missingConfirmed ? 999 : first }));
        Assert.True(registry.Exists(first));
        Assert.True(registry.Exists(second));
        Assert.Equal(3, registry.NextEntityId);
        Assert.Empty(events);
    }

    [Fact]
    public void Exact_realtime_read_does_not_fall_back_to_snapshot_or_default()
    {
        var registry = new ContextRegistry();
        var id = registry.Create().With(new TestProperty(0)).Build();
        registry.TryGetReference(id, out var reference);
        var resolver = new ContextValueResolver(registry);
        var zero = resolver.ReadRealtime<int, TestProperty>(reference, "value");
        Assert.True(zero.Found);
        Assert.Equal(0, zero.Value);
        Assert.Equal(ContextEntityQueryStatus.FieldMissing, resolver.ReadRealtime<int, TestProperty>(reference, "absent").Status);
        registry.Remove<TestProperty>(id);
        Assert.Equal(ContextEntityQueryStatus.PropertyMissing, resolver.ReadRealtime<int, TestProperty>(reference, "value").Status);
        registry.Destroy(id);
        registry.RestoreEntity(id).With(new TestProperty(9));
        Assert.Equal(ContextEntityQueryStatus.IdentityMismatch, resolver.ReadRealtime<int, TestProperty>(reference, "value").Status);
    }

    [Fact]
    public void Exact_read_does_not_register_missing_property_type()
    {
        var registry = new ContextRegistry();
        var id = registry.Create().Build();
        registry.TryGetReference(id, out var reference);
        var resolver = new ContextValueResolver(registry);
        Assert.Null(PropertyTypeRegistry.Instance.Get<NeverRegisteredProperty>());
        Assert.Equal(ContextEntityQueryStatus.PropertyMissing,
            resolver.ReadRealtime<int, NeverRegisteredProperty>(reference, "value").Status);
        Assert.Null(PropertyTypeRegistry.Instance.Get<NeverRegisteredProperty>());
    }

    [Fact]
    public void Provider_rebinding_during_read_is_rejected_after_capture()
    {
        var registry = new ContextRegistry();
        var id = registry.Create().Build();
        registry.TryGetReference(id, out var reference);
        var providers = new ContextRealtimeProviderRegistry();
        var provider = new RebindingProvider(() =>
        {
            registry.Destroy(id);
            registry.RestoreEntity(id).Build();
        });
        providers.Register<TestProperty>(provider);
        var resolver = new ContextValueResolver(registry, realtimeProviders: providers);
        var result = resolver.ReadRealtime<int, TestProperty>(reference, "value");
        Assert.Equal(ContextEntityQueryStatus.IdentityMismatch, result.Status);
        Assert.False(result.Found);
        Assert.Equal(1, provider.Reads);
    }

    [Theory]
    [InlineData("_nextEntityId")]
    [InlineData("_nextGeneration")]
    public void Exhausted_identity_space_does_not_wrap_or_create_entity(string counter)
    {
        var registry = new ContextRegistry();
        typeof(ContextRegistry).GetField(counter, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(registry, long.MaxValue);
        var cursor = registry.NextEntityId;
        Assert.Throws<InvalidOperationException>(() => registry.Create());
        Assert.Equal(cursor, registry.NextEntityId);
        Assert.Equal(0, registry.Count);
    }

    private sealed class TestProperty(int value) : IProperty, IContextValueProvider
    {
        public int TypeId => PropertyTypeRegistry.Instance.Get<TestProperty>()?.Id ?? 0;
        public bool TryGetValue<T>(string key, out T result)
        {
            if (key == "value" && value is T typed) { result = typed; return true; }
            result = default!;
            return false;
        }
    }

    private sealed class NeverRegisteredProperty : IProperty
    {
        public int TypeId => 0;
    }

    private sealed class RebindingProvider(Action rebind) : IContextRealtimeValueProvider
    {
        public int Reads;
        public bool TryGetProperty(long contextId, out IProperty property)
        {
            Reads++;
            rebind();
            property = new TestProperty(9);
            return true;
        }
        public bool TryGetValue<T>(long contextId, string key, out T value) => throw new InvalidOperationException("Field path must not run.");
    }
}
