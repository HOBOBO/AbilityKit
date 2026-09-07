using System;


namespace AbilityKit.HFSM.Actions
{

    /// <summary>
    /// 直到成功：重复执行直到成功
    /// </summary>
    [System.Serializable]
    public class UntilSuccessAction : ActionBase, IDecoratorAction
    {
        public IAction child;

        public UntilSuccessAction() { }

        public UntilSuccessAction(IAction child)
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
                else if (status == BehaviorStatus.Success)
                {
                    return BehaviorStatus.Success;
                }

                // 失败，重置并重试
                child.Reset();
            }
        }
    }
}
