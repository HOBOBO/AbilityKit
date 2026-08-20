#nullable enable

using System;
using UnityEngine;

namespace AbilityKit.Demo.Shooter.View.PlayMode
{
    [CreateAssetMenu(
        fileName = "ShooterRemoteStateSyncPlayModeProfile",
        menuName = "AbilityKit/Shooter/Remote State Sync Play Mode Profile")]
    public sealed class ShooterRemoteStateSyncPlayModeProfile : ScriptableObject
    {
        [Header("Connection")]
        [SerializeField] private ShooterRemoteStateSyncLaunchMode launchMode = ShooterRemoteStateSyncLaunchMode.RestoreFirst;
        [SerializeField] private string host = ShooterRemoteStateSyncDefaults.DefaultHost;
        [SerializeField] private int port = ShooterRemoteStateSyncDefaults.DefaultPort;
        [SerializeField] private string sessionToken = ShooterRemoteStateSyncDefaults.DefaultSessionToken;
        [SerializeField] private string region = ShooterRemoteStateSyncDefaults.DefaultRegion;
        [SerializeField] private string serverId = ShooterRemoteStateSyncDefaults.DefaultServerId;
        [SerializeField] private string roomId = string.Empty;
        [SerializeField] private float timeoutSeconds = 10f;

        [Header("Session")]
        [SerializeField] private string syncTemplateId = ShooterRoomLaunchSpec.DefaultSyncTemplateId;
        [SerializeField] private int randomSeed = 3901;
        [SerializeField] private int playerCount = 2;
        [SerializeField] private int controlledPlayerId = 1;
        [SerializeField] private float worldScale = 1f;

        public ShooterRemoteStateSyncLaunchMode LaunchMode => launchMode;
        public string Host => string.IsNullOrWhiteSpace(host) ? ShooterRemoteStateSyncDefaults.DefaultHost : host;
        public int Port => Math.Max(1, port);
        public string SessionToken => string.IsNullOrWhiteSpace(sessionToken) ? ShooterRemoteStateSyncDefaults.DefaultSessionToken : sessionToken;
        public string Region => string.IsNullOrWhiteSpace(region) ? ShooterRemoteStateSyncDefaults.DefaultRegion : region;
        public string ServerId => string.IsNullOrWhiteSpace(serverId) ? ShooterRemoteStateSyncDefaults.DefaultServerId : serverId;
        public string RoomId => roomId ?? string.Empty;
        public TimeSpan Timeout => TimeSpan.FromSeconds(Math.Max(1f, timeoutSeconds));
        public string SyncTemplateId => string.IsNullOrWhiteSpace(syncTemplateId) ? ShooterRoomLaunchSpec.DefaultSyncTemplateId : syncTemplateId;
        public int RandomSeed => randomSeed;
        public int PlayerCount => Math.Max(1, playerCount);
        public int ControlledPlayerId => Math.Max(1, controlledPlayerId);
        public float WorldScale => Mathf.Max(0.001f, worldScale);

        public ShooterRemoteStateSyncLaunchOptions BuildLaunchOptions(
            string? sessionTokenOverride = null,
            string? roomIdOverride = null,
            ShooterRemoteStateSyncLaunchMode? launchModeOverride = null)
        {
            var templateOptions = ShooterPlayModeSessionOptions.FromTemplate(
                ShooterAcceptanceCatalog.GetSyncTemplate(SyncTemplateId),
                RandomSeed,
                ControlledPlayerId,
                WorldScale);
            var sessionOptions = new ShooterPlayModeSessionOptions(
                templateOptions.SyncModel,
                templateOptions.TickRate,
                Math.Max(PlayerCount, ControlledPlayerId),
                templateOptions.RandomSeed,
                templateOptions.ControlledPlayerId,
                templateOptions.EnableAuthoritativeWorld,
                templateOptions.LatencyMs,
                templateOptions.JitterMs,
                templateOptions.PacketLossRate,
                templateOptions.ReorderRate,
                templateOptions.BandwidthKbps,
                templateOptions.WorldScale,
                templateOptions.NetworkName,
                templateOptions.SyncTemplateId,
                templateOptions.GameplayScenario);

            return new ShooterRemoteStateSyncLaunchOptions(
                sessionOptions,
                new ShooterClientNetworkEndpoint(Host, Port),
                string.IsNullOrWhiteSpace(sessionTokenOverride) ? SessionToken : sessionTokenOverride!,
                Region,
                ServerId,
                launchModeOverride ?? LaunchMode,
                Timeout,
                roomIdOverride ?? RoomId);
        }
    }
}
