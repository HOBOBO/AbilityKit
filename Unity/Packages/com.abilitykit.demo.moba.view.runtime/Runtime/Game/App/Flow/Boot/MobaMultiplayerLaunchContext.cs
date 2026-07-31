using System.Threading;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// One-shot handoff from the multiplayer starter to the MOBA client scene.
    /// </summary>
    public static class MobaMultiplayerLaunchContext
    {
        private static int _requested;

        public static void Request()
        {
            Interlocked.Exchange(ref _requested, 1);
        }

        internal static bool ConsumeRequest()
        {
            return Interlocked.Exchange(ref _requested, 0) != 0;
        }
    }
}
