#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Network.Runtime.Sync
{
    /// <summary>配置问题的严重级别。</summary>
    public enum NetworkSyncConfigurationSeverity
    {
        /// <summary>不会阻止启动，但值得向诊断系统报告。</summary>
        Warning = 0,

        /// <summary>配置无法安全运行，必须在启动前修正。</summary>
        Error = 1
    }

    /// <summary>稳定的配置问题代码，供日志、测试和编辑器工具使用。</summary>
    public enum NetworkSyncConfigurationIssueCode
    {
        /// <summary>客户端回放策略不是已知枚举值。</summary>
        UnknownClientPlaybackPolicy = 0,
        /// <summary>输入策略包含未知标志位。</summary>
        UnknownInputPolicyBits = 1,
        /// <summary>快照策略包含未知标志位。</summary>
        UnknownSnapshotPolicyBits = 2,
        /// <summary>兴趣管理策略包含未知标志位。</summary>
        UnknownInterestPolicyBits = 3,
        /// <summary>恢复策略包含未知标志位。</summary>
        UnknownRecoveryPolicyBits = 4,
        /// <summary>服务器校验策略包含未知标志位。</summary>
        UnknownServerValidationPolicyBits = 5,
        /// <summary>输入策略组合互相冲突。</summary>
        ConflictingInputPolicies = 6,
        /// <summary>兴趣管理策略组合互相冲突。</summary>
        ConflictingInterestPolicies = 7,
        /// <summary>预测回放缺少输入提交策略。</summary>
        PredictionRequiresSubmittedInput = 8,
        /// <summary>插值回放缺少状态快照流。</summary>
        InterpolationRequiresSnapshotStream = 9,
        /// <summary>快照恢复策略缺少状态快照流。</summary>
        RecoveryRequiresSnapshotStream = 10,
        /// <summary>AOI 恢复缺少选择性兴趣管理策略。</summary>
        AoiRecoveryRequiresSelectiveInterest = 11,
        /// <summary>服务器输入校验缺少输入提交策略。</summary>
        InputValidationRequiresSubmittedInput = 12,
        /// <summary>协议版本范围无效。</summary>
        InvalidSchemaVersionRange = 13,
        /// <summary>能力协商缺少要求 Profile。</summary>
        MissingRequiredProfile = 14,
        /// <summary>提供方缺少客户端回放能力。</summary>
        MissingClientPlaybackCapability = 15,
        /// <summary>提供方缺少输入策略能力。</summary>
        MissingInputCapabilities = 16,
        /// <summary>提供方缺少快照策略能力。</summary>
        MissingSnapshotCapabilities = 17,
        /// <summary>提供方缺少兴趣管理能力。</summary>
        MissingInterestCapabilities = 18,
        /// <summary>提供方缺少恢复能力。</summary>
        MissingRecoveryCapabilities = 19,
        /// <summary>提供方缺少服务器校验能力。</summary>
        MissingServerValidationCapabilities = 20,
        /// <summary>协议版本范围没有交集。</summary>
        SchemaVersionMismatch = 21,
        /// <summary>快照管线缺少信封映射。</summary>
        MissingEnvelopeFactory = 22,
        /// <summary>快照管线缺少应用处理器。</summary>
        MissingSnapshotApplyHandler = 23,
        /// <summary>客户端回放能力包含未知标志位。</summary>
        UnknownClientPlaybackCapabilityBits = 24,
        /// <summary>可靠事件策略包含未知标志位。</summary>
        UnknownReliableEventPolicyBits = 25,
        /// <summary>可靠事件能力包含未知标志位。</summary>
        UnknownReliableEventCapabilityBits = 26,
        /// <summary>一次会话同时选择了自动 ACK 与外置 ACK。</summary>
        ConflictingReliableEventAcknowledgementPolicies = 27,
        /// <summary>可靠事件 ACK 缺少有序交付。</summary>
        ReliableEventAcknowledgementRequiresOrderedDelivery = 28,
        /// <summary>可靠事件检查点缺少 ACK 所有权。</summary>
        ReliableEventCheckpointRequiresAcknowledgement = 29,
        /// <summary>乱序缓存缺少有序交付。</summary>
        ReliableEventBufferRequiresOrderedDelivery = 30,
        /// <summary>权威基线恢复缺少全量快照恢复能力。</summary>
        ReliableEventBaselineRecoveryRequiresSnapshotRecovery = 31,
        /// <summary>可靠事件策略缺少事件流。</summary>
        ReliableEventRequiresEventStream = 32,
        /// <summary>提供方缺少可靠事件交付能力。</summary>
        MissingReliableEventCapabilities = 33
    }

    /// <summary>一条结构化配置问题。</summary>
    public readonly struct NetworkSyncConfigurationIssue
    {
        internal NetworkSyncConfigurationIssue(
            NetworkSyncConfigurationIssueCode code,
            NetworkSyncConfigurationSeverity severity,
            string path,
            string message)
        {
            Code = code;
            Severity = severity;
            Path = path;
            Message = message;
        }

        /// <summary>稳定问题代码。</summary>
        public NetworkSyncConfigurationIssueCode Code { get; }

        /// <summary>问题严重级别。</summary>
        public NetworkSyncConfigurationSeverity Severity { get; }

        /// <summary>问题对应的配置路径。</summary>
        public string Path { get; }

        /// <summary>面向开发者的中文说明。</summary>
        public string Message { get; }
    }

    /// <summary>一次配置检查产生的不可变问题集合。</summary>
    public sealed class NetworkSyncConfigurationReport
    {
        private static readonly NetworkSyncConfigurationIssue[] EmptyIssues =
            Array.Empty<NetworkSyncConfigurationIssue>();
        private readonly IReadOnlyList<NetworkSyncConfigurationIssue> _issues;

        internal NetworkSyncConfigurationReport(List<NetworkSyncConfigurationIssue>? issues)
        {
            var snapshot = issues == null || issues.Count == 0 ? EmptyIssues : issues.ToArray();
            _issues = Array.AsReadOnly(snapshot);
            var errorCount = 0;
            for (var i = 0; i < snapshot.Length; i++)
            {
                if (snapshot[i].Severity == NetworkSyncConfigurationSeverity.Error)
                {
                    errorCount++;
                }
            }

            ErrorCount = errorCount;
        }

        /// <summary>全部配置问题的稳定快照。</summary>
        public IReadOnlyList<NetworkSyncConfigurationIssue> Issues => _issues;

        /// <summary>阻止启动的问题数量。</summary>
        public int ErrorCount { get; }

        /// <summary>配置是否不存在阻止启动的问题。</summary>
        public bool IsValid => ErrorCount == 0;

        /// <summary>报告无效时抛出包含完整报告的异常。</summary>
        public void ThrowIfInvalid(string? configurationName = null)
        {
            if (!IsValid)
            {
                throw new NetworkSyncConfigurationException(configurationName, this);
            }
        }
    }

    /// <summary>同步配置无法安全启动时抛出的异常。</summary>
    public sealed class NetworkSyncConfigurationException : InvalidOperationException
    {
        internal NetworkSyncConfigurationException(
            string? configurationName,
            NetworkSyncConfigurationReport report)
            : base(CreateMessage(configurationName, report))
        {
            Report = report;
        }

        /// <summary>包含全部配置问题的结构化报告。</summary>
        public NetworkSyncConfigurationReport Report { get; }

        private static string CreateMessage(string? configurationName, NetworkSyncConfigurationReport report)
        {
            var target = string.IsNullOrWhiteSpace(configurationName) ? "网络同步配置" : configurationName;
            return $"{target}包含 {report.ErrorCount} 个阻止启动的问题。";
        }
    }

    /// <summary>可由运行端提供的客户端回放能力集合。</summary>
    [Flags]
    public enum ClientPlaybackCapabilities
    {
        /// <summary>不提供客户端回放能力。</summary>
        None = 0,

        /// <summary>预测并在权威状态到达时回滚重演。</summary>
        PredictRollback = 1 << 0,

        /// <summary>对权威状态流进行延迟插值。</summary>
        AuthoritativeInterpolation = 1 << 1,

        /// <summary>保持最近权威状态。</summary>
        HoldLatest = 1 << 2,

        /// <summary>短时外推并在权威状态到达时纠正。</summary>
        ExtrapolateThenCorrect = 1 << 3,

        /// <summary>本地对象预测、远端对象插值的混合回放。</summary>
        HybridLocalPredictRemoteInterpolate = 1 << 4
    }

    /// <summary>运行端可提供的可靠事件交付能力上限。</summary>
    [Flags]
    public enum ReliableEventCapabilities
    {
        /// <summary>不提供可靠事件扩展能力。</summary>
        None = 0,
        /// <summary>支持按单调序列交付事件。</summary>
        OrderedDelivery = 1 << 0,
        /// <summary>支持由框架自动提交 ACK。</summary>
        AutomaticAcknowledgement = 1 << 1,
        /// <summary>支持由接入方显式提交 ACK。</summary>
        ExternalAcknowledgement = 1 << 2,
        /// <summary>支持保存并恢复已确认游标。</summary>
        PersistentCheckpoint = 1 << 3,
        /// <summary>支持在容量上限内缓存乱序事件。</summary>
        BufferedOutOfOrder = 1 << 4,
        /// <summary>支持通过权威状态基线恢复失效的事件时间线。</summary>
        AuthoritativeBaselineRecovery = 1 << 5
    }

    /// <summary>
    /// 一个运行端或接入模块实际提供的同步能力。它描述能力上限，不代表所有能力必须同时启用。
    /// </summary>
    public readonly struct NetworkSyncCapabilities
    {
        /// <summary>创建带协议版本范围的能力集合。</summary>
        public NetworkSyncCapabilities(
            int minimumSchemaVersion,
            int maximumSchemaVersion,
            ClientPlaybackCapabilities clientPlayback,
            InputPolicy input,
            SnapshotPolicy snapshot,
            InterestPolicy interest,
            RecoveryPolicy recovery,
            ServerValidationPolicy serverValidation)
            : this(
                minimumSchemaVersion,
                maximumSchemaVersion,
                clientPlayback,
                input,
                snapshot,
                interest,
                recovery,
                serverValidation,
                ReliableEventCapabilities.None)
        {
        }

        /// <summary>创建包含可靠事件能力的协议能力集合。</summary>
        public NetworkSyncCapabilities(
            int minimumSchemaVersion,
            int maximumSchemaVersion,
            ClientPlaybackCapabilities clientPlayback,
            InputPolicy input,
            SnapshotPolicy snapshot,
            InterestPolicy interest,
            RecoveryPolicy recovery,
            ServerValidationPolicy serverValidation,
            ReliableEventCapabilities reliableEvent)
        {
            MinimumSchemaVersion = minimumSchemaVersion;
            MaximumSchemaVersion = maximumSchemaVersion;
            ClientPlayback = clientPlayback;
            Input = input;
            Snapshot = snapshot;
            Interest = interest;
            Recovery = recovery;
            ServerValidation = serverValidation;
            ReliableEvent = reliableEvent;
        }

        /// <summary>提供方支持的最低协议结构版本。</summary>
        public int MinimumSchemaVersion { get; }
        /// <summary>提供方支持的最高协议结构版本。</summary>
        public int MaximumSchemaVersion { get; }
        /// <summary>提供的客户端回放能力。</summary>
        public ClientPlaybackCapabilities ClientPlayback { get; }
        /// <summary>提供的输入策略能力。</summary>
        public InputPolicy Input { get; }
        /// <summary>提供的快照策略能力。</summary>
        public SnapshotPolicy Snapshot { get; }
        /// <summary>提供的兴趣管理能力。</summary>
        public InterestPolicy Interest { get; }
        /// <summary>提供的恢复能力。</summary>
        public RecoveryPolicy Recovery { get; }
        /// <summary>提供的服务器校验能力。</summary>
        public ServerValidationPolicy ServerValidation { get; }
        /// <summary>提供的可靠事件交付能力。</summary>
        public ReliableEventCapabilities ReliableEvent { get; }

        /// <summary>根据单个 Profile 创建恰好覆盖其要求的能力集合。</summary>
        public static NetworkSyncCapabilities FromProfile(
            in NetworkSyncProfile profile,
            int minimumSchemaVersion,
            int maximumSchemaVersion)
        {
            return new NetworkSyncCapabilities(
                minimumSchemaVersion,
                maximumSchemaVersion,
                NetworkSyncConfigurationValidator.ToCapability(profile.ClientPlayback),
                profile.Input,
                profile.Snapshot,
                profile.Interest,
                profile.Recovery,
                profile.ServerValidation,
                NetworkSyncConfigurationValidator.ToCapability(profile.ReliableEvent));
        }
    }

    /// <summary>能力协商的版本交集与结构化诊断。</summary>
    public readonly struct NetworkSyncNegotiationResult
    {
        internal NetworkSyncNegotiationResult(
            int minimumSchemaVersion,
            int maximumSchemaVersion,
            NetworkSyncConfigurationReport report)
        {
            MinimumSchemaVersion = minimumSchemaVersion;
            MaximumSchemaVersion = maximumSchemaVersion;
            Report = report;
        }

        /// <summary>协商后的最低协议结构版本。</summary>
        public int MinimumSchemaVersion { get; }
        /// <summary>协商后的最高协议结构版本。</summary>
        public int MaximumSchemaVersion { get; }
        /// <summary>能力与版本诊断报告。</summary>
        public NetworkSyncConfigurationReport Report { get; }
        /// <summary>是否可以使用协商后的配置安全启动。</summary>
        public bool IsCompatible => Report.IsValid;
    }

    /// <summary>同步 Profile 规则检查与端点能力协商入口。</summary>
    public static class NetworkSyncConfigurationValidator
    {
        private const InputPolicy KnownInputPolicies = InputPolicy.NoClientInput |
            InputPolicy.ImmediateSubmit | InputPolicy.InputDelayBuffer |
            InputPolicy.ServerRemapAcceptedFrame | InputPolicy.DeterministicBroadcast;
        private const SnapshotPolicy KnownSnapshotPolicies = SnapshotPolicy.FullSnapshot |
            SnapshotPolicy.DeltaSnapshot | SnapshotPolicy.KeyFrameSnapshot |
            SnapshotPolicy.AuthorityOverride | SnapshotPolicy.FixedRateStateStream |
            SnapshotPolicy.BatchSnapshot | SnapshotPolicy.EventStream;
        private const InterestPolicy KnownInterestPolicies = InterestPolicy.AllEntities |
            InterestPolicy.OwnerRelevant | InterestPolicy.DistanceAoi |
            InterestPolicy.TeamOrFactionAoi | InterestPolicy.PriorityBudget |
            InterestPolicy.LodFrequency;
        private const RecoveryPolicy KnownRecoveryPolicies = RecoveryPolicy.RequestFullSnapshot |
            RecoveryPolicy.RequestKeyFrame | RecoveryPolicy.RequestAoiSlice |
            RecoveryPolicy.CatchUpToServerFrame | RecoveryPolicy.ReconnectResume;
        private const ServerValidationPolicy KnownServerValidationPolicies =
            ServerValidationPolicy.AuthoritativeOnly | ServerValidationPolicy.InputValidation |
            ServerValidationPolicy.LagCompensatedHitValidation |
            ServerValidationPolicy.ClientHashAudit | ServerValidationPolicy.AntiCheatEnvelope;
        private const ReliableEventPolicy KnownReliableEventPolicies =
            ReliableEventPolicy.OrderedDelivery |
            ReliableEventPolicy.AutomaticAcknowledgement |
            ReliableEventPolicy.ExternalAcknowledgement |
            ReliableEventPolicy.PersistentCheckpoint |
            ReliableEventPolicy.BufferedOutOfOrder |
            ReliableEventPolicy.AuthoritativeBaselineRecovery;
        private const ReliableEventCapabilities KnownReliableEventCapabilities =
            ReliableEventCapabilities.OrderedDelivery |
            ReliableEventCapabilities.AutomaticAcknowledgement |
            ReliableEventCapabilities.ExternalAcknowledgement |
            ReliableEventCapabilities.PersistentCheckpoint |
            ReliableEventCapabilities.BufferedOutOfOrder |
            ReliableEventCapabilities.AuthoritativeBaselineRecovery;
        private const SnapshotPolicy StateSnapshotPolicies = SnapshotPolicy.FullSnapshot |
            SnapshotPolicy.DeltaSnapshot | SnapshotPolicy.KeyFrameSnapshot |
            SnapshotPolicy.FixedRateStateStream | SnapshotPolicy.BatchSnapshot;
        private const InterestPolicy SelectiveInterestPolicies = InterestPolicy.OwnerRelevant |
            InterestPolicy.DistanceAoi | InterestPolicy.TeamOrFactionAoi |
            InterestPolicy.PriorityBudget | InterestPolicy.LodFrequency;
        private const InputPolicy SubmittedInputPolicies = InputPolicy.ImmediateSubmit |
            InputPolicy.InputDelayBuffer | InputPolicy.DeterministicBroadcast;

        /// <summary>检查单个 Profile 的内部一致性。</summary>
        public static NetworkSyncConfigurationReport ValidateProfile(in NetworkSyncProfile profile)
        {
            var issues = new List<NetworkSyncConfigurationIssue>();
            AppendProfileIssues(in profile, issues, "Profile");
            return new NetworkSyncConfigurationReport(issues);
        }

        /// <summary>检查一个能力声明的版本范围与所有策略标志位。</summary>
        public static NetworkSyncConfigurationReport ValidateCapabilities(in NetworkSyncCapabilities capabilities)
        {
            var issues = new List<NetworkSyncConfigurationIssue>();
            AppendCapabilityIssues(in capabilities, issues, "Capabilities");
            return new NetworkSyncConfigurationReport(issues);
        }

        /// <summary>检查要求 Profile、协议版本与提供方能力是否兼容。</summary>
        public static NetworkSyncNegotiationResult Negotiate(
            in NetworkSyncProfile requiredProfile,
            int requiredMinimumSchemaVersion,
            int requiredMaximumSchemaVersion,
            in NetworkSyncCapabilities available)
        {
            var issues = new List<NetworkSyncConfigurationIssue>();
            AppendProfileIssues(in requiredProfile, issues, "RequiredProfile");
            AppendVersionRangeIssues(
                requiredMinimumSchemaVersion,
                requiredMaximumSchemaVersion,
                issues,
                "RequiredSchemaVersions");
            AppendCapabilityIssues(in available, issues, "AvailableCapabilities");

            var minimum = Math.Max(requiredMinimumSchemaVersion, available.MinimumSchemaVersion);
            var maximum = Math.Min(requiredMaximumSchemaVersion, available.MaximumSchemaVersion);
            if (requiredMinimumSchemaVersion >= 0 &&
                requiredMaximumSchemaVersion >= requiredMinimumSchemaVersion &&
                available.MinimumSchemaVersion >= 0 &&
                available.MaximumSchemaVersion >= available.MinimumSchemaVersion &&
                maximum < minimum)
            {
                Add(issues, NetworkSyncConfigurationIssueCode.SchemaVersionMismatch,
                    "SchemaVersions", "要求的协议版本范围与提供方支持范围没有交集。");
            }

            var requiredPlayback = ToCapability(requiredProfile.ClientPlayback);
            if (!ContainsAll(available.ClientPlayback, requiredPlayback))
            {
                Add(issues, NetworkSyncConfigurationIssueCode.MissingClientPlaybackCapability,
                    "AvailableCapabilities.ClientPlayback", "提供方不支持 Profile 要求的客户端回放策略。");
            }

            AppendMissingFlags(issues, requiredProfile.Input, available.Input,
                NetworkSyncConfigurationIssueCode.MissingInputCapabilities, "AvailableCapabilities.Input");
            AppendMissingFlags(issues, requiredProfile.Snapshot, available.Snapshot,
                NetworkSyncConfigurationIssueCode.MissingSnapshotCapabilities, "AvailableCapabilities.Snapshot");
            AppendMissingFlags(issues, requiredProfile.Interest, available.Interest,
                NetworkSyncConfigurationIssueCode.MissingInterestCapabilities, "AvailableCapabilities.Interest");
            AppendMissingFlags(issues, requiredProfile.Recovery, available.Recovery,
                NetworkSyncConfigurationIssueCode.MissingRecoveryCapabilities, "AvailableCapabilities.Recovery");
            AppendMissingFlags(issues, requiredProfile.ServerValidation, available.ServerValidation,
                NetworkSyncConfigurationIssueCode.MissingServerValidationCapabilities,
                "AvailableCapabilities.ServerValidation");
            AppendMissingFlags(issues, ToCapability(requiredProfile.ReliableEvent), available.ReliableEvent,
                NetworkSyncConfigurationIssueCode.MissingReliableEventCapabilities,
                "AvailableCapabilities.ReliableEvent");

            return new NetworkSyncNegotiationResult(minimum, maximum, new NetworkSyncConfigurationReport(issues));
        }

        internal static ClientPlaybackCapabilities ToCapability(ClientPlaybackPolicy policy)
        {
            return policy switch
            {
                ClientPlaybackPolicy.None => ClientPlaybackCapabilities.None,
                ClientPlaybackPolicy.PredictRollback => ClientPlaybackCapabilities.PredictRollback,
                ClientPlaybackPolicy.AuthoritativeInterpolation => ClientPlaybackCapabilities.AuthoritativeInterpolation,
                ClientPlaybackPolicy.HoldLatest => ClientPlaybackCapabilities.HoldLatest,
                ClientPlaybackPolicy.ExtrapolateThenCorrect => ClientPlaybackCapabilities.ExtrapolateThenCorrect,
                ClientPlaybackPolicy.HybridLocalPredictRemoteInterpolate =>
                    ClientPlaybackCapabilities.HybridLocalPredictRemoteInterpolate,
                _ => ClientPlaybackCapabilities.None
            };
        }

        internal static ReliableEventCapabilities ToCapability(ReliableEventPolicy policy)
        {
            return (ReliableEventCapabilities)(int)policy;
        }

        internal static void AppendVersionRangeIssues(
            int minimum,
            int maximum,
            List<NetworkSyncConfigurationIssue> issues,
            string path)
        {
            if (minimum < 0 || maximum < minimum)
            {
                Add(issues, NetworkSyncConfigurationIssueCode.InvalidSchemaVersionRange,
                    path, "协议版本范围必须非负，并且最大版本不能小于最小版本。");
            }
        }

        internal static void Add(
            List<NetworkSyncConfigurationIssue> issues,
            NetworkSyncConfigurationIssueCode code,
            string path,
            string message,
            NetworkSyncConfigurationSeverity severity = NetworkSyncConfigurationSeverity.Error)
        {
            issues.Add(new NetworkSyncConfigurationIssue(code, severity, path, message));
        }

        internal static void AppendProfileIssues(
            in NetworkSyncProfile profile,
            List<NetworkSyncConfigurationIssue> issues,
            string path)
        {
            if (!Enum.IsDefined(typeof(ClientPlaybackPolicy), profile.ClientPlayback))
                Add(issues, NetworkSyncConfigurationIssueCode.UnknownClientPlaybackPolicy,
                    path + ".ClientPlayback", "客户端回放策略不是框架已知值。");
            AppendUnknownFlags(issues, profile.Input, KnownInputPolicies,
                NetworkSyncConfigurationIssueCode.UnknownInputPolicyBits, path + ".Input");
            AppendUnknownFlags(issues, profile.Snapshot, KnownSnapshotPolicies,
                NetworkSyncConfigurationIssueCode.UnknownSnapshotPolicyBits, path + ".Snapshot");
            AppendUnknownFlags(issues, profile.Interest, KnownInterestPolicies,
                NetworkSyncConfigurationIssueCode.UnknownInterestPolicyBits, path + ".Interest");
            AppendUnknownFlags(issues, profile.Recovery, KnownRecoveryPolicies,
                NetworkSyncConfigurationIssueCode.UnknownRecoveryPolicyBits, path + ".Recovery");
            AppendUnknownFlags(issues, profile.ServerValidation, KnownServerValidationPolicies,
                NetworkSyncConfigurationIssueCode.UnknownServerValidationPolicyBits, path + ".ServerValidation");
            AppendUnknownFlags(issues, profile.ReliableEvent, KnownReliableEventPolicies,
                NetworkSyncConfigurationIssueCode.UnknownReliableEventPolicyBits, path + ".ReliableEvent");

            if ((profile.Input & InputPolicy.NoClientInput) != 0 &&
                (profile.Input & ~InputPolicy.NoClientInput) != 0)
                Add(issues, NetworkSyncConfigurationIssueCode.ConflictingInputPolicies,
                    path + ".Input", "NoClientInput 不能与其他输入策略同时启用。");
            if ((profile.Interest & InterestPolicy.AllEntities) != 0 &&
                (profile.Interest & SelectiveInterestPolicies) != 0)
                Add(issues, NetworkSyncConfigurationIssueCode.ConflictingInterestPolicies,
                    path + ".Interest", "AllEntities 不能与选择性兴趣管理策略同时启用。");

            var predicts = profile.ClientPlayback == ClientPlaybackPolicy.PredictRollback ||
                profile.ClientPlayback == ClientPlaybackPolicy.HybridLocalPredictRemoteInterpolate;
            if (predicts && (profile.Input & SubmittedInputPolicies) == 0)
                Add(issues, NetworkSyncConfigurationIssueCode.PredictionRequiresSubmittedInput,
                    path + ".Input", "预测回放要求至少启用一种输入提交策略。");

            var interpolates = profile.ClientPlayback == ClientPlaybackPolicy.AuthoritativeInterpolation ||
                profile.ClientPlayback == ClientPlaybackPolicy.ExtrapolateThenCorrect ||
                profile.ClientPlayback == ClientPlaybackPolicy.HybridLocalPredictRemoteInterpolate;
            if (interpolates && (profile.Snapshot & StateSnapshotPolicies) == 0)
                Add(issues, NetworkSyncConfigurationIssueCode.InterpolationRequiresSnapshotStream,
                    path + ".Snapshot", "插值或外推回放要求至少启用一种状态快照流。");
            var snapshotRecoveryPolicies = RecoveryPolicy.RequestFullSnapshot |
                RecoveryPolicy.RequestKeyFrame | RecoveryPolicy.RequestAoiSlice |
                RecoveryPolicy.CatchUpToServerFrame;
            if ((profile.Recovery & snapshotRecoveryPolicies) != 0 &&
                (profile.Snapshot & StateSnapshotPolicies) == 0)
                Add(issues, NetworkSyncConfigurationIssueCode.RecoveryRequiresSnapshotStream,
                    path + ".Snapshot", "快照恢复策略要求 Profile 提供可重新建立状态的快照流。");
            if ((profile.Recovery & RecoveryPolicy.RequestAoiSlice) != 0 &&
                (profile.Interest & SelectiveInterestPolicies) == 0)
                Add(issues, NetworkSyncConfigurationIssueCode.AoiRecoveryRequiresSelectiveInterest,
                    path + ".Interest", "AOI 切片恢复要求至少启用一种选择性兴趣管理策略。");
            if ((profile.ServerValidation & ServerValidationPolicy.InputValidation) != 0 &&
                (profile.Input & SubmittedInputPolicies) == 0)
                Add(issues, NetworkSyncConfigurationIssueCode.InputValidationRequiresSubmittedInput,
                    path + ".Input", "服务器输入校验要求至少启用一种输入提交策略。");
            AppendReliableEventPolicyIssues(in profile, issues, path);
        }

        private static void AppendCapabilityIssues(
            in NetworkSyncCapabilities capabilities,
            List<NetworkSyncConfigurationIssue> issues,
            string path)
        {
            AppendVersionRangeIssues(
                capabilities.MinimumSchemaVersion,
                capabilities.MaximumSchemaVersion,
                issues,
                path + ".SchemaVersions");
            const ClientPlaybackCapabilities knownPlayback =
                ClientPlaybackCapabilities.PredictRollback |
                ClientPlaybackCapabilities.AuthoritativeInterpolation |
                ClientPlaybackCapabilities.HoldLatest |
                ClientPlaybackCapabilities.ExtrapolateThenCorrect |
                ClientPlaybackCapabilities.HybridLocalPredictRemoteInterpolate;
            AppendUnknownFlags(issues, capabilities.ClientPlayback, knownPlayback,
                NetworkSyncConfigurationIssueCode.UnknownClientPlaybackCapabilityBits,
                path + ".ClientPlayback");
            AppendUnknownFlags(issues, capabilities.Input, KnownInputPolicies,
                NetworkSyncConfigurationIssueCode.UnknownInputPolicyBits, path + ".Input");
            AppendUnknownFlags(issues, capabilities.Snapshot, KnownSnapshotPolicies,
                NetworkSyncConfigurationIssueCode.UnknownSnapshotPolicyBits, path + ".Snapshot");
            AppendUnknownFlags(issues, capabilities.Interest, KnownInterestPolicies,
                NetworkSyncConfigurationIssueCode.UnknownInterestPolicyBits, path + ".Interest");
            AppendUnknownFlags(issues, capabilities.Recovery, KnownRecoveryPolicies,
                NetworkSyncConfigurationIssueCode.UnknownRecoveryPolicyBits, path + ".Recovery");
            AppendUnknownFlags(issues, capabilities.ServerValidation, KnownServerValidationPolicies,
                NetworkSyncConfigurationIssueCode.UnknownServerValidationPolicyBits,
                path + ".ServerValidation");
            AppendUnknownFlags(issues, capabilities.ReliableEvent, KnownReliableEventCapabilities,
                NetworkSyncConfigurationIssueCode.UnknownReliableEventCapabilityBits,
                path + ".ReliableEvent");
            AppendReliableEventCapabilityIssues(in capabilities, issues, path);
        }

        private static void AppendReliableEventPolicyIssues(
            in NetworkSyncProfile profile,
            List<NetworkSyncConfigurationIssue> issues,
            string path)
        {
            var reliable = profile.ReliableEvent;
            var acknowledgements = ReliableEventPolicy.AutomaticAcknowledgement |
                ReliableEventPolicy.ExternalAcknowledgement;
            if ((reliable & acknowledgements) == acknowledgements)
                Add(issues, NetworkSyncConfigurationIssueCode.ConflictingReliableEventAcknowledgementPolicies,
                    path + ".ReliableEvent", "自动 ACK 与外置 ACK 只能为一次会话选择一种。");
            if ((reliable & acknowledgements) != 0 &&
                (reliable & ReliableEventPolicy.OrderedDelivery) == 0)
                Add(issues, NetworkSyncConfigurationIssueCode.ReliableEventAcknowledgementRequiresOrderedDelivery,
                    path + ".ReliableEvent", "可靠事件 ACK 要求启用有序交付。");
            if ((reliable & ReliableEventPolicy.PersistentCheckpoint) != 0 &&
                (reliable & acknowledgements) == 0)
                Add(issues, NetworkSyncConfigurationIssueCode.ReliableEventCheckpointRequiresAcknowledgement,
                    path + ".ReliableEvent", "持久化检查点必须明确由框架或接入方持有 ACK。");
            if ((reliable & ReliableEventPolicy.BufferedOutOfOrder) != 0 &&
                (reliable & ReliableEventPolicy.OrderedDelivery) == 0)
                Add(issues, NetworkSyncConfigurationIssueCode.ReliableEventBufferRequiresOrderedDelivery,
                    path + ".ReliableEvent", "乱序缓存必须与有序交付共同启用。");
            if (reliable != ReliableEventPolicy.None &&
                (profile.Snapshot & SnapshotPolicy.EventStream) == 0)
                Add(issues, NetworkSyncConfigurationIssueCode.ReliableEventRequiresEventStream,
                    path + ".Snapshot", "可靠事件交付要求 Profile 声明 EventStream。");
            if ((reliable & ReliableEventPolicy.AuthoritativeBaselineRecovery) != 0 &&
                ((profile.Recovery & RecoveryPolicy.RequestFullSnapshot) == 0 ||
                 (profile.Snapshot & StateSnapshotPolicies) == 0))
                Add(issues, NetworkSyncConfigurationIssueCode.ReliableEventBaselineRecoveryRequiresSnapshotRecovery,
                    path + ".Recovery", "权威基线恢复要求可请求并应用全量状态快照。");
        }

        private static void AppendReliableEventCapabilityIssues(
            in NetworkSyncCapabilities capabilities,
            List<NetworkSyncConfigurationIssue> issues,
            string path)
        {
            var reliable = capabilities.ReliableEvent;
            var acknowledgements = ReliableEventCapabilities.AutomaticAcknowledgement |
                ReliableEventCapabilities.ExternalAcknowledgement;
            if ((reliable & acknowledgements) != 0 &&
                (reliable & ReliableEventCapabilities.OrderedDelivery) == 0)
                Add(issues, NetworkSyncConfigurationIssueCode.ReliableEventAcknowledgementRequiresOrderedDelivery,
                    path + ".ReliableEvent", "可靠事件 ACK 能力要求同时提供有序交付能力。");
            if ((reliable & ReliableEventCapabilities.PersistentCheckpoint) != 0 &&
                (reliable & acknowledgements) == 0)
                Add(issues, NetworkSyncConfigurationIssueCode.ReliableEventCheckpointRequiresAcknowledgement,
                    path + ".ReliableEvent", "持久化检查点能力要求至少提供一种 ACK 所有权。");
            if ((reliable & ReliableEventCapabilities.BufferedOutOfOrder) != 0 &&
                (reliable & ReliableEventCapabilities.OrderedDelivery) == 0)
                Add(issues, NetworkSyncConfigurationIssueCode.ReliableEventBufferRequiresOrderedDelivery,
                    path + ".ReliableEvent", "乱序缓存能力要求同时提供有序交付能力。");
            if (reliable != ReliableEventCapabilities.None &&
                (capabilities.Snapshot & SnapshotPolicy.EventStream) == 0)
                Add(issues, NetworkSyncConfigurationIssueCode.ReliableEventRequiresEventStream,
                    path + ".Snapshot", "可靠事件能力要求同时提供 EventStream 能力。");
            if ((reliable & ReliableEventCapabilities.AuthoritativeBaselineRecovery) != 0 &&
                ((capabilities.Recovery & RecoveryPolicy.RequestFullSnapshot) == 0 ||
                 (capabilities.Snapshot & StateSnapshotPolicies) == 0))
                Add(issues, NetworkSyncConfigurationIssueCode.ReliableEventBaselineRecoveryRequiresSnapshotRecovery,
                    path + ".Recovery", "权威基线恢复能力要求提供全量状态快照恢复能力。");
        }

        private static void AppendUnknownFlags<T>(
            List<NetworkSyncConfigurationIssue> issues,
            T actual,
            T known,
            NetworkSyncConfigurationIssueCode code,
            string path)
            where T : struct, Enum
        {
            var actualBits = ToUInt64Bits(actual);
            var knownBits = ToUInt64Bits(known);
            if ((actualBits & ~knownBits) != 0)
                Add(issues, code, path, "策略集合包含框架未知的标志位。");
        }

        private static void AppendMissingFlags<T>(
            List<NetworkSyncConfigurationIssue> issues,
            T required,
            T available,
            NetworkSyncConfigurationIssueCode code,
            string path)
            where T : struct, Enum
        {
            var requiredBits = ToUInt64Bits(required);
            var availableBits = ToUInt64Bits(available);
            if ((availableBits & requiredBits) != requiredBits)
                Add(issues, code, path, "提供方能力未覆盖 Profile 要求的全部策略位。");
        }

        private static bool ContainsAll(ClientPlaybackCapabilities available, ClientPlaybackCapabilities required)
        {
            return (available & required) == required;
        }

        private static ulong ToUInt64Bits<T>(T value) where T : struct, Enum
        {
            return unchecked((ulong)Convert.ToInt64(value));
        }
    }
}
