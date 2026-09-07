using System.Collections.Generic;


namespace AbilityKit.HFSM.Actions
{

    /// <summary>
    /// 随机序列：随机顺序执行子行为
    /// </summary>
    [System.Serializable]
    public class RandomSequenceAction : ActionBase, ICompositeAction
    {
        public List<IAction> children = new List<IAction>();
        private List<IAction> shuffledChildren;
        private int currentIndex;

        public RandomSequenceAction() { }

        public RandomSequenceAction(List<IAction> children)
        {
            this.children = children;
        }

        public void AddChild(IAction child) => children.Add(child);

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
