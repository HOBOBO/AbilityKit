using System;


namespace AbilityKit.HFSM.Actions
{

    /// <summary>
    /// 条件逆变器：如果条件满足则执行成功，否则执行子行为
    /// </summary>
    [System.Serializable]
    public class ConditionalAbortAction : ActionBase, IDecoratorAction
    {
        public Func<bool> condition;
        public IAction child;

        public ConditionalAbortAction() { }

        public ConditionalAbortAction(Func<bool> condition, IAction child)
        {
            this.condition = condition;
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

            // 如果条件满足，立即返回成功
            if (condition != null && condition())
            {
                return BehaviorStatus.Success;
            }

            // 否则执行子行为
            if (child != null)
            {
                return child.Execute(context);
            }

            return BehaviorStatus.Failure;
        }
    }
}
