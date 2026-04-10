using System;

namespace AbilityKit.Core.Common.Pool
{
    internal interface IObjectPoolDebug
    {
        Type ElementType { get; }
        PoolStats Stats { get; }
        int MaxSize { get; }
    }
}
