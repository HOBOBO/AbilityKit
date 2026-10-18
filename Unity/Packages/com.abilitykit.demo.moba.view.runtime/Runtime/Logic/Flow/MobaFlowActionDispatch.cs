using System;

namespace AbilityKit.Game.Flow
{
    internal readonly struct MobaFlowActionContext
    {
        public MobaFlowActionContext(IMobaFlowActionTarget target, int installedCount = 0)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
            InstalledCount = installedCount;
        }

        public IMobaFlowActionTarget Target { get; }
        public int InstalledCount { get; }
    }

    internal sealed class MobaFlowActionExecutor
    {
        public bool Execute(string actionId, in MobaFlowActionContext ctx)
        {
            if (string.IsNullOrEmpty(actionId))
            {
                return true;
            }

            switch (actionId)
            {
                case MobaFlowActionIds.ResetBattleSessionRuntimeState:
                    ctx.Target.ResetBattleSessionRuntimeState();
                    return true;
                case MobaFlowActionIds.ReturnLobbyAfterBattleEnd:
                    ctx.Target.ReturnLobbyAfterBattleEnd();
                    return true;
                default:
                    return false;
            }
        }
    }

    internal sealed class MobaFlowSwitchExecutor
    {
        public bool Execute(string switchFlowId, in MobaFlowActionContext ctx)
        {
            if (string.IsNullOrEmpty(switchFlowId))
            {
                return true;
            }

            switch (switchFlowId)
            {
                case MobaFlowSwitchIds.AdvanceOnConnectEnter:
                    ctx.Target.TryAdvanceOnConnectEnter();
                    return true;
                case MobaFlowSwitchIds.AdvanceOnCreateOrJoinWorldEnter:
                    ctx.Target.TryAdvanceOnCreateOrJoinWorldEnter();
                    return true;
                case MobaFlowSwitchIds.AdvanceOnLoadAssetsEnter:
                    ctx.Target.TryAdvanceOnLoadAssetsEnter();
                    return true;
                default:
                    return false;
            }
        }
    }
}
