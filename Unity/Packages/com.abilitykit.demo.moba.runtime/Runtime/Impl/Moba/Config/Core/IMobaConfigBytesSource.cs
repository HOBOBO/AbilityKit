namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core
{
    public interface IMobaConfigBytesSource
    {
        bool TryGetBytes(string key, out byte[] bytes);
    }
}
