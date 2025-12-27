using AbilityKit.Ability.Flow;
using AbilityKit.Ability.Flow.Blocks;
using AbilityKit.Ability.Flow.Nodes;
using AbilityKit.Ability.Flow.Stages;
using AbilityKit.Ability.World.Management;

namespace AbilityKit.Ability.Impl.FlowExamples
{
    public sealed class BattleDungeonFlowProvider : IStagedFlowProvider<BattleDungeonFlowArgs>
    {
        public IFlowNode CreateStage(FlowStageKey stage, BattleDungeonFlowArgs args)
        {
            if (stage == FlowStages.Enter)
            {
                return new CreateResourceNode<IWorldManager>(
                    create: _ => BattleDungeonBootstrap.CreateWorlds(args.WorldId)
                );
            }

            if (stage == FlowStages.Running)
            {
                return new WaitSecondsEventNode(args.RunSeconds);
            }

            if (stage == FlowStages.Exit)
            {
                return new DisposeResourceNode<IWorldManager>(dispose: w => w.DisposeAll());
            }

            if (stage == FlowStages.PostExit)
            {
                return new DoNode(onEnter: c => c.Clear());
            }

            return null;
        }
    }
}
