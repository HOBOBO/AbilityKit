#nullable enable

using System;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Network.Sdk;
using AbilityKit.Protocol.Shooter;

namespace AbilityKit.Demo.Shooter.View
{
    /// <summary>
    /// Shooter 客户端同步链路组装选项。
    /// 将同步模型、快照解码器与插值配置集中到一个稳定参数对象中，
    /// 便于 PlayMode、Gateway 与验收测试在同一客户端装配入口上切换多种同步方案。
    /// </summary>
    public readonly struct ShooterClientSyncAssemblyOptions
    {
        public ShooterClientSyncAssemblyOptions(
            NetworkSyncModel syncModel,
            ShooterGatewaySnapshotDecoder? decoder = null,
            InterpolationConfig? interpolationConfig = null,
            IReliableEventCheckpointStore? reliableEventCheckpointStore = null,
            ReliableEventCheckpointLifecycleOptions? reliableEventCheckpointLifecycleOptions = null,
            NetworkSessionRecoveryOptions? sessionRecoveryOptions = null)
            : this(
                NetworkSyncProfileRegistry.Resolve(syncModel),
                syncModel.ToString(),
                NetworkSyncProfileRegistry.DefaultCatalog,
                decoder,
                interpolationConfig,
                availableCapabilities: null,
                ShooterStateSyncCompatibilityPolicy.MinimumPureStateVersion,
                ShooterPureStateSyncCodec.CurrentVersion,
                remoteCapabilities: null,
                NetworkSyncRemoteCapabilityPolicy.Ignore,
                reliableEventCheckpointStore,
                reliableEventCheckpointLifecycleOptions,
                sessionRecoveryOptions)
        {
        }

        public ShooterClientSyncAssemblyOptions(
            in NetworkSyncProfile syncProfile,
            ShooterGatewaySnapshotDecoder? decoder = null,
            InterpolationConfig? interpolationConfig = null,
            IReliableEventCheckpointStore? reliableEventCheckpointStore = null,
            ReliableEventCheckpointLifecycleOptions? reliableEventCheckpointLifecycleOptions = null,
            NetworkSessionRecoveryOptions? sessionRecoveryOptions = null)
            : this(
                in syncProfile,
                syncProfile.CompatibilityModel.ToString(),
                NetworkSyncProfileRegistry.DefaultCatalog,
                decoder,
                interpolationConfig,
                availableCapabilities: null,
                ShooterStateSyncCompatibilityPolicy.MinimumPureStateVersion,
                ShooterPureStateSyncCodec.CurrentVersion,
                remoteCapabilities: null,
                NetworkSyncRemoteCapabilityPolicy.Ignore,
                reliableEventCheckpointStore,
                reliableEventCheckpointLifecycleOptions,
                sessionRecoveryOptions)
        {
        }

        private ShooterClientSyncAssemblyOptions(
            in NetworkSyncProfile syncProfile,
            string profileName,
            NetworkSyncProfileCatalog profileCatalog,
            ShooterGatewaySnapshotDecoder? decoder,
            InterpolationConfig? interpolationConfig,
            NetworkSyncCapabilities? availableCapabilities,
            int minimumSchemaVersion,
            int maximumSchemaVersion,
            NetworkSyncCapabilities? remoteCapabilities,
            NetworkSyncRemoteCapabilityPolicy remoteCapabilityPolicy,
            IReliableEventCheckpointStore? reliableEventCheckpointStore,
            ReliableEventCheckpointLifecycleOptions? reliableEventCheckpointLifecycleOptions,
            NetworkSessionRecoveryOptions? sessionRecoveryOptions)
        {
            SyncProfile = syncProfile;
            ProfileName = profileName;
            ProfileCatalog = profileCatalog ?? throw new ArgumentNullException(nameof(profileCatalog));
            Decoder = decoder;
            InterpolationConfig = interpolationConfig;
            MinimumSchemaVersion = minimumSchemaVersion;
            MaximumSchemaVersion = maximumSchemaVersion;
            AvailableCapabilities = availableCapabilities ?? NetworkSyncCapabilities.FromProfile(
                in syncProfile,
                minimumSchemaVersion,
                maximumSchemaVersion);
            RemoteCapabilities = remoteCapabilities;
            RemoteCapabilityPolicy = remoteCapabilityPolicy;
            ReliableEventCheckpointStore = reliableEventCheckpointStore;
            ReliableEventCheckpointLifecycleOptions = reliableEventCheckpointLifecycleOptions;
            SessionRecoveryOptions = sessionRecoveryOptions;
        }

        public static ShooterClientSyncAssemblyOptions Default => ForModel(ShooterClientSyncControllerFactory.DefaultSyncModel);

        public static ShooterClientSyncAssemblyOptions ForModel(NetworkSyncModel syncModel)
        {
            return new ShooterClientSyncAssemblyOptions(syncModel);
        }

        public static ShooterClientSyncAssemblyOptions ForProfile(in NetworkSyncProfile syncProfile)
        {
            return new ShooterClientSyncAssemblyOptions(in syncProfile);
        }

        public NetworkSyncProfile SyncProfile { get; }

        /// <summary>用于配置和诊断的稳定 Profile 名称。</summary>
        public string ProfileName { get; }

        /// <summary>用于解析项目自定义 Profile 的目录。</summary>
        public NetworkSyncProfileCatalog ProfileCatalog { get; }

        public NetworkSyncModel SyncModel => SyncProfile.CompatibilityModel;

        public ShooterGatewaySnapshotDecoder? Decoder { get; }

        public InterpolationConfig? InterpolationConfig { get; }

        /// <summary>跨客户端会话复用的可靠事件检查点存储提供器。</summary>
        public IReliableEventCheckpointStore? ReliableEventCheckpointStore { get; }

        /// <summary>可靠事件检查点在断线、暂停和退出阶段采用的生命周期策略。</summary>
        public ReliableEventCheckpointLifecycleOptions? ReliableEventCheckpointLifecycleOptions { get; }

        /// <summary>统一会话恢复决策采用的策略、升级和诊断选项。</summary>
        public NetworkSessionRecoveryOptions? SessionRecoveryOptions { get; }

        /// <summary>Shooter 客户端实际提供的同步能力。</summary>
        public NetworkSyncCapabilities AvailableCapabilities { get; }

        /// <summary>由 Gateway 握手或房间元数据提供的远端同步能力。</summary>
        public NetworkSyncCapabilities? RemoteCapabilities { get; }

        /// <summary>远端能力在 Shooter 会话启动时的协商要求。</summary>
        public NetworkSyncRemoteCapabilityPolicy RemoteCapabilityPolicy { get; }

        /// <summary>客户端支持的最低快照结构版本。</summary>
        public int MinimumSchemaVersion { get; }

        /// <summary>客户端支持的最高快照结构版本。</summary>
        public int MaximumSchemaVersion { get; }

        public ShooterClientSyncAssemblyOptions WithDecoder(ShooterGatewaySnapshotDecoder? decoder)
        {
            return new ShooterClientSyncAssemblyOptions(
                SyncProfile,
                ProfileName,
                ProfileCatalog,
                decoder,
                InterpolationConfig,
                AvailableCapabilities,
                MinimumSchemaVersion,
                MaximumSchemaVersion,
                RemoteCapabilities,
                RemoteCapabilityPolicy,
                ReliableEventCheckpointStore,
                ReliableEventCheckpointLifecycleOptions,
                SessionRecoveryOptions);
        }

        public ShooterClientSyncAssemblyOptions WithInterpolationConfig(InterpolationConfig? interpolationConfig)
        {
            return new ShooterClientSyncAssemblyOptions(
                SyncProfile,
                ProfileName,
                ProfileCatalog,
                Decoder,
                interpolationConfig,
                AvailableCapabilities,
                MinimumSchemaVersion,
                MaximumSchemaVersion,
                RemoteCapabilities,
                RemoteCapabilityPolicy,
                ReliableEventCheckpointStore,
                ReliableEventCheckpointLifecycleOptions,
                SessionRecoveryOptions);
        }

        public ShooterClientSyncAssemblyOptions WithSyncModel(NetworkSyncModel syncModel)
        {
            var profile = NetworkSyncProfileRegistry.Resolve(syncModel);
            return new ShooterClientSyncAssemblyOptions(
                in profile,
                syncModel.ToString(),
                ProfileCatalog,
                Decoder,
                InterpolationConfig,
                availableCapabilities: null,
                MinimumSchemaVersion,
                MaximumSchemaVersion,
                RemoteCapabilities,
                RemoteCapabilityPolicy,
                ReliableEventCheckpointStore,
                ReliableEventCheckpointLifecycleOptions,
                SessionRecoveryOptions);
        }

        public ShooterClientSyncAssemblyOptions WithSyncProfile(in NetworkSyncProfile syncProfile)
        {
            return new ShooterClientSyncAssemblyOptions(
                in syncProfile,
                syncProfile.CompatibilityModel.ToString(),
                ProfileCatalog,
                Decoder,
                InterpolationConfig,
                availableCapabilities: null,
                MinimumSchemaVersion,
                MaximumSchemaVersion,
                RemoteCapabilities,
                RemoteCapabilityPolicy,
                ReliableEventCheckpointStore,
                ReliableEventCheckpointLifecycleOptions,
                SessionRecoveryOptions);
        }

        /// <summary>替换接入模块实际提供的能力声明。</summary>
        public ShooterClientSyncAssemblyOptions WithAvailableCapabilities(
            in NetworkSyncCapabilities availableCapabilities)
        {
            return new ShooterClientSyncAssemblyOptions(
                SyncProfile,
                ProfileName,
                ProfileCatalog,
                Decoder,
                InterpolationConfig,
                availableCapabilities,
                MinimumSchemaVersion,
                MaximumSchemaVersion,
                RemoteCapabilities,
                RemoteCapabilityPolicy,
                ReliableEventCheckpointStore,
                ReliableEventCheckpointLifecycleOptions,
                SessionRecoveryOptions);
        }

        /// <summary>替换会话要求和客户端支持的协议结构版本范围。</summary>
        public ShooterClientSyncAssemblyOptions WithSchemaVersionRange(int minimum, int maximum)
        {
            var capabilities = new NetworkSyncCapabilities(
                minimum,
                maximum,
                AvailableCapabilities.ClientPlayback,
                AvailableCapabilities.Input,
                AvailableCapabilities.Snapshot,
                AvailableCapabilities.Interest,
                AvailableCapabilities.Recovery,
                AvailableCapabilities.ServerValidation);
            return new ShooterClientSyncAssemblyOptions(
                SyncProfile,
                ProfileName,
                ProfileCatalog,
                Decoder,
                InterpolationConfig,
                capabilities,
                minimum,
                maximum,
                RemoteCapabilities,
                RemoteCapabilityPolicy,
                ReliableEventCheckpointStore,
                ReliableEventCheckpointLifecycleOptions,
                SessionRecoveryOptions);
        }

        /// <summary>使用项目目录中的稳定名称覆盖 Profile 诊断标识。</summary>
        public ShooterClientSyncAssemblyOptions WithProfileCatalog(
            string profileName,
            NetworkSyncProfileCatalog profileCatalog)
        {
            if (string.IsNullOrWhiteSpace(profileName))
                throw new ArgumentException("Profile 名称不能为空。", nameof(profileName));
            if (profileCatalog == null) throw new ArgumentNullException(nameof(profileCatalog));

            var profile = profileCatalog.Resolve(profileName);
            return new ShooterClientSyncAssemblyOptions(
                in profile,
                profileName,
                profileCatalog,
                Decoder,
                InterpolationConfig,
                availableCapabilities: null,
                MinimumSchemaVersion,
                MaximumSchemaVersion,
                RemoteCapabilities,
                RemoteCapabilityPolicy,
                ReliableEventCheckpointStore,
                ReliableEventCheckpointLifecycleOptions,
                SessionRecoveryOptions);
        }

        /// <summary>设置远端能力声明及其参与启动协商的策略。</summary>
        public ShooterClientSyncAssemblyOptions WithRemoteCapabilities(
            NetworkSyncCapabilities? remoteCapabilities,
            NetworkSyncRemoteCapabilityPolicy policy = NetworkSyncRemoteCapabilityPolicy.NegotiateWhenAvailable)
        {
            return new ShooterClientSyncAssemblyOptions(
                SyncProfile,
                ProfileName,
                ProfileCatalog,
                Decoder,
                InterpolationConfig,
                AvailableCapabilities,
                MinimumSchemaVersion,
                MaximumSchemaVersion,
                remoteCapabilities,
                policy,
                ReliableEventCheckpointStore,
                ReliableEventCheckpointLifecycleOptions,
                SessionRecoveryOptions);
        }

        /// <summary>设置可靠事件检查点存储提供器，供重连或新客户端会话自动恢复。</summary>
        public ShooterClientSyncAssemblyOptions WithReliableEventCheckpointStore(
            IReliableEventCheckpointStore? checkpointStore)
        {
            return new ShooterClientSyncAssemblyOptions(
                SyncProfile,
                ProfileName,
                ProfileCatalog,
                Decoder,
                InterpolationConfig,
                AvailableCapabilities,
                MinimumSchemaVersion,
                MaximumSchemaVersion,
                RemoteCapabilities,
                RemoteCapabilityPolicy,
                checkpointStore,
                ReliableEventCheckpointLifecycleOptions,
                SessionRecoveryOptions);
        }

        /// <summary>设置可靠事件检查点的生命周期 flush 与失败处理策略。</summary>
        public ShooterClientSyncAssemblyOptions WithReliableEventCheckpointLifecycleOptions(
            ReliableEventCheckpointLifecycleOptions? lifecycleOptions)
        {
            return new ShooterClientSyncAssemblyOptions(
                SyncProfile,
                ProfileName,
                ProfileCatalog,
                Decoder,
                InterpolationConfig,
                AvailableCapabilities,
                MinimumSchemaVersion,
                MaximumSchemaVersion,
                RemoteCapabilities,
                RemoteCapabilityPolicy,
                ReliableEventCheckpointStore,
                lifecycleOptions,
                SessionRecoveryOptions);
        }

        /// <summary>设置统一会话恢复协调器的策略、升级和诊断选项。</summary>
        public ShooterClientSyncAssemblyOptions WithSessionRecoveryOptions(
            NetworkSessionRecoveryOptions? recoveryOptions)
        {
            return new ShooterClientSyncAssemblyOptions(
                SyncProfile,
                ProfileName,
                ProfileCatalog,
                Decoder,
                InterpolationConfig,
                AvailableCapabilities,
                MinimumSchemaVersion,
                MaximumSchemaVersion,
                RemoteCapabilities,
                RemoteCapabilityPolicy,
                ReliableEventCheckpointStore,
                ReliableEventCheckpointLifecycleOptions,
                recoveryOptions);
        }

    }

#if UNITY_5_3_OR_NEWER
    /// <summary>
    /// Shooter 示例的 Unity 检查点后端集合。默认后端使用可后台写入的持久化文件；
    /// PlayerPrefs 后端保留在 Unity 主线程执行，并仅在 flush 时调用磁盘保存。
    /// </summary>
    public static class ShooterUnityReliableEventCheckpointStores
    {
        private const string DefaultPlayerPrefsPrefix = "abilitykit.shooter.reliable-event.";
        private static readonly object Gate = new object();
        private static IReliableEventCheckpointStore? _defaultStore;

        /// <summary>获取跨重连会话复用的默认持久化缓冲存储。</summary>
        public static IReliableEventCheckpointStore Default
        {
            get
            {
                lock (Gate)
                {
                    return _defaultStore ??= new BufferedReliableEventCheckpointStore(
                        new FileReliableEventCheckpointStore(
                            System.IO.Path.Combine(
                                UnityEngine.Application.persistentDataPath,
                                "abilitykit-shooter-reliable-events.chk")));
                }
            }
        }

        /// <summary>创建 PlayerPrefs 后端；调用方必须从 Unity 主线程访问该实例。</summary>
        public static IReliableEventCheckpointStore CreatePlayerPrefs(
            string keyPrefix = DefaultPlayerPrefsPrefix)
        {
            if (string.IsNullOrWhiteSpace(keyPrefix))
            {
                throw new ArgumentException("PlayerPrefs 键前缀不能为空。", nameof(keyPrefix));
            }

            return new DelegatingReliableEventCheckpointStore(
                streamId => LoadPlayerPrefs(keyPrefix, streamId),
                checkpoint => SavePlayerPrefs(keyPrefix, checkpoint),
                streamId => RemovePlayerPrefs(keyPrefix, streamId),
                cancellationToken =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    UnityEngine.PlayerPrefs.Save();
                    return System.Threading.Tasks.Task.CompletedTask;
                });
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(
            UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetDefaultStore()
        {
            lock (Gate)
            {
                (_defaultStore as IDisposable)?.Dispose();
                _defaultStore = null;
            }
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(
            UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterApplicationQuit()
        {
            UnityEngine.Application.quitting -= DisposeDefaultStore;
            UnityEngine.Application.quitting += DisposeDefaultStore;
        }

        private static void DisposeDefaultStore()
        {
            lock (Gate)
            {
                (_defaultStore as IDisposable)?.Dispose();
                _defaultStore = null;
            }
        }

        private static ReliableEventCheckpoint? LoadPlayerPrefs(
            string prefix,
            string streamId)
        {
            if (string.IsNullOrWhiteSpace(streamId)) return null;
            var key = BuildPlayerPrefsKey(prefix, streamId);
            if (!UnityEngine.PlayerPrefs.HasKey(key)) return null;

            var fields = UnityEngine.PlayerPrefs.GetString(key, string.Empty).Split('|');
            if (fields.Length != 2 ||
                !long.TryParse(
                    fields[0],
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var sequence))
            {
                return null;
            }

            try
            {
                var timelineId = System.Text.Encoding.UTF8.GetString(
                    Convert.FromBase64String(fields[1]));
                var checkpoint = new ReliableEventCheckpoint(streamId, timelineId, sequence);
                return checkpoint.IsValid ? checkpoint : null;
            }
            catch (FormatException)
            {
                return null;
            }
        }

        private static void SavePlayerPrefs(
            string prefix,
            ReliableEventCheckpoint checkpoint)
        {
            var timeline = Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes(checkpoint.TimelineId));
            var value = checkpoint.LastAcknowledgedSequence.ToString(
                System.Globalization.CultureInfo.InvariantCulture) + "|" + timeline;
            UnityEngine.PlayerPrefs.SetString(
                BuildPlayerPrefsKey(prefix, checkpoint.StreamId),
                value);
        }

        private static bool RemovePlayerPrefs(string prefix, string streamId)
        {
            if (string.IsNullOrWhiteSpace(streamId)) return false;
            var key = BuildPlayerPrefsKey(prefix, streamId);
            if (!UnityEngine.PlayerPrefs.HasKey(key)) return false;
            UnityEngine.PlayerPrefs.DeleteKey(key);
            return true;
        }

        private static string BuildPlayerPrefsKey(string prefix, string streamId)
        {
            var encodedStream = Convert.ToBase64String(
                    System.Text.Encoding.UTF8.GetBytes(streamId))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
            return prefix + encodedStream;
        }
    }
#endif
}
