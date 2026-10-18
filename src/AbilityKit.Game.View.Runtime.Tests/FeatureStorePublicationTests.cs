using System;
using AbilityKit.Game.Flow;
using Xunit;

namespace AbilityKit.Game.View.Runtime.Tests;

public sealed class FeatureStorePublicationTests
{
    [Fact]
    public void RemoveIfCurrent_DoesNotRemoveReplacementPublication()
    {
        var store = new TestFeatureStore();
        var first = new object();
        var replacement = new object();

        store.Set(first);
        store.Set(replacement);

        Assert.False(store.RemoveIfCurrent(first));
        Assert.True(store.TryGet<object>(out var current));
        Assert.Same(replacement, current);
        Assert.True(store.RemoveIfCurrent(replacement));
        Assert.False(store.TryGet<object>(out _));
    }

    private sealed class TestFeatureStore : IGameFeatureStore
    {
        private object _value;

        public bool TryGet<T>(out T component) where T : class
        {
            component = _value as T;
            return component != null;
        }

        public void Set<T>(T component) where T : class => _value = component;
        public void Remove<T>() where T : class => _value = null;
        public void Remove(Type componentType) => _value = null;
    }
}
