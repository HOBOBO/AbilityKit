using AbilityKit.Ability.Flow;
using AbilityKit.Ability.Flow.Blocks;

namespace AbilityKit.FlowExamples
{
    public static class TimeoutAndRaceExample
    {
        public static IFlowNode Create()
        {
            // A：永远 Running（依赖外部事件才会完成）
            var neverDone = new DoNode(onTick: (_, __) => FlowStatus.Running);

            // B：立刻成功
            var immediate = new DoNode(onTick: (_, __) => FlowStatus.Succeeded);

            // Race：B 会先完成，并中断 A
            var race = new RaceNode(neverDone, immediate);

            // Timeout：给 Race 加超时（这里只是示例，race 会立刻结束，不会触发超时）
            return new TimeoutNode(3f, race);
        }
    }
}
