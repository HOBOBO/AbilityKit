namespace AbilityKit.Network.Runtime.Sync
{
    public static class NetworkSyncProfiles
    {
        public static NetworkSyncProfile Unspecified { get; } = new NetworkSyncProfile(
            NetworkSyncModel.Unspecified,
            ClientPlaybackPolicy.None,
            InputPolicy.None,
            SnapshotPolicy.None,
            InterestPolicy.None,
            RecoveryPolicy.None,
            ServerValidationPolicy.None);

        public static NetworkSyncProfile Lockstep { get; } = new NetworkSyncProfile(
            NetworkSyncModel.Lockstep,
            ClientPlaybackPolicy.None,
            InputPolicy.DeterministicBroadcast,
            SnapshotPolicy.None,
            InterestPolicy.AllEntities,
            RecoveryPolicy.None,
            ServerValidationPolicy.ClientHashAudit);

        public static NetworkSyncProfile PredictRollback { get; } = new NetworkSyncProfile(
            NetworkSyncModel.PredictRollback,
            ClientPlaybackPolicy.PredictRollback,
            InputPolicy.ImmediateSubmit | InputPolicy.ServerRemapAcceptedFrame,
            SnapshotPolicy.FullSnapshot | SnapshotPolicy.AuthorityOverride | SnapshotPolicy.EventStream,
            InterestPolicy.AllEntities,
            RecoveryPolicy.CatchUpToServerFrame | RecoveryPolicy.RequestFullSnapshot,
            ServerValidationPolicy.AuthoritativeOnly,
            ReliableEventPolicy.OrderedDelivery | ReliableEventPolicy.ExternalAcknowledgement |
            ReliableEventPolicy.PersistentCheckpoint | ReliableEventPolicy.BufferedOutOfOrder |
            ReliableEventPolicy.AuthoritativeBaselineRecovery);

        public static NetworkSyncProfile AuthoritativeInterpolation { get; } = new NetworkSyncProfile(
            NetworkSyncModel.AuthoritativeInterpolation,
            ClientPlaybackPolicy.AuthoritativeInterpolation,
            InputPolicy.NoClientInput,
            SnapshotPolicy.FixedRateStateStream | SnapshotPolicy.EventStream,
            InterestPolicy.AllEntities,
            RecoveryPolicy.RequestKeyFrame | RecoveryPolicy.RequestFullSnapshot,
            ServerValidationPolicy.AuthoritativeOnly,
            ReliableEventPolicy.OrderedDelivery | ReliableEventPolicy.ExternalAcknowledgement |
            ReliableEventPolicy.PersistentCheckpoint | ReliableEventPolicy.BufferedOutOfOrder |
            ReliableEventPolicy.AuthoritativeBaselineRecovery);

        public static NetworkSyncProfile BatchStateSync { get; } = new NetworkSyncProfile(
            NetworkSyncModel.BatchStateSync,
            ClientPlaybackPolicy.AuthoritativeInterpolation,
            InputPolicy.NoClientInput,
            SnapshotPolicy.BatchSnapshot | SnapshotPolicy.KeyFrameSnapshot | SnapshotPolicy.EventStream,
            InterestPolicy.AllEntities,
            RecoveryPolicy.RequestKeyFrame | RecoveryPolicy.RequestFullSnapshot,
            ServerValidationPolicy.AuthoritativeOnly,
            ReliableEventPolicy.OrderedDelivery | ReliableEventPolicy.ExternalAcknowledgement |
            ReliableEventPolicy.PersistentCheckpoint | ReliableEventPolicy.BufferedOutOfOrder |
            ReliableEventPolicy.AuthoritativeBaselineRecovery);

        public static NetworkSyncProfile MassBattleLodSync { get; } = new NetworkSyncProfile(
            NetworkSyncModel.MassBattleLodSync,
            ClientPlaybackPolicy.AuthoritativeInterpolation,
            InputPolicy.ImmediateSubmit,
            SnapshotPolicy.BatchSnapshot | SnapshotPolicy.KeyFrameSnapshot | SnapshotPolicy.EventStream,
            InterestPolicy.DistanceAoi | InterestPolicy.TeamOrFactionAoi | InterestPolicy.PriorityBudget | InterestPolicy.LodFrequency,
            RecoveryPolicy.RequestAoiSlice,
            ServerValidationPolicy.AuthoritativeOnly | ServerValidationPolicy.InputValidation,
            ReliableEventPolicy.OrderedDelivery | ReliableEventPolicy.ExternalAcknowledgement |
            ReliableEventPolicy.PersistentCheckpoint | ReliableEventPolicy.BufferedOutOfOrder);

        public static NetworkSyncProfile HybridHeroPrediction { get; } = new NetworkSyncProfile(
            NetworkSyncModel.HybridHeroPrediction,
            ClientPlaybackPolicy.HybridLocalPredictRemoteInterpolate,
            InputPolicy.ImmediateSubmit | InputPolicy.ServerRemapAcceptedFrame,
            SnapshotPolicy.FullSnapshot | SnapshotPolicy.FixedRateStateStream | SnapshotPolicy.BatchSnapshot | SnapshotPolicy.EventStream,
            InterestPolicy.OwnerRelevant | InterestPolicy.DistanceAoi | InterestPolicy.PriorityBudget,
            RecoveryPolicy.RequestFullSnapshot | RecoveryPolicy.RequestKeyFrame | RecoveryPolicy.RequestAoiSlice,
            ServerValidationPolicy.AuthoritativeOnly | ServerValidationPolicy.InputValidation,
            ReliableEventPolicy.OrderedDelivery | ReliableEventPolicy.ExternalAcknowledgement |
            ReliableEventPolicy.PersistentCheckpoint | ReliableEventPolicy.BufferedOutOfOrder |
            ReliableEventPolicy.AuthoritativeBaselineRecovery);

        public static NetworkSyncProfile FastReconnect { get; } = new NetworkSyncProfile(
            NetworkSyncModel.FastReconnect,
            ClientPlaybackPolicy.None,
            InputPolicy.None,
            SnapshotPolicy.FullSnapshot | SnapshotPolicy.KeyFrameSnapshot,
            InterestPolicy.AllEntities,
            RecoveryPolicy.ReconnectResume | RecoveryPolicy.RequestFullSnapshot,
            ServerValidationPolicy.AuthoritativeOnly);

        public static NetworkSyncProfile ServerRewindLagCompensation { get; } = new NetworkSyncProfile(
            NetworkSyncModel.ServerRewindLagCompensation,
            ClientPlaybackPolicy.None,
            InputPolicy.ImmediateSubmit | InputPolicy.ServerRemapAcceptedFrame,
            SnapshotPolicy.None,
            InterestPolicy.None,
            RecoveryPolicy.None,
            ServerValidationPolicy.LagCompensatedHitValidation);

        /// <summary>
        /// 为旧兼容模型解析规范 profile。映射现在由 <see cref="NetworkSyncProfileRegistry"/> 统一持有
        /// （审计迁移步骤 6 的枚举收敛），因此 model→profile 关系只存在于一个位置，而不是在这里再复制一份
        /// 手写 switch。该方法作为既有调用点保留。
        /// </summary>
        public static NetworkSyncProfile FromCompatibilityModel(NetworkSyncModel compatibilityModel)
        {
            return NetworkSyncProfileRegistry.Resolve(compatibilityModel);
        }
    }
}
