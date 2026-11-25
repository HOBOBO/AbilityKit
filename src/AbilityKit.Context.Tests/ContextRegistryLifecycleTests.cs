using AbilityKit.Context;
using Xunit;

namespace AbilityKit.Context.Tests;

public sealed class ContextRegistryLifecycleTests
{
    [Fact]
    public void Observers_see_entity_creation_property_update_and_removal_in_order()
    {
        var registry = new ContextRegistry();
        var observed = new List<(ContextEventType Type, bool Exists, bool HasProperty)>();
        registry.Subscribe(evt => observed.Add((evt.Type, registry.Exists(evt.EntityId), registry.Get<TestProperty>(evt.EntityId) != null)));

        var id = registry.Create().With(new TestProperty()).Build();
        Assert.True(registry.Destroy(id));

        Assert.Equal(new[]
        {
            (ContextEventType.Created, true, false),
            (ContextEventType.Updated, true, true),
            (ContextEventType.Destroying, true, true),
            (ContextEventType.Destroyed, false, false),
        }, observed);
        Assert.Empty(registry.GetEntitiesWith<TestProperty>());
    }

    [Fact]
    public void Reentrant_destroy_of_same_entity_does_not_emit_another_lifecycle()
    {
        var registry = new ContextRegistry();
        var id = registry.Create().With(new TestProperty()).Build();
        var observed = new List<ContextEventType>();
        bool? nestedResult = null;
        var attempted = false;
        registry.Subscribe(evt =>
        {
            observed.Add(evt.Type);
            if (evt.Type != ContextEventType.Destroying || attempted) return;
            attempted = true;
            nestedResult = registry.Destroy(evt.EntityId);
        });

        Assert.True(registry.Destroy(id));
        Assert.Equal(false, nestedResult);
        Assert.Equal(new[] { ContextEventType.Destroying, ContextEventType.Destroyed }, observed);
        Assert.False(registry.Exists(id));
    }

    [Fact]
    public void Destroy_does_not_delete_a_replacement_under_the_same_id_after_notification()
    {
        var registry = new ContextRegistry();
        var id = registry.Create().With(new TestProperty()).Build();
        Assert.True(registry.TryGetReference(id, out var oldReference));
        var replacement = new TestProperty();
        var errors = new List<Exception>();
        var observed = new List<ContextEventType>();
        registry.EventHandlerException = (_, error) => errors.Add(error);
        registry.Subscribe(evt =>
        {
            observed.Add(evt.Type);
            if (evt.Type != ContextEventType.Destroying) return;
            registry.RestoreLocalRollbackEntities(id, Array.Empty<long>(), Array.Empty<long>());
            registry.RestoreEntity(id).With(replacement);
        });

        Assert.False(registry.Destroy(id));
        Assert.Empty(errors);
        Assert.Equal(new[] { ContextEventType.Destroying, ContextEventType.Created, ContextEventType.Updated }, observed);
        Assert.Equal(ContextEntityQueryStatus.IdentityMismatch, registry.Query(oldReference));
        Assert.Same(replacement, registry.Get<TestProperty>(id));
        Assert.Equal(new[] { id }, registry.GetEntitiesWith<TestProperty>());
    }

    [Fact]
    public void Observer_failure_is_reported_and_does_not_change_create_or_destroy_result()
    {
        var registry = new ContextRegistry();
        var observed = new List<ContextEventType>();
        var reported = new List<(ContextEventType Type, Exception Error)>();
        registry.Subscribe(_ => throw new InvalidOperationException("observer failed"));
        registry.Subscribe(evt => observed.Add(evt.Type));
        registry.EventHandlerException = (evt, error) => reported.Add((evt.Type, error));

        var entityId = registry.Create().With(new TestProperty()).EntityId;
        var destroyed = registry.Destroy(entityId);

        Assert.True(destroyed);
        Assert.False(registry.Exists(entityId));
        Assert.Contains(ContextEventType.Created, observed);
        Assert.Contains(ContextEventType.Updated, observed);
        Assert.Contains(ContextEventType.Destroying, observed);
        Assert.Contains(ContextEventType.Destroyed, observed);
        Assert.Equal(4, reported.Count);
        Assert.All(reported, item => Assert.Equal("observer failed", item.Error.Message));
    }

    [Fact]
    public void Clear_completes_when_observers_and_error_observer_throw()
    {
        var registry = new ContextRegistry();
        var flowId = registry.CreateFlow("test");
        registry.CreateInFlow(flowId).With(new TestProperty());
        registry.CreateInFlow(flowId).With(new TestProperty());
        registry.Subscribe(evt =>
        {
            if (evt.Type is ContextEventType.Destroying or ContextEventType.Destroyed)
                throw new InvalidOperationException("lifecycle observer failed");
        });
        registry.EventHandlerException = (_, _) => throw new InvalidOperationException("diagnostics failed");

        registry.Clear();

        Assert.Equal(0, registry.Count);
        Assert.Equal(0, registry.FlowCount);
        Assert.Empty(registry.GetEntitiesWith<TestProperty>());
    }

    [Fact]
    public void Entity_specific_observer_failure_does_not_prevent_destroy()
    {
        var registry = new ContextRegistry();
        var entityId = registry.Create().EntityId;
        registry.Subscribe(entityId, _ => throw new InvalidOperationException("entity observer failed"));

        Assert.True(registry.Destroy(entityId));
        Assert.False(registry.Exists(entityId));
    }

    [Fact]
    public void Restore_preserves_identity_and_only_advances_allocation_cursor()
    {
        var registry = new ContextRegistry();
        Assert.Equal(100L, registry.RestoreEntity(100L).Build());
        Assert.Equal(101L, registry.Create().Build());
        Assert.Equal(5L, registry.RestoreEntity(5L).Build());
        Assert.Equal(102L, registry.Create().Build());
    }

    [Fact]
    public void Restore_rejects_existing_identity_without_overwriting_properties()
    {
        var registry = new ContextRegistry();
        var property = new TestProperty();
        var id = registry.Create().With(property).Build();
        var cursor = registry.NextEntityId;

        Assert.Throws<InvalidOperationException>(() => registry.RestoreEntity(id));
        Assert.True(registry.TryGetReference(id, out var reference));
        Assert.Same(property, new ContextValueResolver(registry).ReadRealtimeProperty<TestProperty>(reference).Value);
        Assert.Equal(cursor, registry.NextEntityId);
        Assert.Equal(1, registry.Count);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(long.MaxValue)]
    public void Restore_rejects_invalid_identity(long id)
    {
        var registry = new ContextRegistry();
        Assert.Throws<ArgumentOutOfRangeException>(() => registry.RestoreEntity(id));
        Assert.Equal(0, registry.Count);
    }

    [Fact]
    public void Restore_publishes_created_even_when_an_observer_throws()
    {
        var registry = new ContextRegistry();
        var observed = new List<ContextEventType>();
        registry.Subscribe(_ => throw new InvalidOperationException("observer failed"));
        registry.Subscribe(evt => observed.Add(evt.Type));

        Assert.Equal(20L, registry.RestoreEntity(20L).Build());
        Assert.Equal(new[] { ContextEventType.Created }, observed);
        Assert.True(registry.Exists(20L));
    }

    private sealed class TestProperty : IProperty
    {
        public int TypeId => PropertyTypeRegistry.Instance.Get<TestProperty>()?.Id ?? 0;
    }
}
