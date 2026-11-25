namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// 伤害规则消费权威 Context 事实，保持与 Trace 独立。
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
