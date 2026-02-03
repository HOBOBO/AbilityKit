using System;
using AbilityKit.Ability.Flow;
using AbilityKit.Ability.Flow.Blocks;

namespace AbilityKit.FlowExamples
{
    public static class UsingResourceAndScopeExample
    {
        private sealed class DummyResource
        {
            public int Value;
        }

        public static IFlowNode Create()
        {
            return new UsingResourceNode<DummyResource>(
                create: _ => new DummyResource { Value = 42 },
                dispose: _ => { },
                body: new DoNode(
                    onEnter: ctx =>
                    {
                        // UsingResourceNode 会把资源注入到 context
                        var res = ctx.Get<DummyResource>();
                        res.Value += 1;
                    },
                    onTick: (_, __) => FlowStatus.Succeeded
                )
            );
        }

        public static void DemonstrateManualScope(FlowContext ctx)
        {
            using (ctx.BeginScope())
            {
                ctx.Set(new DummyResource { Value = 1 });
                _ = ctx.Get<DummyResource>();
            }

            // scope 结束后，局部注入会被回收；如果没有全局注入，这里会抛异常/获取失败。
        }
    }
}
