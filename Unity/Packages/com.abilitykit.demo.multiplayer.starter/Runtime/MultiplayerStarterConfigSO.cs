#nullable enable

using System;
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
        [SerializeField] private string mobaSceneName = "MobaMultiplayerScene";
        [SerializeField] private string shooterSceneName = "ShooterMultiplayerScene";

        public string Host => string.IsNullOrWhiteSpace(host) ? "127.0.0.1" : host.Trim();
        public int Port => Math.Max(1, port);
        public string Region => string.IsNullOrWhiteSpace(region) ? "dev" : region.Trim();
        public string ServerId => string.IsNullOrWhiteSpace(serverId) ? "local" : serverId.Trim();
        public TimeSpan RequestTimeout => TimeSpan.FromSeconds(Math.Max(1f, requestTimeoutSeconds));
        public string DefaultAccountPrefix => string.IsNullOrWhiteSpace(defaultAccountPrefix) ? "unity-account" : defaultAccountPrefix.Trim();
        public string DefaultGuestPrefix => string.IsNullOrWhiteSpace(defaultGuestPrefix) ? "unity-guest" : defaultGuestPrefix.Trim();
        public string MobaSceneName => string.IsNullOrWhiteSpace(mobaSceneName) ? "MobaMultiplayerScene" : mobaSceneName.Trim();
        public string ShooterSceneName => string.IsNullOrWhiteSpace(shooterSceneName) ? "ShooterMultiplayerScene" : shooterSceneName.Trim();
    }
}
