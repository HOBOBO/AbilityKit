using System.Collections;
using UnityEngine;


namespace AbilityKit.HFSM.Actions
{
    /// <summary>
    /// 等待指定时间的行为
    /// </summary>
    [System.Serializable]
    public class WaitAction : YieldActionBase
    {
        public float duration = 1f;
        private float elapsed;

        public WaitAction() { }

        public WaitAction(float duration)
        {
            this.duration = duration;
        }

        public override void Reset()
        {
            base.Reset();
            elapsed = 0f;
        }

        protected override void OnStart(BehaviorContext context)
        {
            base.OnStart(context);
            elapsed = 0f;
        }

        protected override BehaviorStatus OnUpdate(BehaviorContext context)
        {
            elapsed += context.deltaTime;
            if (elapsed >= duration)
            {
                return BehaviorStatus.Success;
            }
            return BehaviorStatus.Running;
        }

        public override IEnumerator GetYieldEnumerator(BehaviorContext context)
        {
            elapsed = 0f;
            while (elapsed < duration)
            {
                yield return new WaitForSeconds(Mathf.Min(context.deltaTime, duration - elapsed));
                elapsed += context.deltaTime;
            }
        }
    }
}
