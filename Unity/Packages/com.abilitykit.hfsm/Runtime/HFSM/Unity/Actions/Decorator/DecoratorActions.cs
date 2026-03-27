using System;
using UnityHFSM.Config;

namespace UnityHFSM.Actions
{
    /// <summary>
    /// 重复执行器：重复执行子行为指定次数，-1 表示无限重复
    /// </summary>
    [System.Serializable]
    [HfsmActionType("Repeat", "重复", "重复执行子行为指定次数", "装饰器")]
    public class RepeatAction : ActionBase
    {
        public IAction child;
        public int count = -1; // -1 表示无限重复

        private int currentCount;

        public RepeatAction() { }

        public RepeatAction(IAction child, int count = -1)
        {
            this.child = child;
            this.count = count;
        }

        public override void Reset()
        {
            base.Reset();
            currentCount = 0;
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

            while (true)
            {
                // 检查是否达到重复次数
                if (count >= 0 && currentCount >= count)
                {
                    currentCount = 0;
                    return BehaviorStatus.Success;
                }

                var status = child.Execute(context);

                if (status == BehaviorStatus.Running)
                {
                    return BehaviorStatus.Running;
                }

                currentCount++;

                // 如果子行为失败
                if (status == BehaviorStatus.Failure)
                {
                    currentCount = 0;
                    return BehaviorStatus.Failure;
                }

                // 子行为成功，重置并继续
                child.Reset();
            }
        }
    }

    /// <summary>
    /// 反转器：反转子行为的结果（成功变失败，失败变成成功）
    /// </summary>
    [System.Serializable]
    [HfsmActionType("Invert", "反转", "反转子行为的结果", "装饰器")]
    public class InvertAction : ActionBase
    {
        public IAction child;

        public InvertAction() { }

        public InvertAction(IAction child)
        {
            this.child = child;
        }

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

            if (child == null)
                return BehaviorStatus.Success;

            var status = child.Execute(context);

            switch (status)
            {
                case BehaviorStatus.Success:
                    return BehaviorStatus.Failure;
                case BehaviorStatus.Failure:
                    return BehaviorStatus.Success;
                default:
                    return status;
            }
        }
    }

    /// <summary>
    /// 时间限制器：限制子行为的最大执行时间
    /// </summary>
    [System.Serializable]
    [HfsmActionType("TimeLimit", "时间限制", "限制子行为的最大执行时间", "装饰器")]
    public class TimeLimitAction : ActionBase
    {
        public IAction child;
        public float timeLimit = 5f;

        private float elapsed;

        public TimeLimitAction() { }

        public TimeLimitAction(IAction child, float timeLimit)
        {
            this.child = child;
            this.timeLimit = timeLimit;
        }

        public override void Reset()
        {
            base.Reset();
            elapsed = 0f;
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

            // 检查时间限制
            elapsed += context.deltaTime;
            if (elapsed >= timeLimit)
            {
                child.ForceEnd();
                return BehaviorStatus.Failure;
            }

            var status = child.Execute(context);

            // 如果子行为完成，时间限制也完成
            if (status != BehaviorStatus.Running)
            {
                elapsed = 0f;
                return status;
            }

            return BehaviorStatus.Running;
        }
    }

    /// <summary>
    /// 直到成功：重复执行直到成功
    /// </summary>
    [System.Serializable]
    [HfsmActionType("UntilSuccess", "直到成功", "重复执行直到成功", "装饰器")]
    public class UntilSuccessAction : ActionBase
    {
        public IAction child;

        public UntilSuccessAction() { }

        public UntilSuccessAction(IAction child)
        {
            this.child = child;
        }

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

            if (child == null)
                return BehaviorStatus.Success;

            while (true)
            {
                var status = child.Execute(context);

                if (status == BehaviorStatus.Running)
                {
                    return BehaviorStatus.Running;
                }
                else if (status == BehaviorStatus.Success)
                {
                    return BehaviorStatus.Success;
                }

                // 失败，重置并重试
                child.Reset();
            }
        }
    }

    /// <summary>
    /// 直到失败：重复执行直到失败
    /// </summary>
    [System.Serializable]
    public class UntilFailureAction : ActionBase
    {
        public IAction child;

        public UntilFailureAction() { }

        public UntilFailureAction(IAction child)
        {
            this.child = child;
        }

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

            if (child == null)
                return BehaviorStatus.Success;

            while (true)
            {
                var status = child.Execute(context);

                if (status == BehaviorStatus.Running)
                {
                    return BehaviorStatus.Running;
                }
                else if (status == BehaviorStatus.Failure)
                {
                    return BehaviorStatus.Failure;
                }

                // 成功，重置并重试
                child.Reset();
            }
        }
    }

    /// <summary>
    /// 冷却时间：限制子行为的执行频率
    /// </summary>
    [System.Serializable]
    public class CooldownAction : ActionBase
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

    /// <summary>
    /// 条件执行：如果条件满足则执行 thenAction，否则执行 elseAction（可选）
    /// </summary>
    [System.Serializable]
    public class IfAction : ActionBase
    {
        public Func<bool> condition;
        public IAction thenAction;
        public IAction elseAction;

        private bool evaluated;

        public IfAction() { }

        public IfAction(Func<bool> condition, IAction thenAction, IAction elseAction = null)
        {
            this.condition = condition;
            this.thenAction = thenAction;
            this.elseAction = elseAction;
        }

        public override void Reset()
        {
            base.Reset();
            evaluated = false;
            thenAction?.Reset();
            elseAction?.Reset();
        }

        public override void ForceEnd()
        {
            base.ForceEnd();
            thenAction?.ForceEnd();
            elseAction?.ForceEnd();
        }

        public override BehaviorStatus Execute(BehaviorContext context)
        {
            if (forceEnded)
                return BehaviorStatus.Failure;

            isActive = true;

            if (!evaluated)
            {
                evaluated = true;
                if (condition != null && condition())
                {
                    thenAction?.Reset();
                }
                else
                {
                    elseAction?.Reset();
                }
            }

            // 执行选定的分支
            if (condition != null && condition())
            {
                if (thenAction != null)
                {
                    var status = thenAction.Execute(context);
                    if (status != BehaviorStatus.Running)
                    {
                        evaluated = false;
                    }
                    return status;
                }
            }
            else
            {
                if (elseAction != null)
                {
                    var status = elseAction.Execute(context);
                    if (status != BehaviorStatus.Running)
                    {
                        evaluated = false;
                    }
                    return status;
                }
            }

            evaluated = false;
            return BehaviorStatus.Success;
        }
    }

    /// <summary>
    /// 条件逆变器：如果条件满足则执行成功，否则执行子行为
    /// </summary>
    [System.Serializable]
    public class ConditionalAbortAction : ActionBase
    {
        public Func<bool> condition;
        public IAction child;

        public ConditionalAbortAction() { }

        public ConditionalAbortAction(Func<bool> condition, IAction child)
        {
            this.condition = condition;
            this.child = child;
        }

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
