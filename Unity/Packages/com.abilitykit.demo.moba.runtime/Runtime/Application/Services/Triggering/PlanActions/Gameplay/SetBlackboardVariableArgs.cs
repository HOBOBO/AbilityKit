using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public readonly struct SetBlackboardVariableArgs
    {
        public readonly BlackboardWriteTarget Target;
        public readonly ActionArgValue Value;

        public SetBlackboardVariableArgs(in BlackboardWriteTarget target, in ActionArgValue value)
        {
            Target = target;
            Value = value;
        }
    }
}
