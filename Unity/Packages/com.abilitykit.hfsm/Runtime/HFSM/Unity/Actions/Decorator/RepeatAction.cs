using System;


namespace AbilityKit.HFSM.Actions
{
    /// <summary>
    /// 重复执行器：重复执行子行为指定次数，-1 表示无限重复
    /// </summary>
    [System.Serializable]
    public class RepeatAction : ActionBase, IDecoratorAction
    {
        public IAction child;
        public int count = -1; // -1 表示无限重复

        private int currentCount;

        public RepeatAction() { }

        public RepeatAction(IAction child, int count = -1)
        {
            this.child = child;
            this.count = count;
        }

        public void SetChild(IAction value) => child = value;

        public override void Reset()
        {
            base.Reset();
            currentCount = 0;
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
                // 检查是否达到重复次数
                if (count >= 0 && currentCount >= count)
                {
                    currentCount = 0;
                    return BehaviorStatus.Success;
                }

                var status = child.Execute(context);

                if (status == BehaviorStatus.Running)
                {
                    return BehaviorStatus.Running;
                }

                currentCount++;

                // 如果子行为失败
                if (status == BehaviorStatus.Failure)
                {
                    currentCount = 0;
                    return BehaviorStatus.Failure;
                }

                // 子行为成功，重置并继续
                child.Reset();
            }
        }
    }
}
