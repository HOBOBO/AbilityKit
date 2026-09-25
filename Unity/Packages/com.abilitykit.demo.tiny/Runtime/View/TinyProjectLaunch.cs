#nullable enable

using System;
using AbilityKit.Demo.Common.Gameplay;
using AbilityKit.Demo.Common.Rooms;
using UnityEngine.SceneManagement;

namespace AbilityKit.Demo.Tiny.View
{
    /// <summary>One-shot handoff for projects that launch Tiny without the demo Starter scene.</summary>
    public static class TinyProjectLaunch
    {
        private static readonly object Gate = new object();
        private static DemoMultiplayerLaunchRequest? _pending;
        private static string _returnScene = string.Empty;

        public static void Open(DemoMultiplayerLaunchRequest request, string returnScene)
        {
            Prepare(request, returnScene);
            try { SceneManager.LoadScene(DemoSceneRoutes.Tiny, LoadSceneMode.Single); }
            catch
            {
                Clear();
                throw;
            }
        }

        public static void Prepare(DemoMultiplayerLaunchRequest request, string returnScene)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!request.IsAuthenticated || string.IsNullOrWhiteSpace(request.AccountId) ||
                string.IsNullOrWhiteSpace(request.Host) || request.Port <= 0 || request.Port > 65535)
                throw new ArgumentException("An authenticated Tiny launch is required.", nameof(request));
            if (string.IsNullOrWhiteSpace(returnScene))
                throw new ArgumentException("A return scene is required.", nameof(returnScene));
            lock (Gate)
            {
                _pending = request;
                _returnScene = returnScene;
                DemoMultiplayerLaunchIntent.Request(DemoMultiplayerGameplay.Tiny, request);
                DemoLaunchIntent.Request(new DemoLaunchRequest(
                    DemoGameplayId.Tiny, DemoLaunchMode.Multiplayer, "tiny-multiplayer"));
            }
        }

        internal static bool TryConsume(out DemoMultiplayerLaunchRequest request, out string returnScene)
        {
            lock (Gate)
            {
                request = _pending!;
                returnScene = _returnScene;
                ClearUnsafe();
                if (request != null)
                    DemoMultiplayerLaunchIntent.TryConsume(DemoMultiplayerGameplay.Tiny, out _);
                return request != null;
            }
        }

        internal static void Clear()
        {
            lock (Gate)
            {
                ClearUnsafe();
                DemoLaunchIntent.Clear();
                DemoMultiplayerLaunchIntent.Clear();
            }
        }

        private static void ClearUnsafe()
        {
            _pending = null;
            _returnScene = string.Empty;
        }
    }
}
