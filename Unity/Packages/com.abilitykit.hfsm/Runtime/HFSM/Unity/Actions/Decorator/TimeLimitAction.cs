using System;


namespace AbilityKit.HFSM.Actions
{

    /// <summary>
    /// 时间限制器：限制子行为的最大执行时间
    /// </summary>
    [System.Serializable]
    public class TimeLimitAction : ActionBase, IDecoratorAction
    {
        public IAction child;
        public float timeLimit = 5f;

        private float elapsed;

        public TimeLimitAction() { }

        public TimeLimitAction(IAction child, float timeLimit)
        {
            this.child = child;
            this.timeLimit = timeLimit;
        }

        public void SetChild(IAction value) => child = value;

        public override void Reset()
        {
            base.Reset();
            elapsed = 0f;
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

            // 检查时间限制
            elapsed += context.deltaTime;
            if (elapsed >= timeLimit)
            {
                child.ForceEnd();
                return BehaviorStatus.Failure;
            }

            var status = child.Execute(context);

            // 如果子行为完成，时间限制也完成
            if (status != BehaviorStatus.Running)
            {
                elapsed = 0f;
                return status;
            }

            return BehaviorStatus.Running;
        }
    }
}
