using System.Collections;
using UnityEngine;


namespace AbilityKit.HFSM.Actions
{

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
}
