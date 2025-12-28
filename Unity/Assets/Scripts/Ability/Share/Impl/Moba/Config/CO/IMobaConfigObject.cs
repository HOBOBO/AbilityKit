using System;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.CO
{
    public interface IMobaConfigObject<out TKey>
    {
        TKey Key { get; }
    }

    public interface ITaggedConfigObject
    {
        ReadOnlySpan<int> Tags { get; }
    }
}
