using AbilityKit.Ability.Flow;
using AbilityKit.Ability.Flow.Blocks;

namespace AbilityKit.FlowExamples
{
    public static class BasicSessionExample
    {
        public static FlowStatus RunOnce(float deltaTime)
        {
            using var session = new FlowSession();

            var root = new DoNode(
                onEnter: _ => { },
                onTick: (_, __) => FlowStatus.Succeeded,
                onExit: _ => { },
                onInterrupt: _ => { }
            );

            session.Start(root);
            return session.Step(deltaTime);
        }
    }
}
