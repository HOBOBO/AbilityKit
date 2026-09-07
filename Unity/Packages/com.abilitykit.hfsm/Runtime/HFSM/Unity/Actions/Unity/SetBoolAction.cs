using System.Collections;
using UnityEngine;


namespace AbilityKit.HFSM.Actions
{

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
}
