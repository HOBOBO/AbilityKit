using AbilityKit.Benchmarking;

namespace AbilityKit.Runtime.Benchmarks;

public static class BenchmarkScenarioCatalog
{
    private static readonly IReadOnlySet<string> SupportedModules = new HashSet<string>(
        Create("full").Select(scenario => scenario.Descriptor.Module),
        StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyCollection<string> Modules => SupportedModules;

    public static bool IsSupportedModule(string module) =>
        module.Equals("all", StringComparison.OrdinalIgnoreCase)
        || SupportedModules.Contains(module);

    public static IReadOnlyList<IBenchmarkScenario> Create(string profile)
    {
        if (!profile.Equals("smoke", StringComparison.OrdinalIgnoreCase)
            && !profile.Equals("full", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Profile must be 'smoke' or 'full'.", nameof(profile));
        }

        var scenarios = new List<IBenchmarkScenario>
        {
            new AttributeRecomputeScenario(modifierCount: 1),
            new AttributeRecomputeScenario(modifierCount: 16),
            new ModifierComposeSortedScenario(modifierCount: 4),
            new ModifierComposeSortedScenario(modifierCount: 32),
            new RecordIdHashScenario(nameLength: 16),
            new RecordIdHashScenario(nameLength: 64),
            new LegacyFrameIndexScenario(frameCount: 128),
            new SortedIntSetFrameIndexScenario(frameCount: 128),
            new TargetingStreamingTopKScenario(candidateCount: 128, topK: 8),
            new TargetingStreamingTopKScenario(candidateCount: 1_024, topK: 16),
            new PipelineSynchronousScenario(4, batchSize: 100),
            new PipelineSynchronousScenario(32, batchSize: 100),
            new PipelineActiveRunsScenario(1_000),
            new EventDispatcherPublishScenario(fanout: 1),
            new EventDispatcherPublishScenario(fanout: 64),
            new TriggerDispatchScenario(1, queued: false, reuseControl: false, batchSize: 100),
            new TriggerDispatchScenario(64, queued: false, reuseControl: false, batchSize: 100),
            new TriggerDispatchScenario(64, queued: false, reuseControl: true, batchSize: 100),
            new TriggerDispatchScenario(64, queued: true, reuseControl: false, batchSize: 100)
        };

        if (profile.Equals("full", StringComparison.OrdinalIgnoreCase))
        {
            scenarios.Add(new AttributeRecomputeScenario(modifierCount: 64));
            scenarios.Add(new ModifierComposeSortedScenario(modifierCount: 128));
            scenarios.Add(new RecordIdHashScenario(nameLength: 256));
            scenarios.Add(new LegacyFrameIndexScenario(frameCount: 1_024));
            scenarios.Add(new SortedIntSetFrameIndexScenario(frameCount: 1_024));
            scenarios.Add(new TargetingStreamingTopKScenario(candidateCount: 4_096, topK: 32));
            scenarios.Add(new PipelineSynchronousScenario(64, batchSize: 100));
            scenarios.Add(new PipelineActiveRunsScenario(5_000));
            scenarios.Add(new EventDispatcherPublishScenario(fanout: 256));
            scenarios.Add(new TriggerDispatchScenario(256, queued: false, reuseControl: false, batchSize: 100));
            scenarios.Add(new TriggerDispatchScenario(256, queued: false, reuseControl: true, batchSize: 100));
            scenarios.Add(new TriggerDispatchScenario(256, queued: true, reuseControl: false, batchSize: 100));
        }

        return scenarios;
    }
}
