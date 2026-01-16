using AbilityKit.Ability.Triggering.Blackboard;

namespace AbilityKit.Ability.Triggering.Variables.Numeric.Domains
{
    public static class DefaultBlackboardNumericVarDomains
    {
        public static BlackboardNumericVarDomain CreateDefault()
        {
            return new BlackboardNumericVarDomain(BlackboardIds.Default, BlackboardIds.Default);
        }

        public static BlackboardNumericVarDomain CreateActor()
        {
            return new BlackboardNumericVarDomain(BlackboardIds.Actor, BlackboardIds.Actor);
        }

        public static BlackboardNumericVarDomain CreateSkill()
        {
            return new BlackboardNumericVarDomain(BlackboardIds.Skill, BlackboardIds.Skill);
        }

        public static BlackboardNumericVarDomain CreateEffect()
        {
            return new BlackboardNumericVarDomain(BlackboardIds.Effect, BlackboardIds.Effect);
        }

        public static BlackboardNumericVarDomain CreateProjectile()
        {
            return new BlackboardNumericVarDomain(BlackboardIds.Projectile, BlackboardIds.Projectile);
        }

        public static BlackboardNumericVarDomain CreateBattle()
        {
            return new BlackboardNumericVarDomain(BlackboardIds.Battle, BlackboardIds.Battle);
        }

        public static BlackboardNumericVarDomain CreateGlobal()
        {
            return new BlackboardNumericVarDomain(BlackboardIds.Global, BlackboardIds.Global);
        }
    }
}
