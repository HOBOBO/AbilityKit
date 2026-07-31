#nullable enable

using System.Threading;

namespace AbilityKit.Demo.Shooter.View.PlayMode
{
    /// <summary>
    /// One-shot handoff from the multiplayer starter to the Shooter client scene.
    /// </summary>
    public static class ShooterMultiplayerLaunchContext
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
