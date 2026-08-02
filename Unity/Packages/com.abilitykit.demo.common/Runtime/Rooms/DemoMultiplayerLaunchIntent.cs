#nullable enable

using System;
using System.Threading;

namespace AbilityKit.Demo.Common.Rooms
{
    public enum DemoMultiplayerGameplay
    {
        Moba = 0,
        Shooter = 1
    }

    public sealed class DemoMultiplayerLaunchRequest
    {
        public DemoMultiplayerLaunchRequest(
            string host,
            int port,
            string region,
            string serverId,
            string accountId,
            string sessionToken,
            TimeSpan timeout,
            bool suppressAutomaticLobbyActions = false)
        {
            Host = host ?? string.Empty;
            Port = port;
            Region = region ?? string.Empty;
            ServerId = serverId ?? string.Empty;
            AccountId = accountId ?? string.Empty;
            SessionToken = sessionToken ?? string.Empty;
            Timeout = timeout;
            SuppressAutomaticLobbyActions = suppressAutomaticLobbyActions;
        }

        public string Host { get; }
        public int Port { get; }
        public string Region { get; }
        public string ServerId { get; }
        public string AccountId { get; }
        public string SessionToken { get; }
        public TimeSpan Timeout { get; }
        public bool SuppressAutomaticLobbyActions { get; }
        public bool IsAuthenticated => !string.IsNullOrWhiteSpace(SessionToken);
    }

    /// <summary>
    /// One-shot handoff from the common starter scene to a gameplay-specific multiplayer scene.
    /// </summary>
    public static class DemoMultiplayerLaunchIntent
    {
        private static int _requested;
        private static DemoMultiplayerGameplay _gameplay;
        private static DemoMultiplayerLaunchRequest _request;

        public static void Request(
            DemoMultiplayerGameplay gameplay,
            DemoMultiplayerLaunchRequest request)
        {
            _gameplay = gameplay;
            _request = request ?? throw new ArgumentNullException(nameof(request));
            Interlocked.Exchange(ref _requested, 1);
        }

        public static bool TryConsume(
            DemoMultiplayerGameplay expectedGameplay,
            out DemoMultiplayerLaunchRequest request)
        {
            if (Interlocked.Exchange(ref _requested, 0) == 0)
            {
                request = null;
                return false;
            }

            request = _request;
            var matches = _gameplay == expectedGameplay;
            _request = default;
            return matches;
        }
    }
}
