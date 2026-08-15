using System;

namespace AbilityKit.Network.Runtime.Sync
{
    public enum ClientPlaybackPolicy
    {
        None = 0,
        PredictRollback = 1,
        AuthoritativeInterpolation = 2,
        HoldLatest = 3,
        ExtrapolateThenCorrect = 4,
        HybridLocalPredictRemoteInterpolate = 5
    }

    [Flags]
    public enum InputPolicy
    {
        None = 0,
        NoClientInput = 1 << 0,
        ImmediateSubmit = 1 << 1,
        InputDelayBuffer = 1 << 2,
        ServerRemapAcceptedFrame = 1 << 3,
        DeterministicBroadcast = 1 << 4
    }

    [Flags]
    public enum SnapshotPolicy
    {
        None = 0,
        FullSnapshot = 1 << 0,
        DeltaSnapshot = 1 << 1,
        KeyFrameSnapshot = 1 << 2,
        AuthorityOverride = 1 << 3,
        FixedRateStateStream = 1 << 4,
        BatchSnapshot = 1 << 5,
        EventStream = 1 << 6
    }

    [Flags]
    public enum InterestPolicy
    {
        None = 0,
        AllEntities = 1 << 0,
        OwnerRelevant = 1 << 1,
        DistanceAoi = 1 << 2,
        TeamOrFactionAoi = 1 << 3,
        PriorityBudget = 1 << 4,
        LodFrequency = 1 << 5
    }

    [Flags]
    public enum RecoveryPolicy
    {
        None = 0,
        RequestFullSnapshot = 1 << 0,
        RequestKeyFrame = 1 << 1,
        RequestAoiSlice = 1 << 2,
        CatchUpToServerFrame = 1 << 3,
        ReconnectResume = 1 << 4
    }

    [Flags]
    public enum ServerValidationPolicy
    {
        None = 0,
        AuthoritativeOnly = 1 << 0,
        InputValidation = 1 << 1,
        LagCompensatedHitValidation = 1 << 2,
        ClientHashAudit = 1 << 3,
        AntiCheatEnvelope = 1 << 4
    }

    /// <summary>一次同步会话实际启用的可靠事件交付策略。</summary>
    [Flags]
    public enum ReliableEventPolicy
    {
        /// <summary>不要求可靠事件交付。</summary>
        None = 0,
        /// <summary>事件必须按单调序列交付。</summary>
        OrderedDelivery = 1 << 0,
        /// <summary>由框架在业务处理成功后自动提交 ACK。</summary>
        AutomaticAcknowledgement = 1 << 1,
        /// <summary>由接入方在事务或业务提交完成后显式提交 ACK。</summary>
        ExternalAcknowledgement = 1 << 2,
        /// <summary>保存已确认游标，以便重连后继续消费。</summary>
        PersistentCheckpoint = 1 << 3,
        /// <summary>在容量上限内缓存先到达的乱序事件。</summary>
        BufferedOutOfOrder = 1 << 4,
        /// <summary>时间线或保留窗口失效后通过权威基线重新建立游标。</summary>
        AuthoritativeBaselineRecovery = 1 << 5
    }

    public readonly struct NetworkSyncProfile : IEquatable<NetworkSyncProfile>
    {
        public NetworkSyncProfile(
            NetworkSyncModel compatibilityModel,
            ClientPlaybackPolicy clientPlayback,
            InputPolicy input,
            SnapshotPolicy snapshot,
            InterestPolicy interest,
            RecoveryPolicy recovery,
            ServerValidationPolicy serverValidation)
            : this(
                compatibilityModel,
                clientPlayback,
                input,
                snapshot,
                interest,
                recovery,
                serverValidation,
                ReliableEventPolicy.None)
        {
        }

        /// <summary>创建包含可靠事件策略的同步 Profile。</summary>
        public NetworkSyncProfile(
            NetworkSyncModel compatibilityModel,
            ClientPlaybackPolicy clientPlayback,
            InputPolicy input,
            SnapshotPolicy snapshot,
            InterestPolicy interest,
            RecoveryPolicy recovery,
            ServerValidationPolicy serverValidation,
            ReliableEventPolicy reliableEvent)
        {
            CompatibilityModel = compatibilityModel;
            ClientPlayback = clientPlayback;
            Input = input;
            Snapshot = snapshot;
            Interest = interest;
            Recovery = recovery;
            ServerValidation = serverValidation;
            ReliableEvent = reliableEvent;
        }

        public NetworkSyncModel CompatibilityModel { get; }

        public ClientPlaybackPolicy ClientPlayback { get; }

        public InputPolicy Input { get; }

        public SnapshotPolicy Snapshot { get; }

        public InterestPolicy Interest { get; }

        public RecoveryPolicy Recovery { get; }

        public ServerValidationPolicy ServerValidation { get; }

        /// <summary>本次会话要求启用的可靠事件交付策略。</summary>
        public ReliableEventPolicy ReliableEvent { get; }

        public bool Equals(NetworkSyncProfile other)
        {
            return CompatibilityModel == other.CompatibilityModel &&
                   ClientPlayback == other.ClientPlayback &&
                   Input == other.Input &&
                   Snapshot == other.Snapshot &&
                   Interest == other.Interest &&
                   Recovery == other.Recovery &&
                   ServerValidation == other.ServerValidation &&
                   ReliableEvent == other.ReliableEvent;
        }

        public override bool Equals(object? obj)
        {
            return obj is NetworkSyncProfile other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                CompatibilityModel,
                ClientPlayback,
                Input,
                Snapshot,
                Interest,
                Recovery,
                ServerValidation,
                ReliableEvent);
        }

        public static bool operator ==(NetworkSyncProfile left, NetworkSyncProfile right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(NetworkSyncProfile left, NetworkSyncProfile right)
        {
            return !left.Equals(right);
        }
    }
}
