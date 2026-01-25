using AbilityKit.Ability.Flow;
using AbilityKit.Ability.Flow.Blocks;
using AbilityKit.Ability.Flow.Stages;
using UnityEngine;

namespace AbilityKit.Ability.Impl.FlowExamples
{
    public sealed class BattleDungeonStageLogContributor : IFlowStageContributor<BattleDungeonFlowArgs>
    {
        public int Order => 0;

        public bool CanContribute(FlowStageKey stage)
        {
            return stage == FlowStages.PreEnter || stage == FlowStages.PostExit;
        }

        public IFlowNode CreateNode(FlowStageKey stage, BattleDungeonFlowArgs args)
        {
            if (stage == FlowStages.PreEnter)
            {
                return new DoNode(onEnter: _ => Debug.Log($"[BattleDungeon] PreEnter world={args.WorldId} runSeconds={args.RunSeconds}"));
            }

            if (stage == FlowStages.PostExit)
            {
                return new DoNode(onEnter: _ => Debug.Log("[BattleDungeon] PostExit"));
            }

            return null;
        }
    }
}
