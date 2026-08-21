using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public readonly struct AddNumericBlackboardArgs
    {
        public readonly BlackboardWriteTarget Target;
        public readonly double Value;

        public AddNumericBlackboardArgs(in BlackboardWriteTarget target, double value)
        {
            Target = target;
            Value = value;
        }
    }
}
