using System;


namespace AbilityKit.HFSM.Actions
{

    /// <summary>
    /// 反转器：反转子行为的结果（成功变失败，失败变成成功）
    /// </summary>
    [System.Serializable]
    public class InvertAction : ActionBase, IDecoratorAction
    {
        public IAction child;

        public InvertAction() { }

        public InvertAction(IAction child)
        {
            this.child = child;
        }

        public void SetChild(IAction value) => child = value;

        public override void Reset()
        {
            base.Reset();
            child?.Reset();
        }

        public override void ForceEnd()
        {
            base.ForceEnd();
            child?.ForceEnd();
        }

        public override BehaviorStatus Execute(BehaviorContext context)
        {
            if (forceEnded)
                return BehaviorStatus.Failure;

            isActive = true;

            if (child == null)
                return BehaviorStatus.Success;

            var status = child.Execute(context);

            switch (status)
            {
                case BehaviorStatus.Success:
                    return BehaviorStatus.Failure;
                case BehaviorStatus.Failure:
                    return BehaviorStatus.Success;
                default:
                    return status;
            }
        }
    }
}
