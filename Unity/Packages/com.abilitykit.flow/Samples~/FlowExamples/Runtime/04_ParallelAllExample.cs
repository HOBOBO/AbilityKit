using AbilityKit.Ability.Flow;
using AbilityKit.Ability.Flow.Blocks;

namespace AbilityKit.FlowExamples
{
    public static class ParallelAllExample
    {
        public static IFlowNode CreateAllSuccess()
        {
            var a = new DoNode(onTick: (_, __) => FlowStatus.Succeeded);
            var b = new DoNode(onTick: (_, __) => FlowStatus.Succeeded);
            return new ParallelAllNode(a, b);
        }

        public static IFlowNode CreateOneFail()
        {
            var a = new DoNode(onTick: (_, __) => FlowStatus.Succeeded);
            var b = new DoNode(onTick: (_, __) => FlowStatus.Failed);
            return new ParallelAllNode(a, b);
        }
    }
}
