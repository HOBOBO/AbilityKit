using System;
using System.Collections;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Actions
{

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
