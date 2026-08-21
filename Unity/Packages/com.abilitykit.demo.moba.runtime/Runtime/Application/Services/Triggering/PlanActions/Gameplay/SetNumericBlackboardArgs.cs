using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public readonly struct SetNumericBlackboardArgs
    {
        public readonly BlackboardWriteTarget Target;
        public readonly double Value;

        public SetNumericBlackboardArgs(in BlackboardWriteTarget target, double value)
        {
            Target = target;
            Value = value;
        }
    }
}
