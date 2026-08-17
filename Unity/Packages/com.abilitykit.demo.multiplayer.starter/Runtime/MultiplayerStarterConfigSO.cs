#nullable enable

using System;
using AbilityKit.Demo.Common.Gameplay;
using UnityEngine;

namespace AbilityKit.Starter
{
    [CreateAssetMenu(
        fileName = "MultiplayerStarterConfig",
        menuName = "AbilityKit/Multiplayer/Starter Config")]
    public sealed class MultiplayerStarterConfigSO : ScriptableObject
    {
        [Header("Gateway Environment")]
        [SerializeField] private string host = "127.0.0.1";
        [SerializeField] private int port = 4000;
        [SerializeField] private string region = "dev";
        [SerializeField] private string serverId = "local";
        [SerializeField] private float requestTimeoutSeconds = 10f;

        [Header("Authentication")]
        [SerializeField] private string defaultAccountPrefix = "unity-account";
        [SerializeField] private string defaultGuestPrefix = "unity-guest";

        [Header("Scenes")]
        [SerializeField] private string gameplaySceneName = DemoSceneRoutes.Gameplay;

        [Header("Gameplay Profiles")]
        [SerializeField] private string mobaProfileId = string.Empty;
        [SerializeField] private string shooterProfileId = string.Empty;

        public string Host => string.IsNullOrWhiteSpace(host) ? "127.0.0.1" : host.Trim();
        public int Port => Math.Max(1, port);
        public string Region => string.IsNullOrWhiteSpace(region) ? "dev" : region.Trim();
        public string ServerId => string.IsNullOrWhiteSpace(serverId) ? "local" : serverId.Trim();
        public TimeSpan RequestTimeout => TimeSpan.FromSeconds(Math.Max(1f, requestTimeoutSeconds));
        public string DefaultAccountPrefix => string.IsNullOrWhiteSpace(defaultAccountPrefix) ? "unity-account" : defaultAccountPrefix.Trim();
        public string DefaultGuestPrefix => string.IsNullOrWhiteSpace(defaultGuestPrefix) ? "unity-guest" : defaultGuestPrefix.Trim();
        public string GameplaySceneName => string.IsNullOrWhiteSpace(gameplaySceneName)
            ? DemoSceneRoutes.Gameplay
            : gameplaySceneName.Trim();
        public string MobaProfileId => mobaProfileId?.Trim() ?? string.Empty;
        public string ShooterProfileId => shooterProfileId?.Trim() ?? string.Empty;
    }
}
