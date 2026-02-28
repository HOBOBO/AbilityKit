namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config
{
    public interface IMobaConfigFormatProvider
    {
        MobaConfigFormat Format { get; }
    }

    public sealed class DefaultMobaConfigFormatProvider : IMobaConfigFormatProvider
    {
        public static readonly DefaultMobaConfigFormatProvider Instance = new DefaultMobaConfigFormatProvider();

        private DefaultMobaConfigFormatProvider() { }

        public MobaConfigFormat Format => MobaConfigFormat.Json;
    }
}
