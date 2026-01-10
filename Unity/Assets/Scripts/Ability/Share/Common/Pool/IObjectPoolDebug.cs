using System;

namespace AbilityKit.Ability.Share.Common.Pool
{
    internal interface IObjectPoolDebug
    {
        Type ElementType { get; }
        PoolStats Stats { get; }
        int MaxSize { get; }
    }
}
