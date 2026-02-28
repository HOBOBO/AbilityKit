namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config
{
    public interface IMobaConfigBytesSource
    {
        bool TryGetBytes(string key, out byte[] bytes);
    }
}
