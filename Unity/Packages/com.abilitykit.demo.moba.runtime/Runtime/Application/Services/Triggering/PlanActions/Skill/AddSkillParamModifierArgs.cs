using AbilityKit.Modifiers;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public readonly struct AddSkillParamModifierArgs
    {
        public AddSkillParamModifierArgs(
            int parameterId,
            ModifierOp operation,
            float value,
            int sourceId,
            int priority,
            in MobaActionTargetRequest targetRequest)
        {
            ParameterId = parameterId;
            Operation = operation;
            Value = value;
            SourceId = sourceId;
            Priority = priority;
            TargetRequest = targetRequest;
        }

        public int ParameterId { get; }
        public ModifierOp Operation { get; }
        public float Value { get; }
        public int SourceId { get; }
        public int Priority { get; }
        public MobaActionTargetRequest TargetRequest { get; }
    }
}
