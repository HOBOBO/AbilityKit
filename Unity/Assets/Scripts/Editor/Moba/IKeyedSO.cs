namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    public interface IKeyedSO<out TKey>
    {
        TKey Key { get; }
    }
}
