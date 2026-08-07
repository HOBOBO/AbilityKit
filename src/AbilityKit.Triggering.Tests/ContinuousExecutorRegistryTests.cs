using AbilityKit.Triggering.Runtime.Continuous;
using Xunit;

namespace AbilityKit.Triggering.Tests;

public sealed class ContinuousExecutorRegistryTests
{
    [Fact]
    public void Register_rejects_invalid_executor_and_interval()
    {
        Assert.Throws<ArgumentNullException>(() => ContinuousExecutorRegistry.Register<TestContext>(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => ContinuousExecutorRegistry.Register(new TestExecutor(), -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ContinuousExecutorRegistry.Register(new TestExecutor(), float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => ContinuousExecutorRegistry.Register(new TestExecutor(), float.PositiveInfinity));
    }

    [Fact]
    public void Try_methods_return_false_for_wrong_context_type()
    {
        var executor = new TestExecutor();
        var id = ContinuousExecutorRegistry.Register(executor, 10);
        try
        {
            Assert.False(ContinuousExecutorRegistry.TryStart(id, new object()));
            Assert.False(ContinuousExecutorRegistry.TryExecute(id, 16, new TestInstance(id), new object()));
            Assert.False(ContinuousExecutorRegistry.TryTerminate(id, EContinuousState.Completed, new object()));
            Assert.Equal(0, executor.CallbackCount);
        }
        finally
        {
            ContinuousExecutorRegistry.Unregister(id);
        }
    }

    [Fact]
    public void Try_methods_propagate_executor_failures()
    {
        var id = ContinuousExecutorRegistry.Register(new ThrowingExecutor());
        try
        {
            var error = Assert.Throws<InvalidOperationException>(() =>
                ContinuousExecutorRegistry.TryStart(id, new TestContext()));
            Assert.Equal("start failed", error.Message);
        }
        finally
        {
            ContinuousExecutorRegistry.Unregister(id);
        }
    }

    [Fact]
    public async Task Register_read_and_unregister_are_thread_safe()
    {
        const int workerCount = 8;
        const int registrationsPerWorker = 200;
        var baseline = ContinuousExecutorRegistry.Count;
        var ids = new System.Collections.Concurrent.ConcurrentBag<int>();

        var registerTasks = Enumerable.Range(0, workerCount).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < registrationsPerWorker; i++)
            {
                var id = ContinuousExecutorRegistry.Register(new TestExecutor(), i);
                ids.Add(id);
                Assert.Equal(i, ContinuousExecutorRegistry.GetInterval(id));
            }
        }));
        await Task.WhenAll(registerTasks).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(baseline + workerCount * registrationsPerWorker, ContinuousExecutorRegistry.Count);
        await Task.WhenAll(ids.Select(id => Task.Run(() => ContinuousExecutorRegistry.Unregister(id))))
            .WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(baseline, ContinuousExecutorRegistry.Count);
    }

    private sealed class TestContext;

    private sealed class TestExecutor : ContinuousExecutorBase<TestContext>
    {
        public int CallbackCount { get; private set; }
        protected override void OnStart(TestContext ctx) => CallbackCount++;
        protected override void OnUpdate(float deltaTimeMs, ContinuousExecuteContext execCtx, TestContext ctx) => CallbackCount++;
        protected override void OnTerminate(EContinuousState terminationReason, TestContext ctx) => CallbackCount++;
    }

    private sealed class ThrowingExecutor : ContinuousExecutorBase<TestContext>
    {
        protected override void OnStart(TestContext ctx) => throw new InvalidOperationException("start failed");
    }

    private sealed class TestInstance : IContinuousTriggerInstance
    {
        public TestInstance(int triggerId) => TriggerId = triggerId;
        public int InstanceId => 1;
        public int TriggerId { get; }
        public EContinuousState CurrentState => EContinuousState.Running;
        public int ExecutionCount => 0;
        public float ElapsedMs => 0;
        public float LastExecuteAtMs => 0;
        public int MaxExecutions => -1;
        public bool CanBeInterrupted => true;
        public string InterruptReason => string.Empty;
        public bool IsCompleted => false;
        public bool IsTerminated => false;
    }
}
