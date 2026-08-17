#nullable enable

namespace AbilityKit.Demo.Common.Gameplay
{
    /// <summary>
    /// One-shot composition request passed from a launcher to the shared gameplay scene.
    /// </summary>
    public static class DemoLaunchIntent
    {
        private static readonly object Gate = new object();
        private static bool _hasRequest;
        private static DemoLaunchRequest _request;

        public static void Request(in DemoLaunchRequest request)
        {
            lock (Gate)
            {
                _request = request;
                _hasRequest = true;
            }
        }

        public static bool TryConsume(out DemoLaunchRequest request)
        {
            lock (Gate)
            {
                if (!_hasRequest)
                {
                    request = default;
                    return false;
                }

                request = _request;
                _request = default;
                _hasRequest = false;
                return true;
            }
        }

        public static void Clear()
        {
            lock (Gate)
            {
                _request = default;
                _hasRequest = false;
            }
        }
    }
}
