namespace AbilityKit.Demo.Common.Gameplay
{
    public static class DemoSceneRoutes
    {
        public const string Starter = "StarterScene";
        public const string Moba = "MobaDemoGameplayScene";
        public const string Shooter = "ShooterDemoGameplayScene";
        public const string Tiny = "TinyDemoGameplayScene";

        public static string GetGameplaySceneName(DemoGameplayId gameplay)
        {
            switch (gameplay)
            {
                case DemoGameplayId.Moba: return Moba;
                case DemoGameplayId.Shooter: return Shooter;
                case DemoGameplayId.Tiny: return Tiny;
                default: throw new System.ArgumentOutOfRangeException(nameof(gameplay));
            }
        }
    }
}
