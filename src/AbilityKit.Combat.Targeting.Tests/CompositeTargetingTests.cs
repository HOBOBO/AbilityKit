using AbilityKit.Battle.SearchTarget;
using AbilityKit.Battle.SearchTarget.Providers;
using AbilityKit.Battle.SearchTarget.Rules;
using Xunit;

namespace AbilityKit.Combat.Targeting.Tests;

public sealed class CompositeTargetingTests
{
    [Fact]
    public void Boolean_rules_apply_identity_snapshot_and_null_contracts()
    {
        var source = new ITargetRule[] { new ActorIdRule(1), null! };
        var and = new AndRule(source);
        var or = new OrRule(source);
        source[0] = new ActorIdRule(2);

        using var context = new SearchContext();
        var query = default(SearchQuery);
        var candidate = new EntityId(1);

        Assert.True(and.Test(in query, context, candidate));
        Assert.True(or.Test(in query, context, candidate));
        Assert.True(new AndRule().Test(in query, context, candidate));
        Assert.False(new OrRule().Test(in query, context, candidate));
        Assert.Throws<ArgumentNullException>(() => new AndRule(null!));
        Assert.Throws<ArgumentNullException>(() => new OrRule(null!));
        Assert.Throws<ArgumentNullException>(() => new NotRule(null!));
    }

    [Fact]
    public void Boolean_rules_short_circuit_and_support_nesting()
    {
        var andTail = new CountingRule(true);
        var orTail = new CountingRule(false);
        var nested = new AndRule(
            new OrRule(new ActorIdRule(1), new ActorIdRule(2), orTail),
            new NotRule(new ActorIdRule(3)),
            new CountingRule(true));
        var shortAnd = new AndRule(new CountingRule(false), andTail);

        using var context = new SearchContext();
        var query = default(SearchQuery);

        Assert.True(nested.Test(in query, context, new EntityId(2)));
        Assert.Equal(0, orTail.CallCount);
        Assert.False(nested.Test(in query, context, new EntityId(3)));
        Assert.False(shortAnd.Test(in query, context, new EntityId(1)));
        Assert.Equal(0, andTail.CallCount);
    }

    [Fact]
    public void Concat_preserves_source_order_and_duplicates()
    {
        var provider = new ConcatCandidateProvider(
            new ArrayProvider(1, 1),
            null!,
            new ArrayProvider(2, 1));

        Assert.Equal(new[] { 1, 1, 2, 1 }, Enumerate(provider));
    }

    [Fact]
    public void Union_is_distinct_by_stable_key_and_preserves_first_occurrence()
    {
        var provider = new UnionDistinctCandidateProvider(
            new ArrayProvider(11, 22, 11),
            new ArrayProvider(21, 33));

        Assert.Equal(new[] { 11, 22, 33 }, Enumerate(provider, new DecimalKeyProvider()));
    }

    [Fact]
    public void Intersection_is_distinct_and_uses_first_source_order()
    {
        var provider = new IntersectCandidateProvider(
            new ArrayProvider(31, 22, 11, 22),
            new ArrayProvider(12, 21, 32),
            new ArrayProvider(41, 22, 51));

        Assert.Equal(new[] { 31, 22 }, Enumerate(provider, new DecimalKeyProvider()));
        Assert.Empty(Enumerate(new IntersectCandidateProvider(new ArrayProvider(1), null!)));
        Assert.Empty(Enumerate(new IntersectCandidateProvider()));
    }

    [Fact]
    public void Except_is_distinct_and_uses_primary_source_order()
    {
        var provider = new ExceptCandidateProvider(
            new ArrayProvider(31, 22, 11, 22, 44),
            new ArrayProvider(12),
            null!,
            new ArrayProvider(34));

        Assert.Equal(new[] { 31 }, Enumerate(provider, new DecimalKeyProvider()));
    }

    [Fact]
    public void Composite_providers_snapshot_inputs_and_can_be_nested()
    {
        var sources = new ICandidateProvider[] { new ArrayProvider(1), new ArrayProvider(2) };
        var union = new UnionDistinctCandidateProvider(sources);
        sources[0] = new ArrayProvider(9);
        var nested = new ExceptCandidateProvider(
            new ConcatCandidateProvider(union, new ArrayProvider(2, 3)),
            new ArrayProvider(2));

        Assert.Equal(new[] { 1, 3 }, Enumerate(nested));
    }

    [Fact]
    public void Provider_exception_does_not_leak_pooled_state_into_next_call()
    {
        var throwing = new UnionDistinctCandidateProvider(
            new ArrayProvider(1),
            new ThrowingProvider());

        Assert.Throws<InvalidOperationException>(() => Enumerate(throwing));
        Assert.Equal(
            new[] { 1, 2 },
            Enumerate(new UnionDistinctCandidateProvider(new ArrayProvider(1, 2))));
    }

    [Fact]
    public void Forwarding_composites_preserve_value_consumer_state_before_provider_exception()
    {
        using var context = new SearchContext();
        var query = default(SearchQuery);
        var unionConsumer = new CountingConsumer();
        var exceptConsumer = new CountingConsumer();
        var union = new UnionDistinctCandidateProvider(new EmitThenThrowProvider(1));
        var except = new ExceptCandidateProvider(new EmitThenThrowProvider(2));

        Assert.Throws<InvalidOperationException>(() =>
            union.ForEachCandidate(in query, context, ref unionConsumer));
        Assert.Throws<InvalidOperationException>(() =>
            except.ForEachCandidate(in query, context, ref exceptConsumer));

        Assert.Equal(1, unionConsumer.Count);
        Assert.Equal(1, exceptConsumer.Count);
    }

    private static int[] Enumerate(ICandidateProvider provider, IEntityKeyProvider? keyProvider = null)
    {
        using var context = new SearchContext
        {
            EntityKeyProvider = keyProvider
        };

        var ids = new List<int>();
        var consumer = new ListConsumer(ids);
        var query = default(SearchQuery);
        provider.ForEachCandidate(in query, context, ref consumer);
        return ids.ToArray();
    }

    private struct ListConsumer : ICandidateConsumer
    {
        private readonly List<int> _ids;

        public ListConsumer(List<int> ids)
        {
            _ids = ids;
        }

        public void Consume(EntityId id)
        {
            _ids.Add(checked((int)id.Value));
        }
    }

    private struct CountingConsumer : ICandidateConsumer
    {
        public int Count { get; private set; }

        public void Consume(EntityId id)
        {
            Count++;
        }
    }

    private sealed class ArrayProvider : ICandidateProvider
    {
        private readonly EntityId[] _ids;

        public ArrayProvider(params int[] actorIds)
        {
            _ids = actorIds.Select(static id => (EntityId)new EntityId(id)).ToArray();
        }

        public void ForEachCandidate<TConsumer>(
            in SearchQuery query,
            SearchContext context,
            ref TConsumer consumer)
            where TConsumer : struct, ICandidateConsumer
        {
            for (int i = 0; i < _ids.Length; i++) consumer.Consume(_ids[i]);
        }
    }

    private sealed class ThrowingProvider : ICandidateProvider
    {
        public void ForEachCandidate<TConsumer>(
            in SearchQuery query,
            SearchContext context,
            ref TConsumer consumer)
            where TConsumer : struct, ICandidateConsumer
        {
            throw new InvalidOperationException("Expected provider failure.");
        }
    }

    private sealed class EmitThenThrowProvider : ICandidateProvider
    {
        private readonly EntityId _id;

        public EmitThenThrowProvider(int actorId)
        {
            _id = new EntityId(actorId);
        }

        public void ForEachCandidate<TConsumer>(
            in SearchQuery query,
            SearchContext context,
            ref TConsumer consumer)
            where TConsumer : struct, ICandidateConsumer
        {
            consumer.Consume(_id);
            throw new InvalidOperationException("Expected provider failure after emission.");
        }
    }

    private sealed class DecimalKeyProvider : IEntityKeyProvider
    {
        public ulong GetKey(EntityId id)
        {
            return id.Value % 10UL;
        }
    }

    private sealed class ActorIdRule : ITargetRule
    {
        private readonly int _actorId;

        public ActorIdRule(int actorId)
        {
            _actorId = actorId;
        }

        public bool Test(in SearchQuery query, SearchContext context, EntityId candidate)
        {
            return candidate.Value == (ulong)_actorId;
        }
    }

    private sealed class CountingRule : ITargetRule
    {
        private readonly bool _result;

        public CountingRule(bool result)
        {
            _result = result;
        }

        public int CallCount { get; private set; }

        public bool Test(in SearchQuery query, SearchContext context, EntityId candidate)
        {
            CallCount++;
            return _result;
        }
    }
}
