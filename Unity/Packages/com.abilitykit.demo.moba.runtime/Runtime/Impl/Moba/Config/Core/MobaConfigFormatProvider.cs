namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core
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

    /// <summary>
    /// 使用 Luban 二进制格式的 Provider
    /// </summary>
    public sealed class LubanBinaryMobaConfigFormatProvider : IMobaConfigFormatProvider
    {
        public static readonly LubanBinaryMobaConfigFormatProvider Instance = new LubanBinaryMobaConfigFormatProvider();

        private LubanBinaryMobaConfigFormatProvider() { }

        public MobaConfigFormat Format => MobaConfigFormat.Bytes;
    }
}
