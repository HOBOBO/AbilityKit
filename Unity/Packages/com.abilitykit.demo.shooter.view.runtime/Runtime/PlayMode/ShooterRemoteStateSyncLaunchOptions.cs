#nullable enable

using System;
using AbilityKit.Demo.Shooter.View.Hosting;
using AbilityKit.Network.Sdk;

namespace AbilityKit.Demo.Shooter.View.PlayMode
{
    public static class ShooterRemoteStateSyncDefaults
    {
        public const string DefaultHost = "127.0.0.1";
        public const int DefaultPort = 41001;
        public const string DefaultRegion = "local";
        public const string DefaultServerId = "dev";
        public const string DefaultSessionToken = "unity-shooter-state-sync";
    }

    public enum ShooterRemoteStateSyncLaunchMode
    {
        RestoreFirst = 0,
        CreateNew = 1,
        RestoreOnly = 2,
        JoinRoom = 3
    }

    public readonly struct ShooterRemoteStateSyncLaunchOptions
    {
        public ShooterRemoteStateSyncLaunchOptions(
            ShooterPlayModeSessionOptions sessionOptions,
            ShooterClientNetworkEndpoint endpoint,
            string sessionToken = ShooterRemoteStateSyncDefaults.DefaultSessionToken,
            string region = ShooterRemoteStateSyncDefaults.DefaultRegion,
            string serverId = ShooterRemoteStateSyncDefaults.DefaultServerId,
            ShooterRemoteStateSyncLaunchMode launchMode = ShooterRemoteStateSyncLaunchMode.RestoreFirst,
            TimeSpan? timeout = null,
            string roomId = "",
            ShooterRoomLaunchSpec? roomLaunchSpec = null,
            IReliableEventCheckpointStore? reliableEventCheckpointStore = null,
            ReliableEventCheckpointLifecycleOptions? reliableEventCheckpointLifecycleOptions = null)
        {
            SessionOptions = sessionOptions.Normalized();
            Endpoint = endpoint;
            SessionToken = string.IsNullOrWhiteSpace(sessionToken) ? ShooterRemoteStateSyncDefaults.DefaultSessionToken : sessionToken;
            Region = string.IsNullOrWhiteSpace(region) ? ShooterRemoteStateSyncDefaults.DefaultRegion : region;
            ServerId = string.IsNullOrWhiteSpace(serverId) ? ShooterRemoteStateSyncDefaults.DefaultServerId : serverId;
            LaunchMode = launchMode;
            Timeout = timeout ?? TimeSpan.FromSeconds(5);
            RoomId = roomId ?? string.Empty;
            RoomLaunchSpec = roomLaunchSpec;
#if UNITY_5_3_OR_NEWER
            ReliableEventCheckpointStore = reliableEventCheckpointStore
                ?? ShooterUnityReliableEventCheckpointStores.Default;
#else
            ReliableEventCheckpointStore = reliableEventCheckpointStore;
#endif
            ReliableEventCheckpointLifecycleOptions = reliableEventCheckpointLifecycleOptions;
        }

        public ShooterPlayModeSessionOptions SessionOptions { get; }
        public ShooterClientNetworkEndpoint Endpoint { get; }
        public string SessionToken { get; }
        public string Region { get; }
        public string ServerId { get; }
        public ShooterRemoteStateSyncLaunchMode LaunchMode { get; }
        public TimeSpan Timeout { get; }
        public string RoomId { get; }
        public ShooterRoomLaunchSpec? RoomLaunchSpec { get; }
        /// <summary>跨重连会话复用的可靠事件检查点存储。</summary>
        public IReliableEventCheckpointStore? ReliableEventCheckpointStore { get; }

        /// <summary>可靠事件检查点在断线、暂停和退出阶段采用的生命周期策略。</summary>
        public ReliableEventCheckpointLifecycleOptions? ReliableEventCheckpointLifecycleOptions { get; }

        public ShooterClientSyncAssemblyOptions CreateClientSyncAssemblyOptions()
        {
            return ShooterClientSyncAssemblyOptions
                .ForModel(SessionOptions.SyncModel)
                .WithReliableEventCheckpointStore(ReliableEventCheckpointStore)
                .WithReliableEventCheckpointLifecycleOptions(
                    ReliableEventCheckpointLifecycleOptions);
        }

        public static ShooterRemoteStateSyncLaunchOptions RestoreFirst(
            ShooterPlayModeSessionOptions sessionOptions,
            ShooterClientNetworkEndpoint endpoint,
            string sessionToken = ShooterRemoteStateSyncDefaults.DefaultSessionToken,
            string region = ShooterRemoteStateSyncDefaults.DefaultRegion,
            string serverId = ShooterRemoteStateSyncDefaults.DefaultServerId,
            TimeSpan? timeout = null)
        {
            return new ShooterRemoteStateSyncLaunchOptions(
                sessionOptions,
                endpoint,
                sessionToken,
                region,
                serverId,
                ShooterRemoteStateSyncLaunchMode.RestoreFirst,
                timeout);
        }

        public static ShooterRemoteStateSyncLaunchOptions JoinRoom(
            ShooterPlayModeSessionOptions sessionOptions,
            ShooterClientNetworkEndpoint endpoint,
            string sessionToken,
            string region,
            string serverId,
            string roomId,
            TimeSpan? timeout = null)
        {
            return new ShooterRemoteStateSyncLaunchOptions(
                sessionOptions,
                endpoint,
                sessionToken,
                region,
                serverId,
                ShooterRemoteStateSyncLaunchMode.JoinRoom,
                timeout,
                roomId);
        }
    }
}
