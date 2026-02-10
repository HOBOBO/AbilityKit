namespace AbilityKit.Ability.Share.Impl.Moba.Systems
{
    public static class MobaSystemOrder
    {
        public const int Base = AbilityKit.Ability.World.Entitas.WorldSystemOrder.MobaBase;

        public const int EntityManagerSync = Base + AbilityKit.Ability.World.Entitas.WorldSystemOrder.Early + 5;
        public const int EntityManagerCleanup = Base + AbilityKit.Ability.World.Entitas.WorldSystemOrder.Late + 5;

        public const int ProjectileSync = Base + AbilityKit.Ability.World.Entitas.WorldSystemOrder.Late + 10;
        public const int ProjectileLauncherCleanup = Base + AbilityKit.Ability.World.Entitas.WorldSystemOrder.Late + 12;

        public const int SummonLifecycle = Base + AbilityKit.Ability.World.Entitas.WorldSystemOrder.Late + 14;

        public const int MotionInit = Base + AbilityKit.Ability.World.Entitas.WorldSystemOrder.Early + 10;
        public const int MotionLocomotionInput = Base + AbilityKit.Ability.World.Entitas.WorldSystemOrder.Normal + 50;
        public const int MotionTick = Base + AbilityKit.Ability.World.Entitas.WorldSystemOrder.Late + 20;

        public const int PassiveSkillTriggers = Base + AbilityKit.Ability.World.Entitas.WorldSystemOrder.Normal + 85;
        public const int EffectListeners = Base + AbilityKit.Ability.World.Entitas.WorldSystemOrder.Normal + 90;
        public const int SkillPipelines = Base + AbilityKit.Ability.World.Entitas.WorldSystemOrder.Normal + 100;
        public const int EffectsStep = Base + AbilityKit.Ability.World.Entitas.WorldSystemOrder.Normal + 200;
        public const int BuffsApply = Base + AbilityKit.Ability.World.Entitas.WorldSystemOrder.Normal + 300;
        public const int BuffsRemove = Base + AbilityKit.Ability.World.Entitas.WorldSystemOrder.Normal + 305;
        public const int BuffsTick = Base + AbilityKit.Ability.World.Entitas.WorldSystemOrder.Normal + 310;

        public const int OngoingTriggerPlansReconcile = Base + AbilityKit.Ability.World.Entitas.WorldSystemOrder.Normal + 312;
        public const int OngoingEffectsTick = Base + AbilityKit.Ability.World.Entitas.WorldSystemOrder.Normal + 315;
    }
}
