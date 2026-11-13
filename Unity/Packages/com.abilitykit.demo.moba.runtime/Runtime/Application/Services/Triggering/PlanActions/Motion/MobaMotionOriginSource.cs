namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    /// <summary>Explicit coordinate source for radial displacement. Zero preserves existing caster semantics.</summary>
    public enum MobaMotionOriginSource
    {
        CasterPosition = 0,
        FrozenAimPosition = 1,
        ContextPoint = 2,
    }
}
