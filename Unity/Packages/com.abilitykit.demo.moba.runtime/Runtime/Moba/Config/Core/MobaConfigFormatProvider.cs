namespace AbilityKit.Demo.Moba.Config.Core
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
    /// 浣跨敤 Luban 浜岃繘鍒舵牸寮忕殑 Provider
    /// </summary>
    public sealed class LubanBinaryMobaConfigFormatProvider : IMobaConfigFormatProvider
    {
        public static readonly LubanBinaryMobaConfigFormatProvider Instance = new LubanBinaryMobaConfigFormatProvider();

        private LubanBinaryMobaConfigFormatProvider() { }

        public MobaConfigFormat Format => MobaConfigFormat.Bytes;
    }
}
