namespace AbilityKit.Ability.Share.Impl.Moba.Systems
{
    public static class MobaSystemOrder
    {
        public const int Base = AbilityKit.Ability.World.Entitas.WorldSystemOrder.MobaBase;

        public const int MotionInit = Base + AbilityKit.Ability.World.Entitas.WorldSystemOrder.Early + 10;
        public const int MotionLocomotionInput = Base + AbilityKit.Ability.World.Entitas.WorldSystemOrder.Normal + 50;
        public const int MotionTick = Base + AbilityKit.Ability.World.Entitas.WorldSystemOrder.Late + 20;

        public const int SkillPipelines = Base + AbilityKit.Ability.World.Entitas.WorldSystemOrder.Normal + 100;
        public const int EffectsStep = Base + AbilityKit.Ability.World.Entitas.WorldSystemOrder.Normal + 200;
        public const int BuffsApply = Base + AbilityKit.Ability.World.Entitas.WorldSystemOrder.Normal + 300;
        public const int BuffsTick = Base + AbilityKit.Ability.World.Entitas.WorldSystemOrder.Normal + 310;
    }
}
