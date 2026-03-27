using System.Collections;
using UnityEngine;

namespace UnityHFSM.Actions
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

    /// <summary>
    /// 日志行为
    /// </summary>
    [System.Serializable]
    public class LogAction : ActionBase
    {
        public string message = "";
        public bool logToConsole = true;

        public LogAction() { }

        public LogAction(string message)
        {
            this.message = message;
        }

        public override BehaviorStatus Execute(BehaviorContext context)
        {
            if (forceEnded)
                return BehaviorStatus.Failure;

            if (logToConsole)
            {
                Debug.Log($"[Behavior] {message}");
            }
            context.onLog?.Invoke(message);
            return BehaviorStatus.Success;
        }
    }

    /// <summary>
    /// 设置浮点变量
    /// </summary>
    [System.Serializable]
    public class SetFloatAction : ActionBase
    {
        public string variableName = "";
        public float value = 0f;

        public SetFloatAction() { }

        public SetFloatAction(string variableName, float value)
        {
            this.variableName = variableName;
            this.value = value;
        }

        public override BehaviorStatus Execute(BehaviorContext context)
        {
            if (forceEnded)
                return BehaviorStatus.Failure;

            context.SetVariable(variableName, value);
            return BehaviorStatus.Success;
        }
    }

    /// <summary>
    /// 设置布尔变量
    /// </summary>
    [System.Serializable]
    public class SetBoolAction : ActionBase
    {
        public string variableName = "";
        public bool value = false;

        public SetBoolAction() { }

        public SetBoolAction(string variableName, bool value)
        {
            this.variableName = variableName;
            this.value = value;
        }

        public override BehaviorStatus Execute(BehaviorContext context)
        {
            if (forceEnded)
                return BehaviorStatus.Failure;

            context.SetVariable(variableName, value);
            return BehaviorStatus.Success;
        }
    }

    /// <summary>
    /// 设置整数变量
    /// </summary>
    [System.Serializable]
    public class SetIntAction : ActionBase
    {
        public string variableName = "";
        public int value = 0;

        public SetIntAction() { }

        public SetIntAction(string variableName, int value)
        {
            this.variableName = variableName;
            this.value = value;
        }

        public override BehaviorStatus Execute(BehaviorContext context)
        {
            if (forceEnded)
                return BehaviorStatus.Failure;

            context.SetVariable(variableName, value);
            return BehaviorStatus.Success;
        }
    }
}
