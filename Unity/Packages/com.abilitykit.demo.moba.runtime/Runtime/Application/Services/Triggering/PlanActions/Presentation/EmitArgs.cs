namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    /// <summary>
    /// Typed arguments for the template DSL emit action.
    /// </summary>
    [GenerateMobaPlanActionSchema(AbilityKit.Demo.Moba.Systems.TriggeringConstants.Actions.Emit)]
    public readonly struct EmitArgs
    {
        public readonly int EmitterId;

        public EmitArgs(
            [MobaPlanActionArg(MobaPlanActionArgKind.Int, 0d, true, "emitter_id", "emitterid", "emitterId", DisplayName = "emitterId", Min = 1d)] int emitterId)
        {
            EmitterId = emitterId;
        }
    }
}
