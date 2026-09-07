using System.Collections.Generic;


namespace AbilityKit.HFSM.Actions
{

    /// <summary>
    /// 随机选择器：随机选择一个子行为执行
    /// </summary>
    [System.Serializable]
    public class RandomSelectorAction : ActionBase, ICompositeAction
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

        public void AddChild(IAction child) => children.Add(child);

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
}
