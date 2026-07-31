using AbilityKit.Game.View.Loading;
using Xunit;

namespace AbilityKit.Game.View.Runtime.Tests;

public sealed class ClientLoadingPipelineTests
{
    [Fact]
    public async Task Pipeline_ConvertsWeightedStepsIntoMonotonicOverallProgress()
    {
        var definition = new ClientLoadingPipelineDefinition(new[]
        {
            new ClientLoadingStepDefinition("manifest", "instant", 20),
            new ClientLoadingStepDefinition("assets-a", "half", 40, parallelGroup: 1),
            new ClientLoadingStepDefinition("assets-b", "instant", 40, parallelGroup: 1)
        });
        var registry = new ClientLoadingStepRegistry()
            .Register("instant", _ => new DelegateClientLoadingStep((progress, _) =>
            {
                progress.Report(1f);
                return Task.CompletedTask;
            }))
            .Register("half", _ => new DelegateClientLoadingStep((progress, _) =>
            {
                progress.Report(0.5f);
                progress.Report(1f);
                return Task.CompletedTask;
            }));
        var observed = new List<int>();

        await new ClientLoadingPipeline(definition, registry).ExecuteAsync(
            new ImmediateProgress<ClientLoadingProgress>(value => observed.Add(value.OverallProgress)));

        Assert.NotEmpty(observed);
        Assert.Equal(100, observed[^1]);
        for (var i = 1; i < observed.Count; i++) Assert.True(observed[i] >= observed[i - 1]);
    }

    [Fact]
    public async Task ProgressRelay_CoalescesIntermediateValuesAndRetriesFinalValue()
    {
        var relay = new ClientLoadingProgressRelay();
        var uploads = new List<int>();
        var finalAttempts = 0;
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var uploadTask = relay.UploadUntilCompletedAsync(
            (progress, _) =>
            {
                if (progress == 100 && finalAttempts++ == 0) throw new TimeoutException("transient");
                uploads.Add(progress);
                return Task.CompletedTask;
            },
            new ClientLoadingProgressUploadOptions
            {
                SampleInterval = TimeSpan.FromMilliseconds(1),
                MaxSilence = TimeSpan.FromMilliseconds(5),
                RetryDelay = TimeSpan.FromMilliseconds(1),
                MinimumProgressDelta = 5,
                MaxFinalAttempts = 2
            },
            cancellation.Token);

        relay.Report(new ClientLoadingProgress("assets", 1, 0.01f));
        relay.Report(new ClientLoadingProgress("assets", 4, 0.04f));
        relay.Report(new ClientLoadingProgress("assets", 20, 0.2f));
        relay.Complete();
        await uploadTask;

        Assert.Equal(100, uploads[^1]);
        Assert.Equal(2, finalAttempts);
        Assert.DoesNotContain(1, uploads);
        Assert.DoesNotContain(4, uploads);
    }

    private sealed class ImmediateProgress<T> : IProgress<T>
    {
        private readonly Action<T> _report;

        public ImmediateProgress(Action<T> report)
        {
            _report = report;
        }

        public void Report(T value) => _report(value);
    }
}
