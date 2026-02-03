using System;
using AbilityKit.Ability.Flow;
using AbilityKit.Ability.Flow.Blocks;

namespace AbilityKit.FlowExamples
{
    public static class ExceptionHandlingExample
    {
        public static void RunAndCatch()
        {
            using var session = new FlowSession();

            Exception captured = null;
            session.UnhandledException += ex => captured = ex;

            var root = new DoNode(
                onTick: (_, __) => throw new InvalidOperationException("boom")
            );

            session.Start(root);

            // Start 内部会 prime 一次 Step(0)，这里异常会被捕获并通过事件上报。
            _ = session.Status;
            _ = captured;
        }
    }
}
