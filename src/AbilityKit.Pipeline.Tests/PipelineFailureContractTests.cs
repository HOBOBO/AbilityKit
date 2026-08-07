using Xunit;

namespace AbilityKit.Pipeline.Tests;

public sealed class PipelineFailureContractTests
{
    [Fact]
    public void InstantPipelinePreservesExecutionAndErrorHandlerFailures()
    {
        var pipeline = new InstantAbilityPipeline<TestContext>();
        pipeline.AddPhase(new DoubleFailurePhase());

        var result = pipeline.RunToCompletion(new TestConfig(), new TestContext());

        var aggregate = Assert.IsType<AggregateException>(result.Exception);
        Assert.Collection(
            aggregate.InnerExceptions,
            error => Assert.Equal("execute failed", error.Message),
            error => Assert.Equal("handler failed", error.Message));
        Assert.Equal(EAbilityPipelineState.Failed, result.State);
    }

    private sealed class TestContext : AAbilityPipelineContext;

    private sealed class DoubleFailurePhase : AbilityInstantPhaseBase<TestContext>
    {
        public DoubleFailurePhase() : base("double-failure") { }

        protected override void OnInstantExecute(TestContext context) =>
            throw new InvalidOperationException("execute failed");

        public override void HandleError(TestContext context, Exception exception) =>
            throw new InvalidOperationException("handler failed");
    }

    private sealed class TestConfig : IAbilityPipelineConfig
    {
        public int ConfigId => 1;
        public string ConfigName => "test";
        public IReadOnlyList<IAbilityPhaseConfig> PhaseConfigs => Array.Empty<IAbilityPhaseConfig>();
        public bool AllowInterrupt => true;
        public bool AllowPause => true;
    }
}
