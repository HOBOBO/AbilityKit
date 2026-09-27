#nullable enable

using System;
using AbilityKit.Demo.Common.Rooms;
using UnityEngine.SceneManagement;

namespace AbilityKit.Demo.Tiny.Turn.View
{
    public static class TinyTurnProjectLaunch
    {
        public const string SceneName = "TinyTurnGameplayScene";
        private static DemoMultiplayerLaunchRequest? _pending;
        private static string _returnScene = string.Empty;

        public static void Open(DemoMultiplayerLaunchRequest launch, string returnScene)
        {
            Prepare(launch, returnScene);
            try { SceneManager.LoadScene(SceneName, LoadSceneMode.Single); }
            catch { Clear(); throw; }
        }

        public static void Prepare(DemoMultiplayerLaunchRequest launch, string returnScene)
        {
            if (launch == null || !launch.IsAuthenticated ||
                string.IsNullOrWhiteSpace(launch.AccountId) ||
                string.IsNullOrWhiteSpace(launch.Host) || launch.Port <= 0 || launch.Port > 65535)
                throw new ArgumentException("An authenticated Tiny Turn launch is required.", nameof(launch));
            if (string.IsNullOrWhiteSpace(returnScene))
                throw new ArgumentException("A return scene is required.", nameof(returnScene));
            _pending = launch;
            _returnScene = returnScene;
        }

        internal static bool TryConsume(out DemoMultiplayerLaunchRequest launch, out string returnScene)
        {
            launch = _pending!;
            returnScene = _returnScene;
            Clear();
            return launch != null;
        }

        internal static void Clear()
        {
            _pending = null;
            _returnScene = string.Empty;
        }
    }
}
