namespace AbilityKit.Demo.Common.Gameplay
{
    public static class DemoSceneRoutes
    {
        public const string Starter = "StarterScene";
        public const string Moba = "MobaDemoGameplayScene";
        public const string Shooter = "ShooterDemoGameplayScene";

        public static string GetGameplaySceneName(DemoGameplayId gameplay)
        {
            return gameplay == DemoGameplayId.Moba ? Moba : Shooter;
        }
    }
}
