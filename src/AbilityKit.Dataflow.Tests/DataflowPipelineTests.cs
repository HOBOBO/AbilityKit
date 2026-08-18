using AbilityKit.Dataflow;
using Xunit;

namespace AbilityKit.Dataflow.Tests;

public sealed class DataflowPipelineTests
{
    [Fact]
    public void Execute_feeds_compatible_output_into_the_next_processor()
    {
        var pipeline = new DataflowPipeline<int, int>()
            .AddProcessor(new DelegateProcessor<int, int>("Increment", (value, _) => value + 1))
            .AddProcessor(new DelegateProcessor<int, int>("Double", (value, _) => value * 2));

        var result = pipeline.Execute(3, new DataflowContext());

        Assert.True(result.IsSuccess);
        Assert.Equal(8, result.Output);
        Assert.Equal(2, result.ProcessedCount);
    }

    [Fact]
    public void Execute_keeps_original_input_for_incompatible_output_types()
    {
        var observedInput = string.Empty;
        var pipeline = new DataflowPipeline<string, int>()
            .AddProcessor(new DelegateProcessor<string, int>("Length", (value, _) => value.Length))
            .AddProcessor(new DelegateProcessor<string, int>("Observe", (value, _) =>
            {
                observedInput = value;
                return value[0];
            }));

        var result = pipeline.Execute("abc", new DataflowContext());

        Assert.Equal("abc", observedInput);
        Assert.Equal((int)'a', result.Output);
        Assert.Equal(2, result.ProcessedCount);
    }

    [Fact]
    public void Execute_reports_abort_after_last_processor_and_preserves_output()
    {
        var pipeline = new DataflowPipeline<int, int>()
            .AddProcessor(new DelegateProcessor<int, int>("Stop", (value, context) =>
            {
                context.Abort();
                return value + 4;
            }));

        var result = pipeline.Execute(3, new DataflowContext());

        Assert.True(result.IsAborted);
        Assert.False(result.IsSuccess);
        Assert.Equal(7, result.Output);
        Assert.Equal(1, result.ProcessedCount);
    }

    [Fact]
    public void Execute_honors_pre_aborted_context_even_for_empty_pipeline()
    {
        var context = new DataflowContext();
        context.Abort();

        var result = new DataflowPipeline<int, int>().Execute(3, context);

        Assert.True(result.IsAborted);
        Assert.Equal(0, result.ProcessedCount);
    }

    [Fact]
    public void Execute_failure_preserves_partial_output_and_failed_stage_identity()
    {
        var error = new InvalidOperationException("failure");
        var pipeline = new DataflowPipeline<int, int>()
            .AddProcessor(new DelegateProcessor<int, int>("Increment", (value, _) => value + 1))
            .AddProcessor(new DelegateProcessor<int, int>("Broken", (_, _) => throw error));

        var result = pipeline.Execute(1, new DataflowContext());

        Assert.True(result.HasError);
        Assert.Same(error, result.Error);
        Assert.Equal(2, result.Output);
        Assert.Equal(1, result.ProcessedCount);
        Assert.Equal(1, result.FailedProcessorIndex);
        Assert.Equal("Broken", result.FailedProcessorName);
    }

    [Fact]
    public void Execute_uses_a_processor_snapshot()
    {
        var pipeline = new DataflowPipeline<int, int>();
        var appended = new DelegateProcessor<int, int>("Appended", (value, _) => value + 1);
        pipeline.AddProcessor(new DelegateProcessor<int, int>("Mutator", (value, _) =>
        {
            pipeline.AddProcessor(appended);
            return value + 1;
        }));
        pipeline.AddProcessor(new DelegateProcessor<int, int>("Existing", (value, _) => value + 1));

        var first = pipeline.Execute(0, new DataflowContext());
        var second = pipeline.Execute(0, new DataflowContext());

        Assert.Equal(2, first.Output);
        Assert.Equal(2, first.ProcessedCount);
        Assert.Equal(3, second.Output);
        Assert.Equal(3, second.ProcessedCount);
    }

    [Fact]
    public void AddProcessors_validates_the_whole_batch_before_mutating_pipeline()
    {
        var pipeline = new DataflowPipeline<int, int>();
        var valid = new DelegateProcessor<int, int>("Valid", (value, _) => value);

        Assert.Throws<ArgumentNullException>(() => pipeline.AddProcessors(null!));
        Assert.Throws<ArgumentException>(() => pipeline.AddProcessors(valid, null!));
        Assert.True(pipeline.IsEmpty);
    }

    [Fact]
    public void Builder_returns_independent_pipeline_snapshots()
    {
        var builder = new DataflowPipelineBuilder<int, int>()
            .Add((value, _) => value + 1);
        var first = builder.Build();
        builder.Add((value, _) => value + 1);
        var second = builder.Build();

        first.Clear();

        Assert.True(first.IsEmpty);
        Assert.Equal(2, second.ProcessorCount);
        Assert.Equal(2, second.Execute(0, new DataflowContext()).Output);
    }

    [Fact]
    public void Clone_copies_structure_and_shares_processor_instances()
    {
        var processor = new DelegateProcessor<int, int>("Shared", (value, _) => value + 1);
        var original = new DataflowPipeline<int, int>().AddProcessor(processor);
        var clone = ((DataflowPipeline<int, int>)original).Clone();

        clone.Clear();

        Assert.Equal(1, original.ProcessorCount);
        Assert.Same(processor, original.GetProcessor(0));
        Assert.True(clone.IsEmpty);
    }

    [Fact]
    public void Composite_chains_compatible_outputs_and_copies_constructor_array()
    {
        var originalSecond = new DelegateProcessor<int, int>("Double", (value, _) => value * 2);
        IDataflowProcessor<int, int>[] processors =
        {
            new DelegateProcessor<int, int>("Increment", (value, _) => value + 1),
            originalSecond
        };
        var composite = new CompositeProcessor<int, int>(processors);
        processors[1] = new DelegateProcessor<int, int>("Replacement", (_, _) => -1);

        var output = composite.Process(3, new DataflowContext());

        Assert.Equal(8, output);
    }

    [Fact]
    public void Composite_rejects_null_batches_and_members()
    {
        var valid = new DelegateProcessor<int, int>("Valid", (value, _) => value);

        Assert.Throws<ArgumentNullException>(() => new CompositeProcessor<int, int>(null!));
        Assert.Throws<ArgumentException>(() => new CompositeProcessor<int, int>(valid, null!));
    }

    [Fact]
    public void Processor_base_rejects_null_context()
    {
        Assert.Throws<ArgumentNullException>(() => new IdentityProcessor().Process(1, null!));
    }

    private sealed class DelegateProcessor<TInput, TOutput> : IDataflowProcessor<TInput, TOutput>
    {
        private readonly Func<TInput, IDataflowContext, TOutput> _process;

        public DelegateProcessor(string name, Func<TInput, IDataflowContext, TOutput> process)
        {
            Name = name;
            _process = process;
        }

        public string Name { get; }

        public TOutput Process(TInput input, IDataflowContext context) => _process(input, context);
    }

    private sealed class IdentityProcessor : DataflowProcessor<int>
    {
    }
}
