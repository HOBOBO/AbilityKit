using System.Collections;
using UnityEngine;


namespace AbilityKit.HFSM.Actions
{

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
