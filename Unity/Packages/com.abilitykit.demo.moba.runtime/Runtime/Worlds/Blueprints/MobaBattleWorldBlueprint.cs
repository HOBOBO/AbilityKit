namespace AbilityKit.Demo.Moba.Worlds.Blueprints
{
    public sealed class MobaBattleWorldBlueprint : MobaLogicWorldBlueprintBase
    {
        public const string Type = "battle";

        public override string WorldType => Type;

        protected override MobaLogicWorldProfile Profile => MobaLogicWorldProfile.Battle;

        protected override MobaLogicWorldFeatures Features => MobaLogicWorldFeatures.EntitasContexts | MobaLogicWorldFeatures.BattleRuntime;

        protected override void ConfigureModules(AbilityKit.Ability.World.Abstractions.WorldCreateOptions options)
        {
            EnsureModule(options, () => new AbilityKit.Demo.Moba.Systems.MobaServicesAutoModule());
            EnsureModule(options, () => new AbilityKit.Demo.Moba.Systems.MobaWorldBootstrapModule());
        }
    }
}
