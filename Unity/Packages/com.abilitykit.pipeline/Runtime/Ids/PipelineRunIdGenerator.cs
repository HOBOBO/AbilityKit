using System.Threading;

namespace AbilityKit.Pipeline
{
    internal static class PipelineRunIdGenerator
    {
        private static int _nextId;

        public static int Next()
        {
            var id = Interlocked.Increment(ref _nextId);
            return id != 0 ? id : Interlocked.Increment(ref _nextId);
        }
    }
}
