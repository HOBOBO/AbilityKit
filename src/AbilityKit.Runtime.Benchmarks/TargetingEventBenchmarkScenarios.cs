using AbilityKit.Battle.SearchTarget;
using AbilityKit.Battle.SearchTarget.Selectors;
using AbilityKit.Benchmarking;
using AbilityKit.Core.Eventing;

namespace AbilityKit.Runtime.Benchmarks;

public sealed class TargetingStreamingTopKScenario : BenchmarkScenarioBase
{
    private readonly int _candidateCount;
    private readonly int _topK;
    private readonly int _batchSize;
    private readonly BenchmarkDescriptor _descriptor;
    private readonly TargetSearchEngine _engine = new();
    private readonly SearchContext _context = new();
    private readonly List<EntityId> _results;
    private SearchQuery _query;
    private long _searchCount;
    private long _resultCount;
    private ulong _checksum;

    public TargetingStreamingTopKScenario(int candidateCount, int topK, int batchSize = 10)
    {
        if (candidateCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(candidateCount));
        if (topK <= 0 || topK > candidateCount)
            throw new ArgumentOutOfRangeException(nameof(topK));
        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize));

        _candidateCount = candidateCount;
        _topK = topK;
        _batchSize = batchSize;
        _results = new List<EntityId>(topK);
        _descriptor = new BenchmarkDescriptor(
            $"targeting.streaming-top-k.candidates-{candidateCount}.top-{topK}",
            "targeting",
            "candidate-evaluation",
            new Dictionary<string, string>
            {
                [BenchmarkWorkloadDimensions.Scope] = BenchmarkScenarioScopes.Package,
                ["candidateCount"] = candidateCount.ToString(),
                ["selector"] = "streaming-top-k",
                ["sortDirection"] = "score-descending",
                ["topK"] = topK.ToString(),
                ["batchSize"] = batchSize.ToString(),
                ["resultReuse"] = "single-list"
            });
    }

    public override BenchmarkDescriptor Descriptor => _descriptor;

    public override void Setup()
    {
        _query = new SearchQuery(
            new SequentialCandidateProvider(_candidateCount),
            null!,
            new[] { new SearchOrder(EntityValueScorer.Instance) },
            new StreamingTopKByScoreSelector(),
            _topK);

        _engine.SearchIds(in _query, _context, _results);
    }

    public override void IterationSetup()
    {
        _searchCount = 0;
        _resultCount = 0;
        _checksum = 0;
    }

    public override long Execute(int invocationCount)
    {
        var searchCount = checked(invocationCount * _batchSize);
        ulong checksum = 0;
        long resultCount = 0;

        for (var search = 0; search < searchCount; search++)
        {
            _engine.SearchIds(in _query, _context, _results);
            resultCount += _results.Count;
            for (var index = 0; index < _results.Count; index++)
                checksum += _results[index].Value;
        }

        _searchCount = searchCount;
        _resultCount = resultCount;
        _checksum = checksum;
        return checked((long)searchCount * _candidateCount);
    }

    public override void Validate()
    {
        var expectedResultCount = checked(_searchCount * _topK);
        var firstSelected = _candidateCount - _topK + 1L;
        var expectedPerSearch = checked((ulong)((firstSelected + _candidateCount) * _topK / 2L));
        var expectedChecksum = checked(expectedPerSearch * (ulong)_searchCount);

        if (_resultCount != expectedResultCount || _checksum != expectedChecksum)
            throw new InvalidOperationException("Targeting streaming Top-K result is inconsistent.");
    }

    public override string GetDeterminismDigest() =>
        $"searches={_searchCount};results={_resultCount};checksum={_checksum}";

    public override void Cleanup()
    {
        _context.Dispose();
    }

    private sealed class SequentialCandidateProvider : ICandidateProvider
    {
        private readonly EntityId[] _candidates;

        public SequentialCandidateProvider(int candidateCount)
        {
            _candidates = new EntityId[candidateCount];
            for (var index = 0; index < candidateCount; index++)
                _candidates[index] = new EntityId(index + 1);
        }

        public void ForEachCandidate<TConsumer>(
            in SearchQuery query,
            SearchContext context,
            ref TConsumer consumer)
            where TConsumer : struct, ICandidateConsumer
        {
            for (var index = 0; index < _candidates.Length; index++)
                consumer.Consume(_candidates[index]);
        }
    }

    private sealed class EntityValueScorer : ITargetScorer
    {
        public static readonly EntityValueScorer Instance = new();

        public float Score(in SearchQuery query, SearchContext context, EntityId candidate) =>
            candidate.Value;
    }
}

public sealed class EventDispatcherPublishScenario : BenchmarkScenarioBase
{
    private const int EventId = 0x5A18;

    private readonly int _fanout;
    private readonly int _batchSize;
    private readonly BenchmarkDescriptor _descriptor;
    private readonly List<IEventSubscription> _subscriptions;
    private EventDispatcher _dispatcher = null!;
    private long _executionCount;
    private long _checksum;
    private long _eventCount;

    public EventDispatcherPublishScenario(int fanout, int batchSize = 100)
    {
        if (fanout <= 0)
            throw new ArgumentOutOfRangeException(nameof(fanout));
        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize));

        _fanout = fanout;
        _batchSize = batchSize;
        _subscriptions = new List<IEventSubscription>(fanout);
        _descriptor = new BenchmarkDescriptor(
            $"event-dispatcher.publish.fanout-{fanout}",
            "event-dispatcher",
            "handler-invocation",
            new Dictionary<string, string>
            {
                [BenchmarkWorkloadDimensions.Scope] = BenchmarkScenarioScopes.Capability,
                ["eventKey"] = "integer-id",
                ["fanout"] = fanout.ToString(),
                ["subscriptionMode"] = "persistent",
                ["argumentRelease"] = "disabled",
                ["batchSize"] = batchSize.ToString()
            });
    }

    public override BenchmarkDescriptor Descriptor => _descriptor;

    public override void Setup()
    {
        _dispatcher = new EventDispatcher();
        for (var index = 0; index < _fanout; index++)
        {
            _subscriptions.Add(_dispatcher.Subscribe<BenchmarkDispatchEvent>(
                EventId,
                HandleEvent,
                priority: index));
        }
    }

    public override void IterationSetup()
    {
        _executionCount = 0;
        _checksum = 0;
        _eventCount = 0;
    }

    public override long Execute(int invocationCount)
    {
        var eventCount = checked(invocationCount * _batchSize);
        for (var index = 0; index < eventCount; index++)
        {
            var evt = new BenchmarkDispatchEvent(index + 1);
            _dispatcher.Publish(EventId, in evt, autoReleaseArgs: false);
        }

        _eventCount = eventCount;
        return checked((long)eventCount * _fanout);
    }

    public override void Validate()
    {
        var expectedExecutions = checked(_eventCount * _fanout);
        var expectedChecksum = checked(_fanout * (_eventCount * (_eventCount + 1) / 2));
        if (_executionCount != expectedExecutions || _checksum != expectedChecksum)
            throw new InvalidOperationException("EventDispatcher publish result is inconsistent.");
    }

    public override string GetDeterminismDigest() =>
        $"events={_eventCount};executions={_executionCount};checksum={_checksum}";

    public override void Cleanup()
    {
        for (var index = _subscriptions.Count - 1; index >= 0; index--)
            _subscriptions[index].Unsubscribe();
        _subscriptions.Clear();
    }

    private void HandleEvent(BenchmarkDispatchEvent evt)
    {
        _executionCount++;
        _checksum += evt.Value;
    }
}

public readonly record struct BenchmarkDispatchEvent(int Value);
