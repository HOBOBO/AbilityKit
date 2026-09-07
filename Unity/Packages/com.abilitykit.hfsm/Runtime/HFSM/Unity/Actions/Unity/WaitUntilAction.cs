using System.Collections;
using UnityEngine;


namespace AbilityKit.HFSM.Actions
{

    /// <summary>
    /// 等待条件满足的行为
    /// </summary>
    [System.Serializable]
    public class WaitUntilAction : YieldActionBase
    {
        public System.Func<bool> condition;

        public WaitUntilAction() { }

        public WaitUntilAction(System.Func<bool> condition)
        {
            this.condition = condition;
        }

        protected override BehaviorStatus OnUpdate(BehaviorContext context)
        {
            if (condition == null)
                return BehaviorStatus.Success;

            if (condition())
                return BehaviorStatus.Success;

            return BehaviorStatus.Running;
        }

        public override IEnumerator GetYieldEnumerator(BehaviorContext context)
        {
            while (condition != null && !condition())
            {
                yield return null;
            }
        }
    }
}
