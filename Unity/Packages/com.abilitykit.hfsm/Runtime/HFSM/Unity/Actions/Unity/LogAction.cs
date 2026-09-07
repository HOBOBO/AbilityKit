using System.Collections;
using UnityEngine;


namespace AbilityKit.HFSM.Actions
{

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
}
