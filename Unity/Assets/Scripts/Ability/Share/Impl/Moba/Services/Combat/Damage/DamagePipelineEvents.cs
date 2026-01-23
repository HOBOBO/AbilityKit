namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public static class DamagePipelineEvents
    {
        public const string AttackCreated = "damage.attack.created";
        public const string BeforeCalc = "damage.attack.before_calc";

        public const string CalcBegin = "damage.calc.begin";
        public const string AfterBase = "damage.calc.after_base";
        public const string AfterMitigate = "damage.calc.after_mitigate";
        public const string AfterShield = "damage.calc.after_shield";
        public const string CalcFinal = "damage.calc.final";

        public const string BeforeApply = "damage.apply.before";
        public const string AfterApply = "damage.apply.after";
    }
}
