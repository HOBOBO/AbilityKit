using System.Collections.Generic;

namespace UnityHFSM.Actions
{
    /// <summary>
    /// 序列行为：顺序执行子行为，任一失败则整体失败，全部成功才返回成功
    /// </summary>
    [System.Serializable]
    public class SequenceAction : ActionBase
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

    /// <summary>
    /// 选择器行为：尝试执行子行为，任一成功则整体成功，全部失败才返回失败
    /// </summary>
    [System.Serializable]
    public class SelectorAction : ActionBase
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

    /// <summary>
    /// 并行行为：同时执行所有子行为
    /// </summary>
    [System.Serializable]
    public class ParallelAction : ActionBase
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

    /// <summary>
    /// 随机选择器：随机选择一个子行为执行
    /// </summary>
    [System.Serializable]
    public class RandomSelectorAction : ActionBase
    {
        public List<IAction> children = new List<IAction>();
        public List<float> weights = new List<float>();

        private IAction selectedChild;

        public RandomSelectorAction() { }

        public RandomSelectorAction(List<IAction> children, List<float> weights)
        {
            this.children = children;
            this.weights = weights;
        }

        public override void Reset()
        {
            base.Reset();
            selectedChild = null;
            foreach (var child in children)
            {
                child.Reset();
            }
        }

        public override void ForceEnd()
        {
            base.ForceEnd();
            selectedChild?.ForceEnd();
        }

        public override BehaviorStatus Execute(BehaviorContext context)
        {
            if (forceEnded)
                return BehaviorStatus.Failure;

            isActive = true;

            if (selectedChild == null)
            {
                selectedChild = SelectRandomChild();
                if (selectedChild == null)
                    return BehaviorStatus.Failure;
            }

            var status = selectedChild.Execute(context);

            if (status != BehaviorStatus.Running)
            {
                selectedChild = null;
            }

            return status;
        }

        private IAction SelectRandomChild()
        {
            if (children.Count == 0)
                return null;

            if (weights.Count < children.Count)
            {
                // 如果权重数量不足，使用等权重
                return children[UnityEngine.Random.Range(0, children.Count)];
            }

            float totalWeight = 0f;
            foreach (var w in weights)
            {
                totalWeight += w;
            }

            float randomValue = UnityEngine.Random.Range(0f, totalWeight);
            float currentWeight = 0f;

            for (int i = 0; i < children.Count; i++)
            {
                currentWeight += weights[i];
                if (randomValue <= currentWeight)
                {
                    return children[i];
                }
            }

            return children[children.Count - 1];
        }
    }

    /// <summary>
    /// 随机序列：随机顺序执行子行为
    /// </summary>
    [System.Serializable]
    public class RandomSequenceAction : ActionBase
    {
        public List<IAction> children = new List<IAction>();
        private List<IAction> shuffledChildren;
        private int currentIndex;

        public RandomSequenceAction() { }

        public RandomSequenceAction(List<IAction> children)
        {
            this.children = children;
        }

        public override void Reset()
        {
            base.Reset();
            currentIndex = 0;

            // 创建并打乱副本
            shuffledChildren = new List<IAction>(children);
            Shuffle(shuffledChildren);

            foreach (var child in shuffledChildren)
            {
                child.Reset();
            }
        }

        public override void ForceEnd()
        {
            base.ForceEnd();
            if (shuffledChildren != null)
            {
                for (int i = currentIndex; i < shuffledChildren.Count; i++)
                {
                    shuffledChildren[i].ForceEnd();
                }
            }
        }

        public override BehaviorStatus Execute(BehaviorContext context)
        {
            if (forceEnded)
                return BehaviorStatus.Failure;

            isActive = true;

            if (shuffledChildren == null || shuffledChildren.Count == 0)
                return BehaviorStatus.Success;

            while (currentIndex < shuffledChildren.Count)
            {
                var status = shuffledChildren[currentIndex].Execute(context);

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

        private void Shuffle(List<IAction> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                var temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }
    }
}
