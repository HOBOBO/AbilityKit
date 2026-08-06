using AbilityKit.Battle.SearchTarget;
using AbilityKit.Battle.SearchTarget.Rules;
using AbilityKit.Battle.SearchTarget.Scorers;
using AbilityKit.Battle.SearchTarget.Selectors;
using Xunit;

namespace AbilityKit.Combat.Targeting.Tests;

public sealed class SearchHitTests
{
    [Fact]
    public void SearchQuery_uses_defaults_and_normalizes_null_collections()
    {
        var query = new SearchQuery(new ArrayProvider(), null!, null!, null!, 10);

        Assert.Equal(10, query.MaxCount);
        Assert.Empty(query.Rules);
        Assert.Empty(query.Orders);
    }

    [Fact]
    public void SearchQuery_owns_a_snapshot_of_rules()
    {
        var sourceRules = new List<ITargetRule> { OddActorIdRule.Instance };
        var query = new SearchQuery(new ArrayProvider(1, 2), sourceRules, null!, null!, 0);
        sourceRules.Clear();

        var results = Execute(query);

        Assert.Equal(new[] { 1 }, ActorIds(results));
    }

    [Theory]
    [InlineData(SearchSortDirection.ScoreDescending, 3, 2, 1)]
    [InlineData(SearchSortDirection.ScoreAscending, 1, 2, 3)]
    public void Engine_sorts_by_score_and_uses_actor_id_as_stable_key(
        SearchSortDirection direction,
        params int[] expected)
    {
        var query = new SearchQuery(
            new ArrayProvider(3, 1, 2),
            null!,
            new[] { new SearchOrder(new ActorIdScorer(), direction) },
            null!,
            0);

        var results = Execute(query);

        Assert.Equal(expected, ActorIds(results));
    }

    [Theory]
    [InlineData(false, SearchSortDirection.ScoreDescending, 3, 2)]
    [InlineData(false, SearchSortDirection.ScoreAscending, 1, 2)]
    [InlineData(true, SearchSortDirection.ScoreDescending, 3, 2)]
    [InlineData(true, SearchSortDirection.ScoreAscending, 1, 2)]
    public void TopK_selectors_respect_query_sort_direction(
        bool useBufferedSelector,
        SearchSortDirection direction,
        params int[] expected)
    {
        ITargetSelector selector = useBufferedSelector
            ? new StreamingTopKByScoreSelector()
            : new TopKByScoreSelector();
        var query = new SearchQuery(
            new ArrayProvider(2, 3, 1),
            null!,
            new[] { new SearchOrder(new ActorIdScorer(), direction) },
            selector,
            2);

        var results = Execute(query);

        Assert.Equal(expected, ActorIds(results));
    }

    [Fact]
    public void Buffered_top_k_selector_can_be_reused_without_cross_query_state()
    {
        var selector = new StreamingTopKByScoreSelector();
        var orders = new[] { new SearchOrder(new ActorIdScorer()) };
        var first = new SearchQuery(new ArrayProvider(1, 3), null!, orders, selector, 1);
        var second = new SearchQuery(new ArrayProvider(2, 4), null!, orders, selector, 1);

        Assert.Equal(new[] { 3 }, ActorIds(Execute(first)));
        Assert.Equal(new[] { 4 }, ActorIds(Execute(second)));
    }

    [Fact]
    public void Streaming_top_k_preserves_stats_while_selecting_during_candidate_traversal()
    {
        var stats = new RecordingSearchStats();
        var query = new SearchQuery(
            new ArrayProvider(1, 2, 3, 4, 5, 6),
            new ITargetRule[] { OddActorIdRule.Instance },
            new[] { new SearchOrder(new ActorIdScorer()) },
            new StreamingTopKByScoreSelector(),
            2);
        using var context = new SearchContext
        {
            SearchStats = stats
        };
        var results = new List<EntityId>();

        new TargetSearchEngine().SearchIds(in query, context, results);

        Assert.Equal(new[] { 5, 3 }, ActorIds(results));
        Assert.Equal(6, stats.CandidateCount);
        Assert.Equal(3, stats.HitCount);
        Assert.Equal(2, stats.ResultCount);
    }

    [Fact]
    public void Custom_selector_receives_the_complete_hit_view()
    {
        var selector = new RecordingSelector();
        var query = new SearchQuery(
            new ArrayProvider(4, 1, 3, 2),
            null!,
            new[] { new SearchOrder(new ActorIdScorer()) },
            selector,
            2);

        var results = Execute(query);

        Assert.Equal(4, selector.ReceivedHitCount);
        Assert.Equal(new[] { 4, 1 }, ActorIds(results));
    }

    [Fact]
    public void Custom_selector_cannot_write_past_query_max_count()
    {
        var query = new SearchQuery(
            new ArrayProvider(1, 2, 3, 4),
            null!,
            null!,
            new RecordingSelector(),
            2);

        Assert.Equal(new[] { 1, 2 }, ActorIds(Execute(query)));
    }

    [Fact]
    public void Custom_streaming_capability_uses_fused_top_k_path()
    {
        var query = new SearchQuery(
            new ArrayProvider(1, 3, 2),
            null!,
            new[] { new SearchOrder(new ActorIdScorer()) },
            new ThrowingStreamingSelector(),
            2);

        Assert.Equal(new[] { 3, 2 }, ActorIds(Execute(query)));
    }

    [Fact]
    public void Position_rule_rejects_candidates_locally_when_service_is_missing()
    {
        var query = new SearchQuery(
            new ArrayProvider(1, 2),
            new ITargetRule[] { new CircleShapeRule(Vec2.Zero, 10f) },
            null!,
            null!,
            0);

        var results = Execute(query);

        Assert.Empty(results);
    }

    [Fact]
    public void SearchIds_rejects_null_context_and_results()
    {
        var engine = new TargetSearchEngine();
        var query = new SearchQuery(new ArrayProvider(1), null!, null!, null!, 0);
        var results = new List<EntityId>();

        Assert.Throws<ArgumentNullException>(() => engine.SearchIds(in query, null!, results));
        using var context = new SearchContext();
        Assert.Throws<ArgumentNullException>(() => engine.SearchIds(in query, context, null!));
    }

    [Fact]
    public void Registry_registers_attributed_types_and_creates_instances()
    {
        const int ruleId = 0x7F01;
        TargetRuleRegistry.Instance.Register(typeof(RegistryRule));

        Assert.True(TargetRuleRegistry.Instance.TryGet(ruleId, out var registeredType));
        Assert.Equal(typeof(RegistryRule), registeredType);
        Assert.IsType<RegistryRule>(TargetRuleRegistry.Instance.Create(ruleId));
    }

    [Fact]
    public void Typed_context_keys_are_isolated_by_identity_and_cleared_with_data()
    {
        var firstKey = new SearchContextKey<int>("Seed");
        var secondKey = new SearchContextKey<int>("Seed");
        using var context = new SearchContext();

        context.SetData(firstKey, 7);

        Assert.True(context.TryGetData(firstKey, out var value));
        Assert.Equal(7, value);
        Assert.False(context.TryGetData(secondKey, out _));

        context.ClearData();
        Assert.False(context.TryGetData(firstKey, out _));
    }

    [Fact]
    public void Clear_releases_framework_capabilities_and_typed_extension_data()
    {
        var key = new SearchContextKey<int>("Clear.Value");
        using var context = new SearchContext
        {
            PositionProvider = TestPositionProvider.Instance,
            EntityKeyProvider = TestEntityKeyProvider.Instance,
            SearchStats = new RecordingSearchStats()
        };
        context.SetData(key, 7);

        context.Clear();

        Assert.Null(context.PositionProvider);
        Assert.Null(context.EntityKeyProvider);
        Assert.Null(context.SearchStats);
        Assert.False(context.TryGetData(key, out _));
    }

    [Fact]
    public void Captured_seeded_random_scorer_ignores_context_seed_changes()
    {
        var key = new SearchContextKey<int>("Random.Seed");
        var scorer = new SeededHashRandomScorer(19);
        using var context = new SearchContext();
        context.SetData(key, 23);

        var first = scorer.Score(default, context, new EntityId(7));
        context.SetData(key, 29);
        var second = scorer.Score(default, context, new EntityId(7));

        Assert.Equal(first, second);
    }

    [Fact]
    public void Seeded_random_scorer_reads_seed_from_typed_context_key()
    {
        var key = new SearchContextKey<int>("Random.Seed");
        var scorer = new SeededHashRandomScorer(key);
        using var context = new SearchContext();
        context.SetData(key, 19);

        var first = scorer.Score(default, context, new EntityId(7));
        context.SetData(key, 23);
        var second = scorer.Score(default, context, new EntityId(7));

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Registry_factory_creates_parameterized_components()
    {
        const int scorerId = 0x7F02;
        var countBefore = TargetScorerRegistry.Instance.Count;
        TargetScorerRegistry.Instance.RegisterFactory(scorerId, () => new ConstantScorer(12f));

        var scorer = Assert.IsType<ConstantScorer>(TargetScorerRegistry.Instance.Create(scorerId));
        using var context = new SearchContext();

        Assert.Equal(countBefore + 1, TargetScorerRegistry.Instance.Count);
        Assert.Equal(12f, scorer.Score(default, context, new EntityId(1)));
    }

    [Fact]
    public void Registry_uses_one_id_namespace_and_keeps_first_registration()
    {
        const int scorerId = 0x7F03;
        TargetScorerRegistry.Instance.Register(typeof(RegistryScorer));
        TargetScorerRegistry.Instance.RegisterFactory(scorerId, () => new ConstantScorer(12f));

        Assert.IsType<RegistryScorer>(TargetScorerRegistry.Instance.Create(scorerId));
    }

    [Fact]
    public void Registry_returns_null_for_parameterized_type_without_factory()
    {
        const int scorerId = 0x7F04;
        TargetScorerRegistry.Instance.Register(typeof(ParameterizedRegistryScorer));

        Assert.Null(TargetScorerRegistry.Instance.Create(scorerId));
    }

    [Fact]
    public void Registry_rejects_null_factory()
    {
        Assert.Throws<ArgumentNullException>(() =>
            TargetScorerRegistry.Instance.RegisterFactory(0x7F05, null!));
    }

    [Fact]
    public void Registries_reject_the_same_component_type_under_multiple_ids()
    {
        const int duplicateId = 0x7F06;
        TargetScorerRegistry.Instance.Register(typeof(RegistryScorer));

        TargetScorerRegistry.Instance.RegisterByAttribute(
            new TargetScorerAttribute(duplicateId),
            typeof(RegistryScorer));

        Assert.False(TargetScorerRegistry.Instance.TryGet(duplicateId, out _));
    }

    [Fact]
    public void Registry_supports_concurrent_registration_and_reads()
    {
        const int firstId = 0x7F10;
        const int count = 32;

        Parallel.For(0, count, offset =>
        {
            var id = firstId + offset;
            TargetScorerRegistry.Instance.RegisterFactory(id, () => new ConstantScorer(id));
            Assert.NotNull(TargetScorerRegistry.Instance.Create(id));
        });

        for (var offset = 0; offset < count; offset++)
        {
            Assert.NotNull(TargetScorerRegistry.Instance.Create(firstId + offset));
        }
    }

    [Fact]
    public void Pooled_context_clears_capabilities_and_typed_data_before_next_rent()
    {
        var key = new SearchContextKey<int>("Pool.Value");
        var first = TargetingPool.RentContext();
        first.PositionProvider = TestPositionProvider.Instance;
        first.EntityKeyProvider = TestEntityKeyProvider.Instance;
        first.SearchStats = new RecordingSearchStats();
        first.SetData(key, 7);
        TargetingPool.Release(first);

        var second = TargetingPool.RentContext();
        try
        {
            Assert.Null(second.PositionProvider);
            Assert.Null(second.EntityKeyProvider);
            Assert.Null(second.SearchStats);
            Assert.False(second.TryGetData(key, out _));
        }
        finally
        {
            TargetingPool.Release(second);
        }
    }

    [Fact]
    public void Released_pooled_context_rejects_access_while_standalone_context_remains_reusable()
    {
        var key = new SearchContextKey<int>("Lease.Value");
        var pooled = TargetingPool.RentContext();
        TargetingPool.Release(pooled);

        Assert.Throws<ObjectDisposedException>(() => pooled.SetData(key, 1));
        Assert.Throws<ObjectDisposedException>(() => pooled.TryGetData(key, out _));
        Assert.Throws<ObjectDisposedException>(() => pooled.PositionProvider = TestPositionProvider.Instance);
        Assert.Throws<ObjectDisposedException>(() => pooled.Clear());

        using var standalone = new SearchContext();
        standalone.Dispose();
        standalone.SetData(key, 2);
        Assert.True(standalone.TryGetData(key, out var value));
        Assert.Equal(2, value);
    }

    [Fact]
    public void Pooled_context_release_is_idempotent_and_does_not_duplicate_pool_entries()
    {
        var first = TargetingPool.RentContext();
        TargetingPool.Release(first);
        first.Dispose();

        var second = TargetingPool.RentContext();
        var third = TargetingPool.RentContext();
        try
        {
            Assert.NotSame(second, third);
        }
        finally
        {
            TargetingPool.Release(second);
            TargetingPool.Release(third);
        }
    }

    [Fact]
    public void Disposing_non_pooled_context_does_not_add_it_to_the_pool()
    {
        var standalone = new SearchContext();
        standalone.Dispose();

        var pooled = TargetingPool.RentContext();
        try
        {
            Assert.NotSame(standalone, pooled);
        }
        finally
        {
            TargetingPool.Release(pooled);
        }
    }

    [Fact]
    public void Pooled_result_release_is_idempotent_and_does_not_duplicate_pool_entries()
    {
        var first = TargetingPool.RentResult();
        first.Dispose();
        TargetingPool.Release(first);

        var second = TargetingPool.RentResult();
        var third = TargetingPool.RentResult();
        try
        {
            Assert.NotSame(second, third);
        }
        finally
        {
            TargetingPool.Release(second);
            TargetingPool.Release(third);
        }
    }

    [Fact]
    public void Released_pooled_result_rejects_public_access()
    {
        var result = TargetingPool.RentResult();
        result.MutableIds.Add(new EntityId(1));
        var idsView = result.Ids;
        TargetingPool.Release(result);

        Assert.Throws<ObjectDisposedException>(() => _ = result.Ids);
        Assert.Throws<ObjectDisposedException>(() => _ = result.Count);
        Assert.Throws<ObjectDisposedException>(() => _ = result[0]);
        Assert.Throws<ObjectDisposedException>(() => result.CopyTo(new List<EntityId>()));
        Assert.Throws<ObjectDisposedException>(() => result.Clear());
        Assert.Empty(idsView);
    }

    [Fact]
    public void SearchResult_CopyTo_rejects_null_destination()
    {
        var result = TargetingPool.RentResult();
        try
        {
            Assert.Throws<ArgumentNullException>(() => result.CopyTo(null!));
        }
        finally
        {
            TargetingPool.Release(result);
        }
    }

    [Fact]
    public void Empty_provider_resets_stats_and_reports_zero_results()
    {
        var stats = new RecordingSearchStats();
        stats.OnCandidate();
        stats.OnHit();
        stats.OnResult(3);
        var query = new SearchQuery(null!, null!, null!, null!, 0);
        using var context = new SearchContext { SearchStats = stats };
        var results = new List<EntityId> { new EntityId(7) };

        new TargetSearchEngine().SearchIds(in query, context, results);

        Assert.Empty(results);
        Assert.Equal(0, stats.CandidateCount);
        Assert.Equal(0, stats.HitCount);
        Assert.Equal(0, stats.ResultCount);
    }

    [Fact]
    public void SearchQuery_rejects_negative_max_count()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SearchQuery(new ArrayProvider(1), null!, null!, null!, -1));
    }

    [Fact]
    public void SearchOrder_rejects_null_scorer_and_invalid_direction()
    {
        Assert.Throws<ArgumentNullException>(() => new SearchOrder(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SearchOrder(new ActorIdScorer(), (SearchSortDirection)99));
    }

    [Fact]
    public void SearchQuery_rejects_invalid_duplicate_policy_and_default_order()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SearchQuery(
                new ArrayProvider(1),
                null!,
                null!,
                null!,
                0,
                (SearchDuplicatePolicy)99));

        Assert.Throws<ArgumentException>(() =>
            new SearchQuery(
                new ArrayProvider(1),
                null!,
                new[] { default(SearchOrder) },
                null!,
                0));
    }

    [Fact]
    public void TargetQueryDatabase_rejects_null_arguments_and_clears_results_on_miss()
    {
        var database = new TargetQueryDatabase();
        using var context = new SearchContext();
        var results = new List<EntityId> { new(7) };

        Assert.Throws<ArgumentNullException>(() =>
            database.TrySearchIds(1, null!, results));
        Assert.Throws<ArgumentNullException>(() =>
            database.TrySearchIds(1, context, (List<EntityId>)null!));
        Assert.Throws<ArgumentNullException>(() =>
            database.TrySearchIds(1, null!, out _));

        Assert.False(database.TrySearchIds(1, context, results));
        Assert.Empty(results);
    }

    [Fact]
    public void TargetQueryDatabase_supports_concurrent_registration_and_lookup()
    {
        var database = new TargetQueryDatabase();
        var query = new SearchQuery(new ArrayProvider(1), null!, null!, null!, 0);

        Parallel.For(0, 64, queryId =>
        {
            database.Register(queryId, in query);
            Assert.True(database.TryGetFactory(queryId, out _));
        });

        Assert.Equal(64, database.Count);
    }

    [Fact]
    public void Pooled_lists_retain_normal_capacity_and_trim_oversized_capacity()
    {
        var list = TargetingPool.RentEntityIdList();
        list.Capacity = 128;
        TargetingPool.ReleaseEntityIdList(list);

        var retained = TargetingPool.RentEntityIdList();
        Assert.True(retained.Capacity >= 128);
        retained.Capacity = 5000;
        TargetingPool.ReleaseEntityIdList(retained);

        var trimmed = TargetingPool.RentEntityIdList();
        try
        {
            Assert.Equal(64, trimmed.Capacity);
        }
        finally
        {
            TargetingPool.ReleaseEntityIdList(trimmed);
        }
    }

    [Fact]
    public void Pooled_entity_key_sets_retain_normal_capacity_and_trim_oversized_capacity()
    {
        var normal = TargetingPool.RentEntityKeySet();
        normal.EnsureCapacity(128);
        normal.Add(1);
        TargetingPool.ReleaseEntityKeySet(normal);

        var retained = TargetingPool.RentEntityKeySet();
        Assert.True(retained.EnsureCapacity(0) >= 128);
        for (ulong key = 0; key < 5000; key++)
        {
            retained.Add(key);
        }
        TargetingPool.ReleaseEntityKeySet(retained);

        var trimmed = TargetingPool.RentEntityKeySet();
        try
        {
            Assert.Empty(trimmed);
            Assert.True(trimmed.EnsureCapacity(0) < 5000);
        }
        finally
        {
            TargetingPool.ReleaseEntityKeySet(trimmed);
        }
    }

    [Fact]
    public void Hit_buffer_releases_only_oversized_storage()
    {
        var buffer = new SearchHitBuffer();
        buffer.EnsureCapacity(128);

        buffer.Reset(256);
        Assert.Equal(128, buffer.Capacity);

        buffer.Reset(64);
        Assert.Equal(0, buffer.Capacity);
    }

    [Fact]
    public void Score_buffer_releases_only_oversized_storage()
    {
        var orders = Enumerable
            .Repeat(new SearchOrder(new ConstantScorer(1f)), 17)
            .ToArray();
        var query = new SearchQuery(
            new ArrayProvider(1),
            null!,
            orders,
            null!,
            0);
        using var context = new SearchContext();
        var buffer = new SearchScoreBuffer();
        buffer.Add(orders, in query, context, new EntityId(1));

        buffer.Reset(64);
        Assert.True(buffer.Capacity >= 17);

        buffer.Reset(16);
        Assert.Equal(0, buffer.Capacity);
    }

    [Fact]
    public void Builder_rejects_negative_take_and_propagates_duplicate_policy()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            var invalidBuilder = SearchPipelineBuilder.Create();
            invalidBuilder.Take(-1);
        });

        var builder = SearchPipelineBuilder.Create();
        try
        {
            var query = builder
                .From(new ArrayProvider(1, 1))
                .DistinctCandidatesByEntityKey()
                .Build();

            Assert.Equal(SearchDuplicatePolicy.DistinctByEntityKey, query.DuplicatePolicy);
            Assert.Equal(new[] { 1 }, ActorIds(Execute(query)));
        }
        finally
        {
            builder.Dispose();
        }
    }

    [Fact]
    public void Builder_id_lookup_failure_preserves_existing_order_and_selector()
    {
        var builder = SearchPipelineBuilder.Create()
            .From(new ArrayProvider(1, 2))
            .ScoreBy(new ActorIdScorer())
            .Select(new TopKByScoreSelector())
            .Take(1);
        try
        {
            var query = builder
                .ScoreById(int.MinValue)
                .SelectById(int.MinValue)
                .Build();

            Assert.Single(query.Orders);
            Assert.IsType<ActorIdScorer>(query.Orders[0].Scorer);
            Assert.IsType<TopKByScoreSelector>(query.Selector);
            Assert.Equal(new[] { 2 }, ActorIds(Execute(query)));
        }
        finally
        {
            builder.Dispose();
        }
    }

    [Theory]
    [InlineData(SearchDuplicatePolicy.Preserve, 1, 1, 2)]
    [InlineData(SearchDuplicatePolicy.DistinctByEntityKey, 1, 2)]
    public void Duplicate_candidates_follow_explicit_query_policy(
        SearchDuplicatePolicy duplicatePolicy,
        params int[] expected)
    {
        var query = new SearchQuery(
            new ArrayProvider(1, 1, 2),
            null!,
            null!,
            null!,
            0,
            duplicatePolicy: duplicatePolicy);

        Assert.Equal(expected, ActorIds(Execute(query)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Distinct_policy_commits_entity_key_only_after_rules_pass(bool streaming)
    {
        ITargetSelector? selector = streaming ? new StreamingTopKByScoreSelector() : null;
        var query = new SearchQuery(
            new ArrayProvider(1, 2),
            new ITargetRule[] { new MinimumActorIdRule(2) },
            new[] { new SearchOrder(new ActorIdScorer()) },
            selector!,
            streaming ? 1 : 0,
            duplicatePolicy: SearchDuplicatePolicy.DistinctByEntityKey);
        using var context = new SearchContext
        {
            EntityKeyProvider = ConstantEntityKeyProvider.Instance
        };
        var results = new List<EntityId>();

        new TargetSearchEngine().SearchIds(in query, context, results);

        Assert.Equal(new[] { 2 }, ActorIds(results));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Distinct_policy_commits_entity_key_only_after_scores_are_valid(bool streaming)
    {
        ITargetSelector? selector = streaming ? new StreamingTopKByScoreSelector() : null;
        var query = new SearchQuery(
            new ArrayProvider(1, 2),
            null!,
            new[] { new SearchOrder(new FirstActorNaNScorer()) },
            selector!,
            streaming ? 1 : 0,
            duplicatePolicy: SearchDuplicatePolicy.DistinctByEntityKey);
        using var context = new SearchContext
        {
            EntityKeyProvider = ConstantEntityKeyProvider.Instance
        };
        var results = new List<EntityId>();

        new TargetSearchEngine().SearchIds(in query, context, results);

        Assert.Equal(new[] { 2 }, ActorIds(results));
    }

    [Theory]
    [InlineData(SearchSortDirection.ScoreDescending, 2, 1)]
    [InlineData(SearchSortDirection.ScoreAscending, 1, 2)]
    public void NaN_scores_are_rejected_while_infinities_remain_orderable(
        SearchSortDirection direction,
        params int[] expected)
    {
        var query = new SearchQuery(
            new ArrayProvider(1, 2, 3),
            null!,
            new[] { new SearchOrder(new SpecialValueScorer(), direction) },
            null!,
            0);

        Assert.Equal(expected, ActorIds(Execute(query)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Multiple_orders_use_strict_lexicographic_comparison_for_all_selection_paths(int selectorKind)
    {
        ITargetSelector? selector = selectorKind switch
        {
            1 => new TopKByScoreSelector(),
            2 => new StreamingTopKByScoreSelector(),
            _ => null
        };
        var orders = new[]
        {
            new SearchOrder(new ActorIdGroupScorer(), SearchSortDirection.ScoreAscending),
            new SearchOrder(new ActorIdScorer(), SearchSortDirection.ScoreDescending)
        };
        var query = new SearchQuery(
            new ArrayProvider(21, 12, 22, 11),
            null!,
            orders,
            selector!,
            3);

        Assert.Equal(new[] { 12, 11, 22 }, ActorIds(Execute(query)));
    }

    [Fact]
    public void Ordered_query_owns_a_snapshot_and_rejects_nan_from_any_order()
    {
        var sourceOrders = new List<SearchOrder>
        {
            new(new ConstantScorer(1f), SearchSortDirection.ScoreAscending),
            new(new SpecialValueScorer(), SearchSortDirection.ScoreAscending)
        };
        var query = new SearchQuery(
            new ArrayProvider(3, 2, 1),
            null!,
            sourceOrders,
            null!,
            0);
        sourceOrders.Clear();

        Assert.Equal(2, query.Orders.Count);
        Assert.Equal(new[] { 1, 2 }, ActorIds(Execute(query)));
    }

    [Fact]
    public void Builder_then_score_by_appends_order_and_builds_owned_snapshot()
    {
        var builder = SearchPipelineBuilder.Create();
        SearchQuery query;
        try
        {
            query = builder
                .From(new ArrayProvider(21, 12, 22, 11))
                .ScoreBy(new ActorIdGroupScorer(), SearchSortDirection.ScoreAscending)
                .ThenScoreBy(new ActorIdScorer(), SearchSortDirection.ScoreDescending)
                .Build();
        }
        finally
        {
            builder.Dispose();
        }

        Assert.Equal(2, query.Orders.Count);
        Assert.Equal(SearchSortDirection.ScoreAscending, query.Orders[0].Direction);
        Assert.Equal(SearchSortDirection.ScoreDescending, query.Orders[1].Direction);
        Assert.Equal(new[] { 12, 11, 22, 21 }, ActorIds(Execute(query)));
    }

    private static List<EntityId> Execute(SearchQuery query)
    {
        using var context = new SearchContext();
        var results = new List<EntityId>();
        new TargetSearchEngine().SearchIds(in query, context, results);
        return results;
    }

    private static int[] ActorIds(IEnumerable<EntityId> ids)
    {
        return ids.Select(static id => checked((int)id.Value)).ToArray();
    }

    private sealed class ArrayProvider : ICandidateProvider
    {
        private readonly EntityId[] _ids;

        public ArrayProvider(params int[] actorIds)
        {
            _ids = actorIds.Select(static actorId => (EntityId)new EntityId(actorId)).ToArray();
        }

        public void ForEachCandidate<TConsumer>(
            in SearchQuery query,
            SearchContext context,
            ref TConsumer consumer)
            where TConsumer : struct, ICandidateConsumer
        {
            for (var i = 0; i < _ids.Length; i++)
            {
                consumer.Consume(_ids[i]);
            }
        }
    }

    private sealed class RecordingSelector : ITargetSelector
    {
        public int ReceivedHitCount { get; private set; }

        public void Select(
            in SearchQuery query,
            SearchContext context,
            SearchHitView hits,
            SearchResultWriter results)
        {
            ReceivedHitCount = hits.Count;
            for (int i = 0; i < hits.Count; i++)
            {
                results.Add(hits[i].Id);
            }
        }
    }

    private sealed class ThrowingStreamingSelector : IStreamingTopKByScoreSelector
    {
        public void Select(
            in SearchQuery query,
            SearchContext context,
            SearchHitView hits,
            SearchResultWriter results)
        {
            throw new InvalidOperationException("The fused path must not invoke selector post-processing.");
        }
    }

    private sealed class TestPositionProvider : IPositionProvider
    {
        public static readonly TestPositionProvider Instance = new();

        public bool TryGetPosition(EntityId entity, out Vec2 position)
        {
            position = Vec2.Zero;
            return entity.IsValid;
        }
    }

    private sealed class TestEntityKeyProvider : IEntityKeyProvider
    {
        public static readonly TestEntityKeyProvider Instance = new();

        public ulong GetKey(EntityId id)
        {
            return id.Value;
        }
    }

    private sealed class ConstantEntityKeyProvider : IEntityKeyProvider
    {
        public static readonly ConstantEntityKeyProvider Instance = new();

        public ulong GetKey(EntityId id)
        {
            return 1UL;
        }
    }

    private sealed class RecordingSearchStats : ISearchStats
    {
        public int CandidateCount { get; private set; }
        public int HitCount { get; private set; }
        public int ResultCount { get; private set; }

        public void Reset()
        {
            CandidateCount = 0;
            HitCount = 0;
            ResultCount = 0;
        }

        public void OnCandidate()
        {
            CandidateCount++;
        }

        public void OnHit()
        {
            HitCount++;
        }

        public void OnResult(int count)
        {
            ResultCount = count;
        }
    }

    private sealed class ActorIdScorer : ITargetScorer
    {
        public float Score(in SearchQuery query, SearchContext context, EntityId candidate)
        {
            return candidate.Value;
        }
    }

    private sealed class ActorIdGroupScorer : ITargetScorer
    {
        public float Score(in SearchQuery query, SearchContext context, EntityId candidate)
        {
            return candidate.Value / 10UL;
        }
    }

    private sealed class ConstantScorer : ITargetScorer
    {
        private readonly float _score;

        public ConstantScorer(float score)
        {
            _score = score;
        }

        public float Score(in SearchQuery query, SearchContext context, EntityId candidate)
        {
            return _score;
        }
    }

    [TargetScorer(0x7F03, "TargetingTests.RegistryScorer")]
    private sealed class RegistryScorer : ITargetScorer
    {
        public float Score(in SearchQuery query, SearchContext context, EntityId candidate)
        {
            return candidate.Value;
        }
    }

    [TargetScorer(0x7F04, "TargetingTests.ParameterizedRegistryScorer")]
    private sealed class ParameterizedRegistryScorer : ITargetScorer
    {
        private readonly float _score;

        public ParameterizedRegistryScorer(float score)
        {
            _score = score;
        }

        public float Score(in SearchQuery query, SearchContext context, EntityId candidate)
        {
            return _score;
        }
    }

    private sealed class SpecialValueScorer : ITargetScorer
    {
        public float Score(in SearchQuery query, SearchContext context, EntityId candidate)
        {
            return candidate.Value switch
            {
                1 => float.NegativeInfinity,
                2 => float.PositiveInfinity,
                _ => float.NaN,
            };
        }
    }

    private sealed class FirstActorNaNScorer : ITargetScorer
    {
        public float Score(in SearchQuery query, SearchContext context, EntityId candidate)
        {
            return candidate.Value == 1UL ? float.NaN : candidate.Value;
        }
    }

    private sealed class MinimumActorIdRule : ITargetRule
    {
        private readonly ulong _minimum;

        public MinimumActorIdRule(ulong minimum)
        {
            _minimum = minimum;
        }

        public bool Test(in SearchQuery query, SearchContext context, EntityId candidate)
        {
            return candidate.Value >= _minimum;
        }
    }

    private sealed class OddActorIdRule : ITargetRule
    {
        public static readonly OddActorIdRule Instance = new();

        public bool Test(in SearchQuery query, SearchContext context, EntityId candidate)
        {
            return (candidate.Value & 1UL) != 0UL;
        }
    }

    [TargetRule(0x7F01, "TargetingTests.RegistryRule")]
    private sealed class RegistryRule : ITargetRule
    {
        public bool Test(in SearchQuery query, SearchContext context, EntityId candidate)
        {
            return true;
        }
    }
}
