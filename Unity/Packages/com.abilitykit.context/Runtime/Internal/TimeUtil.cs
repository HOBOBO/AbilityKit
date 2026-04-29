using System;

namespace AbilityKit.Context
{
    internal static class TimeUtil
    {
        public static long CurrentTimeMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}
