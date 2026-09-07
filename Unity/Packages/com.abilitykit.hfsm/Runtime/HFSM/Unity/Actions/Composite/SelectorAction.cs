using System.Collections.Generic;


namespace AbilityKit.HFSM.Actions
{

    /// <summary>
    /// 选择器行为：尝试执行子行为，任一成功则整体成功，全部失败才返回失败
    /// </summary>
    [System.Serializable]
    public class SelectorAction : ActionBase, ICompositeAction
    {
        public List<IAction> children = new List<IAction>();
        private int currentIndex;

        public SelectorAction() { }

        public SelectorAction(params IAction[] children)
        {
            this.children.AddRange(children);
        }

        public SelectorAction(List<IAction> children)
        {
            this.children = children;
        }

        public void AddChild(IAction child) => children.Add(child);

        public override void Reset()
        {
            base.Reset();
            currentIndex = 0;
            foreach (var child in children)
            {
                child.Reset();
            }
        }

        public override void ForceEnd()
        {
            base.ForceEnd();
            for (int i = currentIndex; i < children.Count; i++)
            {
                children[i].ForceEnd();
            }
        }

        public override BehaviorStatus Execute(BehaviorContext context)
        {
            if (forceEnded)
                return BehaviorStatus.Failure;

            isActive = true;

            while (currentIndex < children.Count)
            {
                var status = children[currentIndex].Execute(context);

                if (status == BehaviorStatus.Running)
                {
                    return BehaviorStatus.Running;
                }
                else if (status == BehaviorStatus.Success)
                {
                    currentIndex = 0;
                    return BehaviorStatus.Success;
                }

                currentIndex++;
            }

            currentIndex = 0;
            return BehaviorStatus.Failure;
        }
    }
}
