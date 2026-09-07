using System;


namespace AbilityKit.HFSM.Actions
{

    /// <summary>
    /// 冷却时间：限制子行为的执行频率
    /// </summary>
    [System.Serializable]
    public class CooldownAction : ActionBase, IDecoratorAction
    {
        public IAction child;
        public float cooldownDuration = 1f;

        private float cooldownTimer;
        private bool isInCooldown;

        public CooldownAction() { }

        public CooldownAction(IAction child, float cooldownDuration)
        {
            this.child = child;
            this.cooldownDuration = cooldownDuration;
        }

        public void SetChild(IAction value) => child = value;

        public override void Reset()
        {
            base.Reset();
            cooldownTimer = 0f;
            isInCooldown = false;
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

            // 处理冷却
            if (isInCooldown)
            {
                cooldownTimer -= context.deltaTime;
                if (cooldownTimer <= 0f)
                {
                    isInCooldown = false;
                    cooldownTimer = 0f;
                    child.Reset();
                }
                else
                {
                    return BehaviorStatus.Running;
                }
            }

            var status = child.Execute(context);

            if (status != BehaviorStatus.Running)
            {
                // 子行为完成，开始冷却
                if (status == BehaviorStatus.Success)
                {
                    isInCooldown = true;
                    cooldownTimer = cooldownDuration;
                }
                return status;
            }

            return BehaviorStatus.Running;
        }
    }
}
