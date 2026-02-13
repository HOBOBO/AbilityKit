namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config
{
    public sealed class DefaultMobaConfigTableRegistry : IMobaConfigTableRegistry
    {
        public static readonly DefaultMobaConfigTableRegistry Instance = new DefaultMobaConfigTableRegistry();

        private DefaultMobaConfigTableRegistry() { }

        public MobaRuntimeConfigTableRegistry.Entry[] Tables => MobaRuntimeConfigTableRegistry.Tables;
    }
}
