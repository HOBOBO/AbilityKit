namespace AbilityKit.Ability.Share.Impl.Moba.Systems
{
    public static class MobaSystemOrder
    {
        public const int Base = AbilityKit.Ability.World.Entitas.WorldSystemOrder.MobaBase;

        public const int SkillPipelines = Base + AbilityKit.Ability.World.Entitas.WorldSystemOrder.Normal + 100;
        public const int EffectsStep = Base + AbilityKit.Ability.World.Entitas.WorldSystemOrder.Normal + 200;
    }
}
