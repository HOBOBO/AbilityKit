using System;
using System.Collections;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Actions
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
}
