using AbilityKit.Benchmarking;

namespace AbilityKit.Runtime.Benchmarks;

public static class BenchmarkScenarioCatalog
{
    public static IReadOnlyList<IBenchmarkScenario> Create(string profile)
    {
        var scenarios = new List<IBenchmarkScenario>
        {
            new PipelineSynchronousScenario(4, batchSize: 100),
            new PipelineSynchronousScenario(32, batchSize: 100),
            new PipelineActiveRunsScenario(1_000),
            new TriggerDispatchScenario(1, queued: false, reuseControl: false, batchSize: 100),
            new TriggerDispatchScenario(64, queued: false, reuseControl: false, batchSize: 100),
            new TriggerDispatchScenario(64, queued: false, reuseControl: true, batchSize: 100),
            new TriggerDispatchScenario(64, queued: true, reuseControl: false, batchSize: 100)
        };

        if (profile.Equals("full", StringComparison.OrdinalIgnoreCase))
        {
            scenarios.Add(new PipelineSynchronousScenario(64, batchSize: 100));
            scenarios.Add(new PipelineActiveRunsScenario(5_000));
            scenarios.Add(new TriggerDispatchScenario(256, queued: false, reuseControl: false, batchSize: 100));
            scenarios.Add(new TriggerDispatchScenario(256, queued: false, reuseControl: true, batchSize: 100));
            scenarios.Add(new TriggerDispatchScenario(256, queued: true, reuseControl: false, batchSize: 100));
        }

        return scenarios;
    }
}
