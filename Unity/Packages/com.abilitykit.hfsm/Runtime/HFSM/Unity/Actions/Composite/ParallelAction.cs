using System.Collections.Generic;


namespace AbilityKit.HFSM.Actions
{

    /// <summary>
    /// 并行行为：同时执行所有子行为
    /// </summary>
    [System.Serializable]
    public class ParallelAction : ActionBase, ICompositeAction
    {
        public List<IAction> children = new List<IAction>();
        public bool failOnAnyFailure = false;
        public bool successOnAllSuccess = true;

        private bool[] childStatuses;

        public ParallelAction() { }

        public ParallelAction(List<IAction> children, bool failOnAnyFailure = false)
        {
            this.children = children;
            this.failOnAnyFailure = failOnAnyFailure;
        }

        public void AddChild(IAction child) => children.Add(child);

        public override void Reset()
        {
            base.Reset();
            childStatuses = new bool[children.Count];
            for (int i = 0; i < children.Count; i++)
            {
                childStatuses[i] = false;
                children[i].Reset();
            }
        }

        public override void ForceEnd()
        {
            base.ForceEnd();
            foreach (var child in children)
            {
                child.ForceEnd();
            }
        }

        public override BehaviorStatus Execute(BehaviorContext context)
        {
            if (forceEnded)
                return BehaviorStatus.Failure;

            isActive = true;

            int successCount = 0;
            int failureCount = 0;

            for (int i = 0; i < children.Count; i++)
            {
                if (childStatuses[i])
                    continue;

                var status = children[i].Execute(context);

                if (status == BehaviorStatus.Running)
                {
                    continue;
                }
                else if (status == BehaviorStatus.Success)
                {
                    childStatuses[i] = true;
                    successCount++;
                }
                else if (status == BehaviorStatus.Failure)
                {
                    if (failOnAnyFailure)
                    {
                        ResetChildStatuses();
                        return BehaviorStatus.Failure;
                    }
                    childStatuses[i] = true;
                    failureCount++;
                }
            }

            if (successCount + failureCount >= children.Count)
            {
                ResetChildStatuses();
                return successCount == children.Count ? BehaviorStatus.Success : BehaviorStatus.Failure;
            }

            return BehaviorStatus.Running;
        }

        private void ResetChildStatuses()
        {
            for (int i = 0; i < childStatuses.Length; i++)
            {
                childStatuses[i] = false;
            }
        }
    }
}
