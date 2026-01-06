namespace AbilityKit.Ability.Share.Impl.Moba.Systems
{
    public static class MobaSystemOrder
    {
        public const int Base = AbilityKit.Ability.World.Entitas.WorldSystemOrder.MobaBase;

        public const int SkillPipelines = Base + AbilityKit.Ability.World.Entitas.WorldSystemOrder.Normal + 100;
        public const int EffectsStep = Base + AbilityKit.Ability.World.Entitas.WorldSystemOrder.Normal + 200;
        public const int BuffsApply = Base + AbilityKit.Ability.World.Entitas.WorldSystemOrder.Normal + 300;
        public const int BuffsTick = Base + AbilityKit.Ability.World.Entitas.WorldSystemOrder.Normal + 310;
    }
}
