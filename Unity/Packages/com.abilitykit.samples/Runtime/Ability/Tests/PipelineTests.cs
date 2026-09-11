#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Samples.Logic.Ability.Core.Pipeline;

namespace AbilityKit.Samples.Logic.Ability.Tests
{
    /// <summary>
    /// 管线框架单元测试。
    /// </summary>
    public static class PipelineTests
    {
        public static void RunAll()
        {
            TestPipelinePhaseBase();
            TestTimelinePhaseMethod();
            TestPipelineContext();
            Console.WriteLine("[PipelineTests] All tests passed!");
        }

        private static void TestPipelinePhaseBase()
        {
            Console.WriteLine("[Test] PipelinePhaseBase");

            var context = new TestCtx();
            var phase = new TestSuccessPhase();
            var phaseSkip = new TestSkipPhase();

            // Test success
            var result = phase.Execute(context);
            Assert(result == PhaseResult.Success, $"Phase should succeed, got {result}");
            Assert(phase.Entered, "OnEnter should be called");
            Assert(phase.Completed, "OnComplete should be called");

            // Test skip
            context = new TestCtx();
            phaseSkip = new TestSkipPhase();
            result = phaseSkip.Execute(context);
            Assert(result == PhaseResult.Skip, $"Phase should skip, got {result}");
            Assert(!phaseSkip.Entered, "OnEnter should not be called for skipped phase");

            Console.WriteLine("  - PipelinePhaseBase: OK");
        }

        private static void TestTimelinePhaseMethod()
        {
            Console.WriteLine("[Test] TimelinePhase");

            var context = new TestCtx();
            var phase = new TestTimelinePhase();

            // Simulate timeline progression
            context.SetData("elapsed_ms", 0);
            var result = phase.Execute(context);
            Assert(result == PhaseResult.Pending, "Timeline should be pending at start");

            context.SetData("elapsed_ms", 100);
            result = phase.Execute(context);
            Assert(result == PhaseResult.Pending, "Timeline should be pending at 100ms");

            context.SetData("elapsed_ms", 500);
            result = phase.Execute(context);
            Assert(result == PhaseResult.Success, "Timeline should complete at 500ms");
            Assert(phase.EventsFired == 2, $"Should fire 2 events, fired {phase.EventsFired}");

            Console.WriteLine("  - TimelinePhase: OK");
        }

        private static void TestPipelineContext()
        {
            Console.WriteLine("[Test] PipelineContext");

            var context = new TestCtx();

            context.SetData("key1", "value1");
            context.SetData("key2", 42);

            Assert(context.GetData<string>("key1") == "value1", "String data should be accessible");
            Assert(context.GetData<int>("key2") == 42, "Int data should be accessible");
            Assert(context.HasData("key1"), "HasData should return true for existing key");
            Assert(!context.HasData("missing"), "HasData should return false for missing key");

            context.RemoveData("key1");
            Assert(!context.HasData("key1"), "Data should be removed after RemoveData");

            Console.WriteLine("  - PipelineContext: OK");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new Exception($"Assertion failed: {message}");
        }

        /// <summary>
        /// 测试用上下文。
        /// </summary>
        private class TestCtx : IPipelineContext
        {
            public int ContextId { get; } = 0;
            private readonly Dictionary<string, object> _data = new();

            public T GetData<T>(string key)
            {
                if (_data.TryGetValue(key, out var value) && value is T typed)
                    return typed;
                return default;
            }

            public void SetData<T>(string key, T value) => _data[key] = value;
            public bool HasData(string key) => _data.ContainsKey(key);
            public void RemoveData(string key) => _data.Remove(key);
        }

        /// <summary>
        /// 测试用成功阶段。
        /// </summary>
        private class TestSuccessPhase : PipelinePhaseBase
        {
            public override string PhaseId => "test_success";
            public bool Entered { get; private set; }
            public bool Completed { get; private set; }

            protected override void OnEnter(IPipelineContext context) => Entered = true;
            protected override PhaseResult OnExecute(IPipelineContext context) => PhaseResult.Success;
            protected override void OnComplete(IPipelineContext context) => Completed = true;
        }

        /// <summary>
        /// 测试用跳过阶段。
        /// </summary>
        private class TestSkipPhase : PipelinePhaseBase
        {
            public override string PhaseId => "test_skip";
            public bool Entered { get; private set; }

            protected override bool CanExecute(IPipelineContext context) => false;
            protected override void OnEnter(IPipelineContext context) => Entered = true;
            protected override PhaseResult OnExecute(IPipelineContext context) => PhaseResult.Success;
        }

        /// <summary>
        /// 测试用时间线阶段。
        /// </summary>
        private class TestTimelinePhase : TimelinePhaseBase
        {
            public override string PhaseId => "test_timeline";
            public int EventsFired { get; private set; }

            protected override int DurationMs => 500;

            protected override TimelineEvent[] GetTimelineEvents() => new[]
            {
                new TimelineEvent(100, "event1", null),
                new TimelineEvent(300, "event2", null)
            };

            protected override PhaseResult OnExecute(IPipelineContext context)
            {
                var elapsedMs = context.GetData<int>("elapsed_ms");
                return OnUpdate(context, elapsedMs);
            }

            protected override void OnTimelineEvent(IPipelineContext context, TimelineEvent e)
            {
                EventsFired++;
            }
        }
    }
}
