using AbilityKit.Compute;
using Xunit;

namespace AbilityKit.Compute.Tests;

public sealed class ComputeBackendChainTests
{
    [Fact]
    public void Execute_UsesExplicitFallbackAfterPreferredBackendDeclines()
    {
        var preferred = new StubBackend(ComputeAttemptOutcome.Declined);
        var chain = new ComputeBackendChain(new IComputeBackend[]
        {
            preferred,
            new ManagedSequentialComputeBackend()
        });
        var inputs = new[] { new Input(2), new Input(4), new Input(8) };
        var outputs = new Output[inputs.Length];

        var result = chain.Execute(
            new DoubleKernel(),
            inputs,
            outputs,
            inputs.Length,
            new ComputeExecutionOptions("tests.double"));

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.AttemptCount);
        Assert.Equal(ManagedSequentialComputeBackend.DefaultId, result.BackendId);
        Assert.Equal(new[] { 4, 8, 16 }, outputs.Select(output => output.Value));
    }

    [Fact]
    public void Execute_RejectsInvalidAcceleratedOutputBeforeFallback()
    {
        var invalid = new StubBackend(ComputeAttemptOutcome.Succeeded, outputValue: -1);
        var observer = new RecordingObserver();
        var chain = new ComputeBackendChain(
            new IComputeBackend[] { invalid, new ManagedSequentialComputeBackend() },
            observer);
        var inputs = new[] { new Input(3) };
        var outputs = new Output[1];

        var result = chain.Execute(
            new DoubleKernel(),
            inputs,
            outputs,
            1,
            new ComputeExecutionOptions("tests.validation"),
            new NonNegativeValidator());

        Assert.True(result.Succeeded);
        Assert.Equal(6, outputs[0].Value);
        Assert.Equal(
            new[] { ComputeAttemptOutcome.InvalidOutput, ComputeAttemptOutcome.Succeeded },
            observer.Outcomes);
    }

    [Fact]
    public void Execute_ConvertsBackendExceptionAndContinuesFallback()
    {
        var chain = new ComputeBackendChain(new IComputeBackend[]
        {
            new ThrowingBackend(),
            new ManagedSequentialComputeBackend()
        });
        var inputs = new[] { new Input(5) };
        var outputs = new Output[1];

        var result = chain.Execute(
            new DoubleKernel(),
            inputs,
            outputs,
            1,
            new ComputeExecutionOptions("tests.failure"));

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.AttemptCount);
        Assert.Equal(10, outputs[0].Value);
    }

    [Fact]
    public void Execute_DoesNotInventAnImplicitBackend()
    {
        var chain = new ComputeBackendChain(Array.Empty<IComputeBackend>());
        var outputs = new Output[1];

        var result = chain.Execute(
            new DoubleKernel(),
            new[] { new Input(7) },
            outputs,
            1,
            new ComputeExecutionOptions("tests.no-backend"));

        Assert.False(result.Succeeded);
        Assert.Equal(0, result.AttemptCount);
        Assert.Equal(0, outputs[0].Value);
    }

    [Fact]
    public void ManagedParallelBackend_ExecutesSamePortableKernel()
    {
        var backend = new ManagedParallelComputeBackend(
            minimumItemCount: 0,
            maximumDegreeOfParallelism: 2);
        var inputs = Enumerable.Range(1, 256).Select(value => new Input(value)).ToArray();
        var outputs = new Output[inputs.Length];

        var attempt = backend.Execute(
            new DoubleKernel(),
            inputs,
            outputs,
            inputs.Length,
            new ComputeExecutionOptions("tests.parallel"));

        Assert.True(attempt.Succeeded);
        Assert.Equal(512, outputs[^1].Value);
    }

    [Fact]
    public void Execute_ValidatorExceptionDoesNotPreventFallback()
    {
        var observer = new RecordingObserver();
        var chain = new ComputeBackendChain(
            new IComputeBackend[]
            {
                new StubBackend(ComputeAttemptOutcome.Succeeded, outputValue: 9),
                new ManagedSequentialComputeBackend()
            },
            observer);
        var outputs = new Output[1];

        var result = chain.Execute(
            new DoubleKernel(),
            new[] { new Input(5) },
            outputs,
            1,
            new ComputeExecutionOptions("tests.validator-failure"),
            new ThrowingValidator());

        Assert.False(result.Succeeded);
        Assert.Equal(2, result.AttemptCount);
        Assert.All(observer.Outcomes, outcome => Assert.Equal(ComputeAttemptOutcome.InvalidOutput, outcome));
    }

    private readonly struct Input(int value)
    {
        public int Value { get; } = value;
    }

    private readonly struct Output(int value)
    {
        public int Value { get; } = value;
    }

    private struct DoubleKernel : IComputeKernel<Input, Output>
    {
        public Output Execute(in Input input) => new(input.Value * 2);
    }

    private sealed class StubBackend : IComputeBackend
    {
        private readonly ComputeAttemptOutcome _outcome;
        private readonly int _outputValue;

        public StubBackend(ComputeAttemptOutcome outcome, int outputValue = 0)
        {
            _outcome = outcome;
            _outputValue = outputValue;
        }

        public string Id => "tests.stub";
        public bool IsAvailable => true;

        public ComputeAttempt Execute<TKernel, TInput, TOutput>(
            TKernel kernel,
            TInput[] inputs,
            TOutput[] outputs,
            int count,
            in ComputeExecutionOptions options)
            where TKernel : struct, IComputeKernel<TInput, TOutput>
            where TInput : unmanaged
            where TOutput : unmanaged
        {
            if (_outcome == ComputeAttemptOutcome.Succeeded)
            {
                object boxed = new Output(_outputValue);
                outputs[0] = (TOutput)boxed;
                return ComputeAttempt.Success(Id);
            }

            return ComputeAttempt.Declined(Id);
        }
    }

    private sealed class ThrowingBackend : IComputeBackend
    {
        public string Id => "tests.throwing";
        public bool IsAvailable => true;

        public ComputeAttempt Execute<TKernel, TInput, TOutput>(
            TKernel kernel,
            TInput[] inputs,
            TOutput[] outputs,
            int count,
            in ComputeExecutionOptions options)
            where TKernel : struct, IComputeKernel<TInput, TOutput>
            where TInput : unmanaged
            where TOutput : unmanaged
        {
            throw new InvalidOperationException("backend failure");
        }
    }

    private sealed class NonNegativeValidator : IComputeResultValidator<Input, Output>
    {
        public bool Validate(Input[] inputs, Output[] outputs, int count, out string reason)
        {
            reason = "negative output";
            return outputs.Take(count).All(output => output.Value >= 0);
        }
    }

    private sealed class RecordingObserver : IComputeObserver
    {
        public List<ComputeAttemptOutcome> Outcomes { get; } = new();

        public void OnAttempt(string operationId, int itemCount, in ComputeAttempt attempt)
        {
            Outcomes.Add(attempt.Outcome);
        }
    }

    private sealed class ThrowingValidator : IComputeResultValidator<Input, Output>
    {
        public bool Validate(Input[] inputs, Output[] outputs, int count, out string reason)
        {
            throw new InvalidOperationException("validator failure");
        }
    }
}
