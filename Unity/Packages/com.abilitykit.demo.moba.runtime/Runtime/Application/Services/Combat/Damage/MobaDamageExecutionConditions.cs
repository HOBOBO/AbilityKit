namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// Damage rules consume authoritative Context facts and remain independent from Trace.
    /// </summary>
    public static class MobaDamageExecutionConditions
    {
        public static bool CanTriggerReflection(in MobaCombatExecutionContext context)
        {
            return context.CombatFacts.CanTriggerReflection;
        }

        public static MobaCombatExecutionFacts CreateReflectionFacts(in MobaCombatExecutionContext context)
        {
            return context.CombatFacts.AsReflectedDamage();
        }
    }
}
