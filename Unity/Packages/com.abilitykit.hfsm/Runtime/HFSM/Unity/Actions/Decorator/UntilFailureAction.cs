using System;


namespace AbilityKit.HFSM.Actions
{

    /// <summary>
    /// 直到失败：重复执行直到失败
    /// </summary>
    [System.Serializable]
    public class UntilFailureAction : ActionBase, IDecoratorAction
    {
        public IAction child;

        public UntilFailureAction() { }

        public UntilFailureAction(IAction child)
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

            while (true)
            {
                var status = child.Execute(context);

                if (status == BehaviorStatus.Running)
                {
                    return BehaviorStatus.Running;
                }
                else if (status == BehaviorStatus.Failure)
                {
                    return BehaviorStatus.Failure;
                }

                // 成功，重置并重试
                child.Reset();
            }
        }
    }
}
