using System.Collections.Generic;


namespace AbilityKit.HFSM.Actions
{
    /// <summary>
    /// 序列行为：顺序执行子行为，任一失败则整体失败，全部成功才返回成功
    /// </summary>
    [System.Serializable]
    public class SequenceAction : ActionBase, ICompositeAction
    {
        public List<IAction> children = new List<IAction>();
        private int currentIndex;

        public SequenceAction() { }

        public SequenceAction(params IAction[] children)
        {
            this.children.AddRange(children);
        }

        public SequenceAction(List<IAction> children)
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
                else if (status == BehaviorStatus.Failure)
                {
                    currentIndex = 0;
                    return BehaviorStatus.Failure;
                }

                currentIndex++;
            }

            currentIndex = 0;
            return BehaviorStatus.Success;
        }
    }
}
