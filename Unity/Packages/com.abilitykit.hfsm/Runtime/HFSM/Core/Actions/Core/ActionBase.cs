using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityHFSM.Actions
{
    /// <summary>
    /// 行为基类，提供通用功能
    /// </summary>
    public abstract class ActionBase : IAction
    {
        public string Name { get; set; }
        public string Description { get; set; }

        protected bool isActive;
        protected bool forceEnded;

        public abstract BehaviorStatus Execute(BehaviorContext context);

        public virtual void Reset()
        {
            isActive = false;
            forceEnded = false;
        }

        public virtual void ForceEnd()
        {
            forceEnded = true;
            isActive = false;
            OnForceEnd();
        }

        /// <summary>
        /// 强制终止时的回调，子类可重写
        /// </summary>
        protected virtual void OnForceEnd() { }
    }

    /// <summary>
    /// 带协程支持的行为基类
    /// </summary>
    public abstract class YieldActionBase : ActionBase, IYieldAction
    {
        public bool IsWaitingForCoroutine { get; protected set; }

        public abstract IEnumerator GetYieldEnumerator(BehaviorContext context);

        public override BehaviorStatus Execute(BehaviorContext context)
        {
            if (forceEnded)
                return BehaviorStatus.Failure;

            if (!isActive)
            {
                isActive = true;
                OnStart(context);
            }

            return OnUpdate(context);
        }

        public override void Reset()
        {
            base.Reset();
            IsWaitingForCoroutine = false;
            OnReset();
        }

        /// <summary>
        /// 行为开始时的回调
        /// </summary>
        protected virtual void OnStart(BehaviorContext context) { }

        /// <summary>
        /// 每帧更新，返回执行状态
        /// </summary>
        protected virtual BehaviorStatus OnUpdate(BehaviorContext context)
        {
            return BehaviorStatus.Success;
        }

        /// <summary>
        /// 重置时的回调
        /// </summary>
        protected virtual void OnReset() { }
    }
}
