using System.Threading;

namespace AbilityKit.Game.Battle.Agent
{
    internal sealed class BattleInputCommandSequence
    {
        private long _next;

        public ulong Next()
        {
            return unchecked((ulong)Interlocked.Increment(ref _next));
        }
    }
}
