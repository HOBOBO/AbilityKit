using System;
using AbilityKit.Ability.Flow;
using AbilityKit.Ability.Flow.Nodes;

namespace AbilityKit.FlowExamples
{
    public static class WakePumpExample
    {
        private sealed class ManualCompletion
        {
            public Action<bool> Complete;
        }

        public static void StartAndCompleteSynchronously()
        {
            using var session = new FlowSession();

            var manual = new ManualCompletion();
            session.Context.Set(manual);

            var node = new AwaitCallbackNode(
                subscribe: (ctx, complete) =>
                {
                    var m = ctx.Get<ManualCompletion>();
                    m.Complete = complete;
                    return null;
                }
            );

            session.Start(node);

            // 模拟：回调在同线程触发，并调用 Wake() 推进。
            manual.Complete?.Invoke(true);

            // 如果外部不持续 Step，依然可以靠 Wake/Pump 推进；
            // 但为了获取最终状态，仍可以再 Step 一次。
            session.Step(0f);
        }
    }
}
