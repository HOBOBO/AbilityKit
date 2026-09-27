using System.Diagnostics;

namespace AbilityKit.Game.Flow
{
    internal sealed class StopwatchSessionTickClock : ISessionTickClock
    {
        public double NowSeconds =>
            Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
    }
}
