#nullable enable

using System;
using AbilityKit.Network.Runtime.Sync;

namespace AbilityKit.Network.Sdk
{
    /// <summary>同步会话如何处理远端端点的能力声明。</summary>
    public enum NetworkSyncRemoteCapabilityPolicy
    {
        /// <summary>仅执行本地启动预检，不读取远端能力。</summary>
        Ignore = 0,

        /// <summary>远端能力存在时参与协商，不存在时仍允许使用本地预检结果启动。</summary>
        NegotiateWhenAvailable = 1,

        /// <summary>远端必须提供能力声明，并与本地及 Profile 要求形成有效交集。</summary>
        Require = 2
    }

    /// <summary>
    /// 同步会话装配选项。接入方必须显式声明可用能力，框架不会默认假设支持全部策略。
    /// </summary>
    public sealed class NetworkSyncSessionOptions
    {
        /// <summary>用于按稳定名称解析 Profile 的目录。</summary>
        public NetworkSyncProfileCatalog ProfileCatalog { get; set; } =
            NetworkSyncProfileRegistry.DefaultCatalog;

        /// <summary>需要从目录解析的稳定 Profile 名称。</summary>
        public string? RequiredProfileName { get; set; }

        /// <summary>直接提供的 Profile；设置后优先于 <see cref="RequiredProfileName"/>。</summary>
        public NetworkSyncProfile? RequiredProfile { get; set; }

        /// <summary>会话要求的最低协议结构版本。</summary>
        public int RequiredMinimumSchemaVersion { get; set; }

        /// <summary>会话要求的最高协议结构版本。</summary>
        public int RequiredMaximumSchemaVersion { get; set; }

        /// <summary>本地接入模块实际提供的能力声明。</summary>
        public NetworkSyncCapabilities? AvailableCapabilities { get; set; }

        /// <summary>由握手、房间元数据或测试端点提供的远端能力声明。</summary>
        public NetworkSyncCapabilities? RemoteCapabilities { get; set; }

        /// <summary>远端能力是否参与本次启动协商。</summary>
        public NetworkSyncRemoteCapabilityPolicy RemoteCapabilityPolicy { get; set; }

        /// <summary>控制器缺失时用于诊断的主体名称。</summary>
        public string ControllerSubjectName { get; set; } = "同步控制器";

        internal NetworkSyncSessionOptions Snapshot()
        {
            if (ProfileCatalog == null) throw new ArgumentNullException(nameof(ProfileCatalog));

            var catalogSnapshot = ProfileCatalog.CreateMutableCopy();
            catalogSnapshot.Freeze();
            return new NetworkSyncSessionOptions
            {
                ProfileCatalog = catalogSnapshot,
                RequiredProfileName = RequiredProfileName,
                RequiredProfile = RequiredProfile,
                RequiredMinimumSchemaVersion = RequiredMinimumSchemaVersion,
                RequiredMaximumSchemaVersion = RequiredMaximumSchemaVersion,
                AvailableCapabilities = AvailableCapabilities,
                RemoteCapabilities = RemoteCapabilities,
                RemoteCapabilityPolicy = RemoteCapabilityPolicy,
                ControllerSubjectName = ControllerSubjectName
            };
        }
    }

    /// <summary>同步会话启动失败的稳定分类。</summary>
    public enum NetworkSyncSessionBuildFailureReason
    {
        /// <summary>没有选择必需的 Profile。</summary>
        MissingRequiredProfile = 0,

        /// <summary>没有声明接入模块实际提供的能力。</summary>
        MissingAvailableCapabilities = 1,

        /// <summary>所选 Profile 没有注册对应控制器。</summary>
        MissingControllerRegistration = 2,

        /// <summary>已注册的控制器工厂返回了空引用。</summary>
        ControllerFactoryReturnedNull = 3,

        /// <summary>当前策略要求远端能力，但远端没有提供声明。</summary>
        MissingRemoteCapabilities = 4
    }

    /// <summary>同步会话在创建控制器前无法完成装配时抛出的异常。</summary>
    public sealed class NetworkSyncSessionBuildException : InvalidOperationException
    {
        internal NetworkSyncSessionBuildException(
            NetworkSyncSessionBuildFailureReason reason,
            string message,
            NetworkSyncSessionDescriptor? descriptor = null)
            : base(message)
        {
            Reason = reason;
            Descriptor = descriptor;
        }

        /// <summary>可供日志、测试与编辑器工具稳定判断的失败原因。</summary>
        public NetworkSyncSessionBuildFailureReason Reason { get; }

        /// <summary>已完成能力协商时生成的会话描述；早期选项缺失时为空。</summary>
        public NetworkSyncSessionDescriptor? Descriptor { get; }
    }

    /// <summary>一次成功协商后形成的不可变同步会话描述。</summary>
    public sealed class NetworkSyncSessionDescriptor
    {
        internal NetworkSyncSessionDescriptor(
            string profileName,
            in NetworkSyncProfile profile,
            in NetworkSyncCapabilities localCapabilities,
            NetworkSyncCapabilities? remoteCapabilities,
            NetworkSyncRemoteCapabilityPolicy remoteCapabilityPolicy,
            in NetworkSyncNegotiationResult localNegotiation,
            NetworkSyncNegotiationResult? remoteNegotiation)
        {
            ProfileName = profileName;
            Profile = profile;
            LocalCapabilities = localCapabilities;
            RemoteCapabilities = remoteCapabilities;
            RemoteCapabilityPolicy = remoteCapabilityPolicy;
            LocalNegotiation = localNegotiation;
            RemoteNegotiation = remoteNegotiation;
        }

        /// <summary>用于配置与诊断的稳定 Profile 名称。</summary>
        public string ProfileName { get; }

        /// <summary>本次会话实际使用的 Profile 快照。</summary>
        public NetworkSyncProfile Profile { get; }

        /// <summary>装配时使用的本地能力声明快照。</summary>
        public NetworkSyncCapabilities LocalCapabilities { get; }

        /// <summary>装配时使用的本地能力声明快照；保留该名称用于兼容旧调用方。</summary>
        public NetworkSyncCapabilities AvailableCapabilities => LocalCapabilities;

        /// <summary>参与协商的远端能力声明；仅本地预检或远端未提供时为空。</summary>
        public NetworkSyncCapabilities? RemoteCapabilities { get; }

        /// <summary>本次会话采用的远端能力协商策略。</summary>
        public NetworkSyncRemoteCapabilityPolicy RemoteCapabilityPolicy { get; }

        /// <summary>Profile 要求与本地能力形成的协商结果。</summary>
        public NetworkSyncNegotiationResult LocalNegotiation { get; }

        /// <summary>本地交集继续与远端能力协商的结果；远端未参与时为空。</summary>
        public NetworkSyncNegotiationResult? RemoteNegotiation { get; }

        /// <summary>是否已使用远端能力完成双端协商。</summary>
        public bool IsRemoteNegotiated => RemoteNegotiation.HasValue;

        /// <summary>最终有效的协议版本交集与结构化校验报告。</summary>
        public NetworkSyncNegotiationResult Negotiation => RemoteNegotiation ?? LocalNegotiation;

        /// <summary>协商后的最低协议结构版本。</summary>
        public int MinimumSchemaVersion => Negotiation.MinimumSchemaVersion;

        /// <summary>协商后的最高协议结构版本。</summary>
        public int MaximumSchemaVersion => Negotiation.MaximumSchemaVersion;

        /// <summary>能力与版本校验报告。</summary>
        public NetworkSyncConfigurationReport ConfigurationReport => Negotiation.Report;
    }

    /// <summary>同步会话控制器及其协商描述。</summary>
    public sealed class NetworkSyncSessionBuildResult<TController>
    {
        internal NetworkSyncSessionBuildResult(
            TController controller,
            NetworkSyncSessionDescriptor descriptor)
        {
            Controller = controller;
            Descriptor = descriptor;
        }

        /// <summary>通过启动校验后创建的业务控制器。</summary>
        public TController Controller { get; }

        /// <summary>本次同步会话的 Profile、能力与版本协商结果。</summary>
        public NetworkSyncSessionDescriptor Descriptor { get; }
    }

    /// <summary>
    /// 统一完成 Profile 解析、能力协商、控制器注册检查和控制器创建。
    /// </summary>
    public sealed class NetworkSyncSessionBuilder<TController, TContext>
    {
        private readonly NetworkSyncProfileControllerRegistry<TController, TContext> _registry;
        private readonly NetworkSyncSessionOptions _options;

        public NetworkSyncSessionBuilder(
            NetworkSyncProfileControllerRegistry<TController, TContext> registry,
            NetworkSyncSessionOptions options)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            if (options == null) throw new ArgumentNullException(nameof(options));

            // 构建器持有选项快照，避免调用方后续修改导致一次装配过程前后不一致。
            _options = options.Snapshot();
        }

        /// <summary>校验全部启动前置条件，并在通过后创建控制器。</summary>
        public NetworkSyncSessionBuildResult<TController> Build(in TContext context)
        {
            if (!Enum.IsDefined(typeof(NetworkSyncRemoteCapabilityPolicy), _options.RemoteCapabilityPolicy))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(_options.RemoteCapabilityPolicy),
                    _options.RemoteCapabilityPolicy,
                    "远端能力协商策略不是框架已知值。");
            }

            var profile = ResolveProfile(out var profileName);
            if (!_options.AvailableCapabilities.HasValue)
            {
                throw new NetworkSyncSessionBuildException(
                    NetworkSyncSessionBuildFailureReason.MissingAvailableCapabilities,
                    $"同步 Profile '{profileName}' 缺少接入方能力声明。");
            }

            var localCapabilities = _options.AvailableCapabilities.Value;
            var localNegotiation = NetworkSyncConfigurationValidator.Negotiate(
                in profile,
                _options.RequiredMinimumSchemaVersion,
                _options.RequiredMaximumSchemaVersion,
                in localCapabilities);
            localNegotiation.Report.ThrowIfInvalid(profileName + " 本地能力");

            var remoteCapabilities = ResolveRemoteCapabilities();
            if (!remoteCapabilities.HasValue &&
                _options.RemoteCapabilityPolicy == NetworkSyncRemoteCapabilityPolicy.Require)
            {
                var localDescriptor = new NetworkSyncSessionDescriptor(
                    profileName,
                    in profile,
                    in localCapabilities,
                    remoteCapabilities: null,
                    _options.RemoteCapabilityPolicy,
                    in localNegotiation,
                    remoteNegotiation: null);
                throw new NetworkSyncSessionBuildException(
                    NetworkSyncSessionBuildFailureReason.MissingRemoteCapabilities,
                    $"同步 Profile '{profileName}' 要求远端能力声明，但握手或会话元数据未提供该声明。",
                    localDescriptor);
            }

            NetworkSyncNegotiationResult? remoteNegotiation = null;
            if (remoteCapabilities.HasValue)
            {
                var remote = remoteCapabilities.Value;
                var result = NetworkSyncConfigurationValidator.Negotiate(
                    in profile,
                    localNegotiation.MinimumSchemaVersion,
                    localNegotiation.MaximumSchemaVersion,
                    in remote);
                result.Report.ThrowIfInvalid(profileName + " 远端能力");
                remoteNegotiation = result;
            }

            var descriptor = new NetworkSyncSessionDescriptor(
                profileName,
                in profile,
                in localCapabilities,
                remoteCapabilities,
                _options.RemoteCapabilityPolicy,
                in localNegotiation,
                remoteNegotiation);

            if (!_registry.Supports(in profile))
            {
                var subjectName = string.IsNullOrWhiteSpace(_options.ControllerSubjectName)
                    ? "同步控制器"
                    : _options.ControllerSubjectName;
                throw new NetworkSyncSessionBuildException(
                    NetworkSyncSessionBuildFailureReason.MissingControllerRegistration,
                    $"Profile '{profileName}' 未注册{subjectName}。",
                    descriptor);
            }

            // 构造器异常保留原始类型与堆栈，便于定位接入方自身的启动错误。
            var controller = _registry.Create(in profile, in context, _options.ControllerSubjectName);
            if (controller is null)
            {
                throw new NetworkSyncSessionBuildException(
                    NetworkSyncSessionBuildFailureReason.ControllerFactoryReturnedNull,
                    $"Profile '{profileName}' 的控制器工厂返回了空引用。",
                    descriptor);
            }

            return new NetworkSyncSessionBuildResult<TController>(controller, descriptor);
        }

        private NetworkSyncCapabilities? ResolveRemoteCapabilities()
        {
            if (_options.RemoteCapabilityPolicy == NetworkSyncRemoteCapabilityPolicy.Ignore)
            {
                return null;
            }

            if (_options.RemoteCapabilities.HasValue)
            {
                return _options.RemoteCapabilities.Value;
            }

            return null;
        }

        private NetworkSyncProfile ResolveProfile(out string profileName)
        {
            if (_options.RequiredProfile.HasValue)
            {
                var profile = _options.RequiredProfile.Value;
                profileName = string.IsNullOrWhiteSpace(_options.RequiredProfileName)
                    ? profile.CompatibilityModel.ToString()
                    : _options.RequiredProfileName!;
                return profile;
            }

            if (string.IsNullOrWhiteSpace(_options.RequiredProfileName))
            {
                throw new NetworkSyncSessionBuildException(
                    NetworkSyncSessionBuildFailureReason.MissingRequiredProfile,
                    "同步会话必须按稳定名称或直接 Profile 指定必需能力。");
            }

            var catalog = _options.ProfileCatalog
                ?? throw new ArgumentNullException(nameof(_options.ProfileCatalog));
            profileName = _options.RequiredProfileName!;
            return catalog.Resolve(profileName);
        }
    }

    /// <summary>网络会话恢复协调器可以接收的框架级信号。</summary>
    public enum NetworkSessionRecoverySignalKind
    {
        /// <summary>未产生恢复信号；用于默认值和空决策。</summary>
        None = 0,

        /// <summary>连接已经中断。</summary>
        ConnectionLost = 1,

        /// <summary>连接层已经安排重连。</summary>
        ReconnectScheduled = 2,

        /// <summary>连接层已经耗尽重连次数。</summary>
        ReconnectExhausted = 3,

        /// <summary>状态同步链路要求重新获取完整权威快照。</summary>
        SnapshotResyncRequired = 4,

        /// <summary>可靠事件链路要求重新建立权威事件基线。</summary>
        ReliableEventResyncRequired = 5,

        /// <summary>可靠事件检查点持久化失败。</summary>
        CheckpointFlushFailed = 6,

        /// <summary>可靠事件检查点持久化熔断器已经打开。</summary>
        CheckpointCircuitOpen = 7,

        /// <summary>会话已经恢复到可正常推进状态。</summary>
        Recovered = 8,

        /// <summary>连接已经重新建立，但状态同步和可靠事件可能仍需继续恢复。</summary>
        ConnectionRestored = 9,

        /// <summary>连接层已经开始一次重连尝试。</summary>
        ReconnectAttemptStarted = 10,

        /// <summary>连接层报告了传输或协议异常。</summary>
        ConnectionError = 11
    }

    /// <summary>
    /// 框架建议接入方执行的恢复动作。协调器只生成决策，不直接执行网络或业务操作。
    /// </summary>
    public enum NetworkSessionRecoveryAction
    {
        /// <summary>仅记录信号，不要求立即执行恢复动作。</summary>
        None = 0,

        /// <summary>等待连接层完成已安排的重连。</summary>
        WaitForReconnect = 1,

        /// <summary>向权威端请求完整状态快照。</summary>
        RequestFullSnapshot = 2,

        /// <summary>通过权威状态重新建立可靠事件基线。</summary>
        RestoreReliableEventBaseline = 3,

        /// <summary>释放并重新创建当前网络会话。</summary>
        RebuildSession = 4,

        /// <summary>结束战斗会话并返回大厅。</summary>
        ReturnToLobby = 5,

        /// <summary>中止当前会话，并由产品层展示或上报不可恢复错误。</summary>
        AbortSession = 6
    }

    /// <summary>恢复信号因协调规则未被采纳时的稳定原因。</summary>
    public enum NetworkSessionRecoverySuppressionReason
    {
        /// <summary>信号仍处于去重时间窗内。</summary>
        Duplicate = 0,

        /// <summary>新动作的优先级低于当前动作。</summary>
        LowerPriority = 1,

        /// <summary>选项禁止在已有动作上继续升级。</summary>
        EscalationDisabled = 2,

        /// <summary>选项禁止同优先级动作替换。</summary>
        EqualPriorityReplacementDisabled = 3
    }

    /// <summary>由连接、同步、可靠事件或持久化模块上报的统一会话恢复信号。</summary>
    public readonly struct NetworkSessionRecoverySignal
    {
        /// <summary>创建一个会话恢复信号。</summary>
        public NetworkSessionRecoverySignal(
            NetworkSessionRecoverySignalKind kind,
            SyncHealthSeverity severity = SyncHealthSeverity.Warning,
            int frame = 0,
            Exception? exception = null,
            string? correlationContext = null,
            string? detail = null)
        {
            Kind = kind;
            Severity = severity;
            Frame = frame;
            Exception = exception;
            CorrelationContext = correlationContext ?? string.Empty;
            Detail = detail ?? string.Empty;
        }

        /// <summary>信号分类。</summary>
        public NetworkSessionRecoverySignalKind Kind { get; }

        /// <summary>信号严重程度。</summary>
        public SyncHealthSeverity Severity { get; }

        /// <summary>信号关联的模拟帧；不适用时为 0。</summary>
        public int Frame { get; }

        /// <summary>触发信号的异常；没有异常时为 <c>null</c>。</summary>
        public Exception? Exception { get; }

        /// <summary>用于区分连接、战斗或数据流的稳定关联上下文。</summary>
        public string CorrelationContext { get; }

        /// <summary>供日志和诊断展示的补充说明。</summary>
        public string Detail { get; }
    }

    /// <summary>恢复策略针对一个信号给出的动作指令。</summary>
    public readonly struct NetworkSessionRecoveryDirective
    {
        /// <summary>创建恢复动作指令。</summary>
        public NetworkSessionRecoveryDirective(
            NetworkSessionRecoveryAction action,
            int priority,
            bool terminatesCurrentSession,
            string? reason = null)
        {
            Action = action;
            Priority = priority;
            TerminatesCurrentSession = terminatesCurrentSession;
            Reason = reason ?? string.Empty;
        }

        /// <summary>建议执行的恢复动作。</summary>
        public NetworkSessionRecoveryAction Action { get; }

        /// <summary>动作优先级；值越大，越允许覆盖已有决策。</summary>
        public int Priority { get; }

        /// <summary>执行该动作前是否应停止推进当前会话。</summary>
        public bool TerminatesCurrentSession { get; }

        /// <summary>供接入方记录或展示的决策原因。</summary>
        public string Reason { get; }
    }

    /// <summary>将统一恢复信号映射为项目可执行动作的策略扩展点。</summary>
    public interface INetworkSessionRecoveryPolicy
    {
        /// <summary>评估信号并返回动作指令。</summary>
        NetworkSessionRecoveryDirective Evaluate(in NetworkSessionRecoverySignal signal);
    }

    /// <summary>
    /// 基于规则表的恢复策略。项目可以替换单条规则，也可以直接实现
    /// <see cref="INetworkSessionRecoveryPolicy"/> 表达上下文相关策略。
    /// </summary>
    public sealed class NetworkSessionRecoveryRulePolicy : INetworkSessionRecoveryPolicy
    {
        private readonly System.Collections.Generic.Dictionary<NetworkSessionRecoverySignalKind, NetworkSessionRecoveryDirective> _rules;

        /// <summary>创建包含框架推荐规则的策略。</summary>
        public NetworkSessionRecoveryRulePolicy()
        {
            _rules = CreateDefaultRules();
        }

        private NetworkSessionRecoveryRulePolicy(
            System.Collections.Generic.Dictionary<NetworkSessionRecoverySignalKind, NetworkSessionRecoveryDirective> rules)
        {
            _rules = rules;
        }

        /// <summary>设置或替换一个信号的动作规则。</summary>
        public NetworkSessionRecoveryRulePolicy SetRule(
            NetworkSessionRecoverySignalKind kind,
            in NetworkSessionRecoveryDirective directive)
        {
            if (!Enum.IsDefined(typeof(NetworkSessionRecoverySignalKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "恢复信号不是框架已知值。");
            if (kind == NetworkSessionRecoverySignalKind.None)
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "不能为空恢复信号配置规则。");
            ValidateDirective(in directive);
            _rules[kind] = directive;
            return this;
        }

        /// <inheritdoc />
        public NetworkSessionRecoveryDirective Evaluate(in NetworkSessionRecoverySignal signal)
        {
            return _rules.TryGetValue(signal.Kind, out var directive)
                ? directive
                : new NetworkSessionRecoveryDirective(
                    NetworkSessionRecoveryAction.None,
                    priority: 0,
                    terminatesCurrentSession: false,
                    reason: "未配置恢复规则。");
        }

        internal NetworkSessionRecoveryRulePolicy Snapshot()
        {
            return new NetworkSessionRecoveryRulePolicy(
                new System.Collections.Generic.Dictionary<NetworkSessionRecoverySignalKind, NetworkSessionRecoveryDirective>(_rules));
        }

        internal static void ValidateDirective(in NetworkSessionRecoveryDirective directive)
        {
            if (!Enum.IsDefined(typeof(NetworkSessionRecoveryAction), directive.Action))
                throw new ArgumentOutOfRangeException(nameof(directive), directive.Action, "恢复动作不是框架已知值。");
            if (directive.Priority < 0)
                throw new ArgumentOutOfRangeException(nameof(directive), directive.Priority, "恢复动作优先级不能小于 0。");
        }

        private static System.Collections.Generic.Dictionary<NetworkSessionRecoverySignalKind, NetworkSessionRecoveryDirective> CreateDefaultRules()
        {
            return new System.Collections.Generic.Dictionary<NetworkSessionRecoverySignalKind, NetworkSessionRecoveryDirective>
            {
                [NetworkSessionRecoverySignalKind.ConnectionLost] = new NetworkSessionRecoveryDirective(
                    NetworkSessionRecoveryAction.WaitForReconnect, 10, false, "连接已中断，等待连接层恢复。"),
                [NetworkSessionRecoverySignalKind.ReconnectScheduled] = new NetworkSessionRecoveryDirective(
                    NetworkSessionRecoveryAction.WaitForReconnect, 10, false, "连接层已经安排重连。"),
                [NetworkSessionRecoverySignalKind.ReconnectExhausted] = new NetworkSessionRecoveryDirective(
                    NetworkSessionRecoveryAction.RebuildSession, 60, true, "自动重连已耗尽，需要重建会话。"),
                [NetworkSessionRecoverySignalKind.SnapshotResyncRequired] = new NetworkSessionRecoveryDirective(
                    NetworkSessionRecoveryAction.RequestFullSnapshot, 30, false, "增量状态无法继续，需要完整权威快照。"),
                [NetworkSessionRecoverySignalKind.ReliableEventResyncRequired] = new NetworkSessionRecoveryDirective(
                    NetworkSessionRecoveryAction.RestoreReliableEventBaseline, 40, false, "可靠事件游标失效，需要权威基线。"),
                [NetworkSessionRecoverySignalKind.CheckpointFlushFailed] = new NetworkSessionRecoveryDirective(
                    NetworkSessionRecoveryAction.None, 5, false, "检查点持久化失败，保留诊断并等待后续重试。"),
                [NetworkSessionRecoverySignalKind.CheckpointCircuitOpen] = new NetworkSessionRecoveryDirective(
                    NetworkSessionRecoveryAction.RebuildSession, 60, true, "检查点持久化持续失败并触发熔断。"),
                [NetworkSessionRecoverySignalKind.Recovered] = new NetworkSessionRecoveryDirective(
                    NetworkSessionRecoveryAction.None, 0, false, "会话已经恢复。"),
                [NetworkSessionRecoverySignalKind.ConnectionRestored] = new NetworkSessionRecoveryDirective(
                    NetworkSessionRecoveryAction.None, 11, false, "连接已经恢复，继续等待同步层确认会话状态。"),
                [NetworkSessionRecoverySignalKind.ReconnectAttemptStarted] = new NetworkSessionRecoveryDirective(
                    NetworkSessionRecoveryAction.WaitForReconnect, 10, false, "连接层正在执行重连尝试。"),
                [NetworkSessionRecoverySignalKind.ConnectionError] = new NetworkSessionRecoveryDirective(
                    NetworkSessionRecoveryAction.WaitForReconnect, 10, false, "连接层报告异常，等待连接恢复策略处理。")
            };
        }
    }

    /// <summary>会话恢复决策的诊断接收器。</summary>
    public interface INetworkSessionRecoveryDiagnosticsSink
    {
        /// <summary>协调器采纳并发布决策时调用。</summary>
        void OnDecisionPublished(in NetworkSessionRecoveryDecision decision);

        /// <summary>信号被去重或升级规则抑制时调用。</summary>
        void OnSignalSuppressed(
            in NetworkSessionRecoverySignal signal,
            NetworkSessionRecoverySuppressionReason reason);
    }

    /// <summary>使用委托快速接入会话恢复诊断。</summary>
    public sealed class DelegatingNetworkSessionRecoveryDiagnosticsSink : INetworkSessionRecoveryDiagnosticsSink
    {
        private readonly Action<NetworkSessionRecoveryDecision>? _decision;
        private readonly Action<NetworkSessionRecoverySignal, NetworkSessionRecoverySuppressionReason>? _suppressed;

        /// <summary>创建委托式诊断接收器。</summary>
        public DelegatingNetworkSessionRecoveryDiagnosticsSink(
            Action<NetworkSessionRecoveryDecision>? decision = null,
            Action<NetworkSessionRecoverySignal, NetworkSessionRecoverySuppressionReason>? suppressed = null)
        {
            _decision = decision;
            _suppressed = suppressed;
        }

        /// <inheritdoc />
        public void OnDecisionPublished(in NetworkSessionRecoveryDecision decision)
        {
            _decision?.Invoke(decision);
        }

        /// <inheritdoc />
        public void OnSignalSuppressed(
            in NetworkSessionRecoverySignal signal,
            NetworkSessionRecoverySuppressionReason reason)
        {
            _suppressed?.Invoke(signal, reason);
        }
    }

    /// <summary>会话恢复协调器选项。</summary>
    public sealed class NetworkSessionRecoveryOptions
    {
        /// <summary>相同分类和关联上下文信号的去重时间窗。</summary>
        public TimeSpan DuplicateSignalWindow { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>是否允许更高优先级动作覆盖当前动作。</summary>
        public bool AllowActionEscalation { get; set; } = true;

        /// <summary>是否允许同优先级的新决策替换当前决策。</summary>
        public bool AllowEqualPriorityReplacement { get; set; }

        /// <summary>去重索引最多保留的不同信号数量。</summary>
        public int MaximumTrackedSignals { get; set; } = 128;

        /// <summary>将信号映射到动作的策略。</summary>
        public INetworkSessionRecoveryPolicy Policy { get; set; } = new NetworkSessionRecoveryRulePolicy();

        /// <summary>可选的结构化诊断接收器。</summary>
        public INetworkSessionRecoveryDiagnosticsSink? DiagnosticsSink { get; set; }

        /// <summary>用于去重和决策时间戳的时钟；测试可以注入确定性时间。</summary>
        public Func<DateTimeOffset> UtcNowProvider { get; set; } = () => DateTimeOffset.UtcNow;

        internal NetworkSessionRecoveryOptions Snapshot()
        {
            if (DuplicateSignalWindow < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(DuplicateSignalWindow), DuplicateSignalWindow, "去重时间窗不能小于零。");
            if (MaximumTrackedSignals <= 0)
                throw new ArgumentOutOfRangeException(nameof(MaximumTrackedSignals), MaximumTrackedSignals, "去重索引容量必须大于零。");
            if (Policy == null) throw new ArgumentNullException(nameof(Policy));
            if (UtcNowProvider == null) throw new ArgumentNullException(nameof(UtcNowProvider));

            return new NetworkSessionRecoveryOptions
            {
                DuplicateSignalWindow = DuplicateSignalWindow,
                AllowActionEscalation = AllowActionEscalation,
                AllowEqualPriorityReplacement = AllowEqualPriorityReplacement,
                MaximumTrackedSignals = MaximumTrackedSignals,
                Policy = Policy is NetworkSessionRecoveryRulePolicy rules ? rules.Snapshot() : Policy,
                DiagnosticsSink = DiagnosticsSink,
                UtcNowProvider = UtcNowProvider
            };
        }
    }

    /// <summary>协调器针对一个信号发布的不可变恢复决策。</summary>
    public readonly struct NetworkSessionRecoveryDecision
    {
        /// <summary>创建恢复决策。</summary>
        public NetworkSessionRecoveryDecision(
            in NetworkSessionRecoverySignal signal,
            in NetworkSessionRecoveryDirective directive,
            DateTimeOffset decidedAt)
        {
            Signal = signal;
            Action = directive.Action;
            Priority = directive.Priority;
            TerminatesCurrentSession = directive.TerminatesCurrentSession;
            Reason = directive.Reason ?? string.Empty;
            DecidedAt = decidedAt;
        }

        /// <summary>触发当前决策的信号。</summary>
        public NetworkSessionRecoverySignal Signal { get; }

        /// <summary>建议接入方执行的动作。</summary>
        public NetworkSessionRecoveryAction Action { get; }

        /// <summary>当前决策的策略优先级。</summary>
        public int Priority { get; }

        /// <summary>执行动作前是否应停止推进当前会话。</summary>
        public bool TerminatesCurrentSession { get; }

        /// <summary>策略给出的决策原因。</summary>
        public string Reason { get; }

        /// <summary>协调器采纳决策的 UTC 时间。</summary>
        public DateTimeOffset DecidedAt { get; }

        /// <summary>当前是否包含需要执行的动作。</summary>
        public bool HasAction => Action != NetworkSessionRecoveryAction.None;

        /// <summary>当前是否包含已发布的决策，包括仅用于诊断的无动作决策。</summary>
        public bool HasDecision => Signal.Kind != NetworkSessionRecoverySignalKind.None;
    }

    /// <summary>会话恢复协调器的累计诊断快照。</summary>
    public readonly struct NetworkSessionRecoveryDiagnostics
    {
        internal NetworkSessionRecoveryDiagnostics(
            long receivedSignalCount,
            long publishedDecisionCount,
            long duplicateSignalCount,
            long prioritySuppressedCount,
            long resetCount,
            int trackedSignalCount)
        {
            ReceivedSignalCount = receivedSignalCount;
            PublishedDecisionCount = publishedDecisionCount;
            DuplicateSignalCount = duplicateSignalCount;
            PrioritySuppressedCount = prioritySuppressedCount;
            ResetCount = resetCount;
            TrackedSignalCount = trackedSignalCount;
        }

        /// <summary>收到的信号总数。</summary>
        public long ReceivedSignalCount { get; }

        /// <summary>采纳并发布的决策总数。</summary>
        public long PublishedDecisionCount { get; }

        /// <summary>在去重时间窗内被抑制的信号总数。</summary>
        public long DuplicateSignalCount { get; }

        /// <summary>因动作优先级或升级选项被抑制的信号总数。</summary>
        public long PrioritySuppressedCount { get; }

        /// <summary>通过恢复信号或显式调用完成的重置总数。</summary>
        public long ResetCount { get; }

        /// <summary>当前去重索引中的信号数量。</summary>
        public int TrackedSignalCount { get; }
    }

    /// <summary>
    /// 汇总连接、状态同步、可靠事件和检查点信号，并根据策略发布单一会话恢复决策。
    /// </summary>
    /// <summary>接收统一会话恢复信号的框架扩展点。</summary>
    public interface INetworkSessionRecoverySignalSink
    {
        /// <summary>尝试采纳信号并输出当前协调决策。</summary>
        bool TryReport(
            in NetworkSessionRecoverySignal signal,
            out NetworkSessionRecoveryDecision decision);
    }

    public sealed class NetworkSessionRecoveryCoordinator : INetworkSessionRecoverySignalSink
    {
        private readonly object _gate = new object();
        private readonly NetworkSessionRecoveryOptions _options;
        private readonly System.Collections.Generic.Dictionary<SignalKey, DateTimeOffset> _lastSignals =
            new System.Collections.Generic.Dictionary<SignalKey, DateTimeOffset>();
        private NetworkSessionRecoveryDecision _currentDecision;
        private long _receivedSignalCount;
        private long _publishedDecisionCount;
        private long _duplicateSignalCount;
        private long _prioritySuppressedCount;
        private long _resetCount;

        /// <summary>使用框架推荐选项创建协调器。</summary>
        public NetworkSessionRecoveryCoordinator(NetworkSessionRecoveryOptions? options = null)
        {
            _options = (options ?? new NetworkSessionRecoveryOptions()).Snapshot();
        }

        /// <summary>协调器采纳并发布新决策时触发。</summary>
        public event Action<NetworkSessionRecoveryDecision>? DecisionPublished;

        /// <summary>协调器被显式重置时触发，供执行生命周期同步取消旧动作。</summary>
        public event Action? ResetPerformed;

        /// <summary>读取当前尚未被恢复重置的最高优先级决策。</summary>
        public NetworkSessionRecoveryDecision CurrentDecision
        {
            get
            {
                lock (_gate) return _currentDecision;
            }
        }

        /// <summary>
        /// 上报信号。返回 <c>true</c> 表示信号形成了新决策；去重或优先级抑制时返回 <c>false</c>。
        /// </summary>
        public bool TryReport(
            in NetworkSessionRecoverySignal signal,
            out NetworkSessionRecoveryDecision decision)
        {
            ValidateSignal(in signal);
            var now = _options.UtcNowProvider();
            NetworkSessionRecoverySuppressionReason? suppression = null;
            lock (_gate)
            {
                _receivedSignalCount++;
                if (signal.Kind == NetworkSessionRecoverySignalKind.Recovered)
                {
                    ResetNoLock();
                    var recoveredDirective = _options.Policy.Evaluate(in signal);
                    NetworkSessionRecoveryRulePolicy.ValidateDirective(in recoveredDirective);
                    decision = new NetworkSessionRecoveryDecision(in signal, in recoveredDirective, now);
                    _currentDecision = decision;
                    _publishedDecisionCount++;
                }
                else
                {
                    var key = new SignalKey(in signal);
                    if (IsDuplicateNoLock(in key, now))
                    {
                        _duplicateSignalCount++;
                        decision = _currentDecision;
                        suppression = NetworkSessionRecoverySuppressionReason.Duplicate;
                    }
                    else
                    {
                        TrackSignalNoLock(in key, now);
                        var directive = _options.Policy.Evaluate(in signal);
                        NetworkSessionRecoveryRulePolicy.ValidateDirective(in directive);
                        suppression = ResolvePrioritySuppressionNoLock(in directive);
                        if (suppression.HasValue)
                        {
                            _prioritySuppressedCount++;
                            decision = _currentDecision;
                        }
                        else
                        {
                            decision = new NetworkSessionRecoveryDecision(in signal, in directive, now);
                            _currentDecision = decision;
                            _publishedDecisionCount++;
                        }
                    }
                }
            }

            if (suppression.HasValue)
            {
                PublishSuppressed(in signal, suppression.Value);
                return false;
            }

            PublishDecision(in decision);
            return true;
        }

        /// <summary>清除当前动作和去重索引，供新会话或显式恢复完成时调用。</summary>
        public void Reset()
        {
            lock (_gate) ResetNoLock();
            try { ResetPerformed?.Invoke(); } catch { }
        }

        /// <summary>读取累计诊断快照。</summary>
        public NetworkSessionRecoveryDiagnostics GetDiagnostics()
        {
            lock (_gate)
            {
                return new NetworkSessionRecoveryDiagnostics(
                    _receivedSignalCount,
                    _publishedDecisionCount,
                    _duplicateSignalCount,
                    _prioritySuppressedCount,
                    _resetCount,
                    _lastSignals.Count);
            }
        }

        private static void ValidateSignal(in NetworkSessionRecoverySignal signal)
        {
            if (!Enum.IsDefined(typeof(NetworkSessionRecoverySignalKind), signal.Kind))
                throw new ArgumentOutOfRangeException(nameof(signal), signal.Kind, "恢复信号不是框架已知值。");
            if (signal.Kind == NetworkSessionRecoverySignalKind.None)
                throw new ArgumentOutOfRangeException(nameof(signal), signal.Kind, "不能上报空恢复信号。");
            if (!Enum.IsDefined(typeof(SyncHealthSeverity), signal.Severity))
                throw new ArgumentOutOfRangeException(nameof(signal), signal.Severity, "恢复信号严重程度不是框架已知值。");
        }

        private bool IsDuplicateNoLock(in SignalKey key, DateTimeOffset now)
        {
            if (_options.DuplicateSignalWindow <= TimeSpan.Zero ||
                !_lastSignals.TryGetValue(key, out var previous))
            {
                return false;
            }

            var elapsed = now - previous;
            return elapsed >= TimeSpan.Zero && elapsed <= _options.DuplicateSignalWindow;
        }

        private void TrackSignalNoLock(in SignalKey key, DateTimeOffset now)
        {
            if (!_lastSignals.ContainsKey(key) &&
                _lastSignals.Count >= _options.MaximumTrackedSignals)
            {
                // 容量受限时淘汰最早信号，避免长期会话因关联上下文变化导致索引无限增长。
                var hasOldest = false;
                var oldestKey = default(SignalKey);
                var oldestTime = DateTimeOffset.MaxValue;
                foreach (var pair in _lastSignals)
                {
                    if (pair.Value >= oldestTime) continue;
                    hasOldest = true;
                    oldestKey = pair.Key;
                    oldestTime = pair.Value;
                }

                if (hasOldest) _lastSignals.Remove(oldestKey);
            }

            _lastSignals[key] = now;
        }

        private NetworkSessionRecoverySuppressionReason? ResolvePrioritySuppressionNoLock(
            in NetworkSessionRecoveryDirective directive)
        {
            if (!_currentDecision.HasAction) return null;
            if (directive.Priority < _currentDecision.Priority)
                return NetworkSessionRecoverySuppressionReason.LowerPriority;
            if (directive.Priority > _currentDecision.Priority && !_options.AllowActionEscalation)
                return NetworkSessionRecoverySuppressionReason.EscalationDisabled;
            if (directive.Priority == _currentDecision.Priority && !_options.AllowEqualPriorityReplacement)
                return NetworkSessionRecoverySuppressionReason.EqualPriorityReplacementDisabled;
            return null;
        }

        private void ResetNoLock()
        {
            _currentDecision = default;
            _lastSignals.Clear();
            _resetCount++;
        }

        private void PublishDecision(in NetworkSessionRecoveryDecision decision)
        {
            try { _options.DiagnosticsSink?.OnDecisionPublished(in decision); } catch { }
            try { DecisionPublished?.Invoke(decision); } catch { }
        }

        private void PublishSuppressed(
            in NetworkSessionRecoverySignal signal,
            NetworkSessionRecoverySuppressionReason reason)
        {
            try { _options.DiagnosticsSink?.OnSignalSuppressed(in signal, reason); } catch { }
        }

        private readonly struct SignalKey : IEquatable<SignalKey>
        {
            public SignalKey(in NetworkSessionRecoverySignal signal)
            {
                Kind = signal.Kind;
                CorrelationContext = signal.CorrelationContext ?? string.Empty;
            }

            private NetworkSessionRecoverySignalKind Kind { get; }

            private string CorrelationContext { get; }

            public bool Equals(SignalKey other)
            {
                return Kind == other.Kind &&
                       string.Equals(CorrelationContext, other.CorrelationContext, StringComparison.Ordinal);
            }

            public override bool Equals(object? obj)
            {
                return obj is SignalKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = (int)Kind;
                    hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(CorrelationContext);
                    return hash;
                }
            }
        }
    }

    /// <summary>恢复动作执行完成后的稳定状态。</summary>
    public enum NetworkSessionRecoveryExecutionStatus
    {
        /// <summary>决策不包含需要执行的动作。</summary>
        NoAction = 0,

        /// <summary>已找到处理器并成功执行。</summary>
        Executed = 1,

        /// <summary>当前路由器没有注册该动作的处理器。</summary>
        Unhandled = 2,

        /// <summary>动作处理器执行失败，异常已包含在结果中。</summary>
        Failed = 3,

        /// <summary>动作执行被调用方取消。</summary>
        Cancelled = 4
    }

    /// <summary>未注册恢复动作处理器时采用的策略。</summary>
    public enum NetworkSessionRecoveryUnhandledActionPolicy
    {
        /// <summary>返回 <see cref="NetworkSessionRecoveryExecutionStatus.Unhandled"/>。</summary>
        ReturnUnhandled = 0,

        /// <summary>抛出 <see cref="InvalidOperationException"/>。</summary>
        Throw = 1
    }

    /// <summary>恢复动作处理器抛出异常时采用的策略。</summary>
    public enum NetworkSessionRecoveryHandlerFailurePolicy
    {
        /// <summary>捕获异常并返回失败结果。</summary>
        CaptureAndReturn = 0,

        /// <summary>保留处理器原有抛出行为。</summary>
        Throw = 1
    }

    /// <summary>恢复动作取消时采用的策略。</summary>
    public enum NetworkSessionRecoveryCancellationPolicy
    {
        /// <summary>返回取消结果。</summary>
        ReturnCancelled = 0,

        /// <summary>继续向调用方抛出取消异常。</summary>
        Throw = 1
    }

    /// <summary>恢复动作执行上下文，允许项目附加超时、服务或请求参数。</summary>
    public readonly struct NetworkSessionRecoveryExecutionContext
    {
        /// <summary>创建恢复动作执行上下文。</summary>
        public NetworkSessionRecoveryExecutionContext(
            in NetworkSessionRecoveryDecision decision,
            object? state = null)
        {
            Decision = decision;
            State = state;
        }

        /// <summary>当前需要执行的恢复决策。</summary>
        public NetworkSessionRecoveryDecision Decision { get; }

        /// <summary>由接入方传入的可选执行状态。</summary>
        public object? State { get; }
    }

    /// <summary>项目恢复动作处理器。</summary>
    public delegate System.Threading.Tasks.Task<TResult> NetworkSessionRecoveryActionHandler<TResult>(
        NetworkSessionRecoveryExecutionContext context,
        System.Threading.CancellationToken cancellationToken);

    /// <summary>恢复动作执行结果。</summary>
    public readonly struct NetworkSessionRecoveryExecutionResult<TResult>
    {
        internal NetworkSessionRecoveryExecutionResult(
            in NetworkSessionRecoveryDecision decision,
            NetworkSessionRecoveryExecutionStatus status,
            TResult value,
            bool hasValue,
            Exception? exception)
        {
            Decision = decision;
            Status = status;
            Value = value;
            HasValue = hasValue;
            Exception = exception;
        }

        /// <summary>本次尝试执行的恢复决策。</summary>
        public NetworkSessionRecoveryDecision Decision { get; }

        /// <summary>动作执行状态。</summary>
        public NetworkSessionRecoveryExecutionStatus Status { get; }

        /// <summary>处理器成功执行后返回的项目结果。</summary>
        public TResult Value { get; }

        /// <summary>当前结果是否包含有效的项目返回值。</summary>
        public bool HasValue { get; }

        /// <summary>捕获到的处理器异常；其他状态为 <c>null</c>。</summary>
        public Exception? Exception { get; }

        /// <summary>动作是否成功执行。</summary>
        public bool Succeeded => Status == NetworkSessionRecoveryExecutionStatus.Executed;
    }

    /// <summary>恢复动作执行诊断接收器。</summary>
    public interface INetworkSessionRecoveryExecutionDiagnosticsSink<TResult>
    {
        /// <summary>动作执行完成并形成结构化结果时调用。</summary>
        void OnExecutionCompleted(in NetworkSessionRecoveryExecutionResult<TResult> result);
    }

    /// <summary>使用委托快速接入恢复动作执行诊断。</summary>
    public sealed class DelegatingNetworkSessionRecoveryExecutionDiagnosticsSink<TResult> :
        INetworkSessionRecoveryExecutionDiagnosticsSink<TResult>
    {
        private readonly Action<NetworkSessionRecoveryExecutionResult<TResult>> _completed;

        /// <summary>创建委托式执行诊断接收器。</summary>
        public DelegatingNetworkSessionRecoveryExecutionDiagnosticsSink(
            Action<NetworkSessionRecoveryExecutionResult<TResult>> completed)
        {
            _completed = completed ?? throw new ArgumentNullException(nameof(completed));
        }

        /// <inheritdoc />
        public void OnExecutionCompleted(in NetworkSessionRecoveryExecutionResult<TResult> result)
        {
            _completed(result);
        }
    }

    /// <summary>执行框架恢复决策的项目扩展点。</summary>
    public interface INetworkSessionRecoveryActionExecutor<TResult>
    {
        /// <summary>执行恢复决策，并允许调用方附加项目状态。</summary>
        System.Threading.Tasks.Task<NetworkSessionRecoveryExecutionResult<TResult>> ExecuteAsync(
            NetworkSessionRecoveryDecision decision,
            object? state = null,
            System.Threading.CancellationToken cancellationToken = default);
    }

    /// <summary>泛型恢复动作路由器选项。</summary>
    public sealed class NetworkSessionRecoveryActionRouterOptions<TResult>
    {
        /// <summary>未注册动作处理器时采用的策略。</summary>
        public NetworkSessionRecoveryUnhandledActionPolicy UnhandledActionPolicy { get; set; } =
            NetworkSessionRecoveryUnhandledActionPolicy.ReturnUnhandled;

        /// <summary>处理器执行失败时采用的策略。</summary>
        public NetworkSessionRecoveryHandlerFailurePolicy HandlerFailurePolicy { get; set; } =
            NetworkSessionRecoveryHandlerFailurePolicy.CaptureAndReturn;

        /// <summary>调用方取消动作时采用的策略。</summary>
        public NetworkSessionRecoveryCancellationPolicy CancellationPolicy { get; set; } =
            NetworkSessionRecoveryCancellationPolicy.Throw;

        /// <summary>可选的结构化执行诊断接收器。</summary>
        public INetworkSessionRecoveryExecutionDiagnosticsSink<TResult>? DiagnosticsSink { get; set; }

        internal NetworkSessionRecoveryActionRouterOptions<TResult> Snapshot()
        {
            if (!Enum.IsDefined(typeof(NetworkSessionRecoveryUnhandledActionPolicy), UnhandledActionPolicy))
                throw new ArgumentOutOfRangeException(nameof(UnhandledActionPolicy));
            if (!Enum.IsDefined(typeof(NetworkSessionRecoveryHandlerFailurePolicy), HandlerFailurePolicy))
                throw new ArgumentOutOfRangeException(nameof(HandlerFailurePolicy));
            if (!Enum.IsDefined(typeof(NetworkSessionRecoveryCancellationPolicy), CancellationPolicy))
                throw new ArgumentOutOfRangeException(nameof(CancellationPolicy));

            return new NetworkSessionRecoveryActionRouterOptions<TResult>
            {
                UnhandledActionPolicy = UnhandledActionPolicy,
                HandlerFailurePolicy = HandlerFailurePolicy,
                CancellationPolicy = CancellationPolicy,
                DiagnosticsSink = DiagnosticsSink
            };
        }
    }

    /// <summary>
    /// 将框架恢复动作路由到项目注册的异步处理器。路由器不持有业务服务，项目通过处理器和执行状态完成注入。
    /// </summary>
    public sealed class NetworkSessionRecoveryActionRouter<TResult> :
        INetworkSessionRecoveryActionExecutor<TResult>
    {
        private readonly object _gate = new object();
        private readonly NetworkSessionRecoveryActionRouterOptions<TResult> _options;
        private readonly System.Collections.Generic.Dictionary<NetworkSessionRecoveryAction, NetworkSessionRecoveryActionHandler<TResult>> _handlers =
            new System.Collections.Generic.Dictionary<NetworkSessionRecoveryAction, NetworkSessionRecoveryActionHandler<TResult>>();

        /// <summary>使用默认执行策略创建动作路由器。</summary>
        public NetworkSessionRecoveryActionRouter(
            NetworkSessionRecoveryActionRouterOptions<TResult>? options = null)
        {
            _options = (options ?? new NetworkSessionRecoveryActionRouterOptions<TResult>()).Snapshot();
        }

        /// <summary>注册动作处理器；重复注册默认抛出异常。</summary>
        public NetworkSessionRecoveryActionRouter<TResult> Register(
            NetworkSessionRecoveryAction action,
            NetworkSessionRecoveryActionHandler<TResult> handler,
            bool replaceExisting = false)
        {
            if (!Enum.IsDefined(typeof(NetworkSessionRecoveryAction), action) ||
                action == NetworkSessionRecoveryAction.None)
            {
                throw new ArgumentOutOfRangeException(nameof(action), action, "必须注册有效的非空恢复动作。");
            }

            if (handler == null) throw new ArgumentNullException(nameof(handler));
            lock (_gate)
            {
                if (!replaceExisting && _handlers.ContainsKey(action))
                    throw new InvalidOperationException($"恢复动作 '{action}' 已经注册处理器。");
                _handlers[action] = handler;
            }

            return this;
        }

        /// <summary>移除指定动作处理器。</summary>
        public bool Remove(NetworkSessionRecoveryAction action)
        {
            lock (_gate) return _handlers.Remove(action);
        }

        /// <summary>判断指定动作是否已经注册处理器。</summary>
        public bool CanExecute(NetworkSessionRecoveryAction action)
        {
            lock (_gate) return _handlers.ContainsKey(action);
        }

        /// <summary>按当前路由表执行恢复决策。</summary>
        public async System.Threading.Tasks.Task<NetworkSessionRecoveryExecutionResult<TResult>> ExecuteAsync(
            NetworkSessionRecoveryDecision decision,
            object? state = null,
            System.Threading.CancellationToken cancellationToken = default)
        {
            if (!decision.HasAction)
            {
                return Publish(new NetworkSessionRecoveryExecutionResult<TResult>(
                    in decision,
                    NetworkSessionRecoveryExecutionStatus.NoAction,
                    default!,
                    hasValue: false,
                    exception: null));
            }

            NetworkSessionRecoveryActionHandler<TResult>? handler;
            lock (_gate) _handlers.TryGetValue(decision.Action, out handler);
            if (handler == null)
            {
                if (_options.UnhandledActionPolicy == NetworkSessionRecoveryUnhandledActionPolicy.Throw)
                    throw new InvalidOperationException($"恢复动作 '{decision.Action}' 未注册处理器。");

                return Publish(new NetworkSessionRecoveryExecutionResult<TResult>(
                    in decision,
                    NetworkSessionRecoveryExecutionStatus.Unhandled,
                    default!,
                    hasValue: false,
                    exception: null));
            }

            var context = new NetworkSessionRecoveryExecutionContext(in decision, state);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var value = await handler(context, cancellationToken).ConfigureAwait(false);
                return Publish(new NetworkSessionRecoveryExecutionResult<TResult>(
                    in decision,
                    NetworkSessionRecoveryExecutionStatus.Executed,
                    value,
                    hasValue: true,
                    exception: null));
            }
            catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested &&
                _options.CancellationPolicy == NetworkSessionRecoveryCancellationPolicy.ReturnCancelled)
            {
                return Publish(new NetworkSessionRecoveryExecutionResult<TResult>(
                    in decision,
                    NetworkSessionRecoveryExecutionStatus.Cancelled,
                    default!,
                    hasValue: false,
                    exception));
            }
            catch (Exception exception) when (
                _options.HandlerFailurePolicy == NetworkSessionRecoveryHandlerFailurePolicy.CaptureAndReturn &&
                !(exception is OperationCanceledException && cancellationToken.IsCancellationRequested))
            {
                return Publish(new NetworkSessionRecoveryExecutionResult<TResult>(
                    in decision,
                    NetworkSessionRecoveryExecutionStatus.Failed,
                    default!,
                    hasValue: false,
                    exception));
            }
        }

        private NetworkSessionRecoveryExecutionResult<TResult> Publish(
            NetworkSessionRecoveryExecutionResult<TResult> result)
        {
            try { _options.DiagnosticsSink?.OnExecutionCompleted(in result); } catch { }
            return result;
        }
    }

    /// <summary>恢复运行时收到新决策后的动作执行模式。</summary>
    public enum NetworkSessionRecoveryExecutionMode
    {
        /// <summary>协调器采纳决策后立即执行对应动作。</summary>
        Automatic = 0,

        /// <summary>仅记录决策，由接入方在合适的业务时机显式执行。</summary>
        Manual = 1
    }

    /// <summary>统一会话恢复运行时选项。</summary>
    public sealed class NetworkSessionRecoveryRuntimeOptions
    {
        /// <summary>新决策的动作执行模式。</summary>
        public NetworkSessionRecoveryExecutionMode ExecutionMode { get; set; } =
            NetworkSessionRecoveryExecutionMode.Automatic;

        /// <summary>新决策替换旧决策时，是否取消仍在执行的旧动作。</summary>
        public bool CancelSupersededExecution { get; set; } = true;

        /// <summary>显式重置运行时时，是否取消仍在执行的动作。</summary>
        public bool CancelExecutionOnReset { get; set; } = true;

        /// <summary>旧代次动作完成后，是否抑制其完成事件和最后结果写入。</summary>
        public bool SuppressStaleExecutionCompletion { get; set; } = true;

        /// <summary>是否将无动作决策也交给执行器；默认仅记录恢复完成等诊断决策。</summary>
        public bool ExecuteNoActionDecisions { get; set; }

        /// <summary>自动执行时为项目处理器提供可选状态。</summary>
        public Func<NetworkSessionRecoveryDecision, object?>? ExecutionStateProvider { get; set; }

        /// <summary>自动执行任务抛出未结构化异常时的兜底回调。</summary>
        public Action<Exception>? BackgroundExecutionFailure { get; set; }

        internal NetworkSessionRecoveryRuntimeOptions Snapshot()
        {
            if (!Enum.IsDefined(typeof(NetworkSessionRecoveryExecutionMode), ExecutionMode))
                throw new ArgumentOutOfRangeException(nameof(ExecutionMode));

            return new NetworkSessionRecoveryRuntimeOptions
            {
                ExecutionMode = ExecutionMode,
                CancelSupersededExecution = CancelSupersededExecution,
                CancelExecutionOnReset = CancelExecutionOnReset,
                SuppressStaleExecutionCompletion = SuppressStaleExecutionCompletion,
                ExecuteNoActionDecisions = ExecuteNoActionDecisions,
                ExecutionStateProvider = ExecutionStateProvider,
                BackgroundExecutionFailure = BackgroundExecutionFailure
            };
        }
    }

    /// <summary>恢复运行时累计诊断快照。</summary>
    public readonly struct NetworkSessionRecoveryRuntimeDiagnostics
    {
        internal NetworkSessionRecoveryRuntimeDiagnostics(
            long acceptedDecisionCount,
            long startedExecutionCount,
            long completedExecutionCount,
            long staleExecutionCount,
            long faultedExecutionCount,
            long resetCount,
            long generation)
        {
            AcceptedDecisionCount = acceptedDecisionCount;
            StartedExecutionCount = startedExecutionCount;
            CompletedExecutionCount = completedExecutionCount;
            StaleExecutionCount = staleExecutionCount;
            FaultedExecutionCount = faultedExecutionCount;
            ResetCount = resetCount;
            Generation = generation;
        }

        /// <summary>协调器采纳的决策总数。</summary>
        public long AcceptedDecisionCount { get; }

        /// <summary>已经交给动作执行器的执行总数。</summary>
        public long StartedExecutionCount { get; }

        /// <summary>允许写入最后结果并发布完成事件的执行总数。</summary>
        public long CompletedExecutionCount { get; }

        /// <summary>因代次过期而被抑制的执行完成总数。</summary>
        public long StaleExecutionCount { get; }

        /// <summary>执行器直接抛出异常的总数。</summary>
        public long FaultedExecutionCount { get; }

        /// <summary>运行时显式重置总数。</summary>
        public long ResetCount { get; }

        /// <summary>当前恢复生命周期代次。</summary>
        public long Generation { get; }
    }

    /// <summary>
    /// 组合恢复协调器与项目动作执行器，并统一处理自动执行、取消、代次隔离和重置生命周期。
    /// </summary>
    public sealed class NetworkSessionRecoveryRuntime<TResult> :
        INetworkSessionRecoverySignalSink,
        IDisposable
    {
        private readonly object _gate = new object();
        private readonly NetworkSessionRecoveryCoordinator _coordinator;
        private readonly INetworkSessionRecoveryActionExecutor<TResult> _executor;
        private readonly NetworkSessionRecoveryRuntimeOptions _options;
        private readonly System.Collections.Generic.List<System.Threading.CancellationTokenSource> _retiredCancellationSources =
            new System.Collections.Generic.List<System.Threading.CancellationTokenSource>();
        private System.Threading.CancellationTokenSource? _executionCancellation;
        private System.Threading.Tasks.Task<NetworkSessionRecoveryExecutionResult<TResult>> _pendingExecution =
            System.Threading.Tasks.Task.FromResult(default(NetworkSessionRecoveryExecutionResult<TResult>));
        private NetworkSessionRecoveryExecutionResult<TResult> _lastExecution;
        private bool _hasLastExecution;
        private bool _disposed;
        private long _generation;
        private long _acceptedDecisionCount;
        private long _startedExecutionCount;
        private long _completedExecutionCount;
        private long _staleExecutionCount;
        private long _faultedExecutionCount;
        private long _resetCount;

        /// <summary>使用框架推荐协调选项和运行时选项创建恢复运行时。</summary>
        public NetworkSessionRecoveryRuntime(
            INetworkSessionRecoveryActionExecutor<TResult> executor,
            NetworkSessionRecoveryOptions? recoveryOptions = null,
            NetworkSessionRecoveryRuntimeOptions? runtimeOptions = null)
            : this(
                executor,
                new NetworkSessionRecoveryCoordinator(recoveryOptions),
                runtimeOptions)
        {
        }

        /// <summary>复用已有协调器创建恢复运行时，适合将业务执行层挂接到既有会话对象。</summary>
        public NetworkSessionRecoveryRuntime(
            INetworkSessionRecoveryActionExecutor<TResult> executor,
            NetworkSessionRecoveryCoordinator coordinator,
            NetworkSessionRecoveryRuntimeOptions? runtimeOptions = null)
        {
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            _options = (runtimeOptions ?? new NetworkSessionRecoveryRuntimeOptions()).Snapshot();
            _coordinator.DecisionPublished += HandleDecisionPublished;
            _coordinator.ResetPerformed += HandleCoordinatorReset;
        }

        /// <summary>协调器采纳新决策并更新运行时代次后触发。</summary>
        public event Action<NetworkSessionRecoveryDecision>? DecisionAccepted;

        /// <summary>当前代次动作完成且允许发布结果时触发。</summary>
        public event Action<NetworkSessionRecoveryExecutionResult<TResult>>? ExecutionCompleted;

        /// <summary>当前协调后的恢复决策。</summary>
        public NetworkSessionRecoveryDecision CurrentDecision => _coordinator.CurrentDecision;

        /// <summary>当前是否已经记录可用执行结果。</summary>
        public bool HasLastExecution
        {
            get { lock (_gate) return _hasLastExecution; }
        }

        /// <summary>最近一次未被过期策略抑制的执行结果。</summary>
        public NetworkSessionRecoveryExecutionResult<TResult> LastExecution
        {
            get { lock (_gate) return _lastExecution; }
        }

        /// <summary>最近一次自动或手动启动的执行任务。</summary>
        public System.Threading.Tasks.Task<NetworkSessionRecoveryExecutionResult<TResult>> PendingExecution
        {
            get { lock (_gate) return _pendingExecution; }
        }

        /// <inheritdoc />
        public bool TryReport(
            in NetworkSessionRecoverySignal signal,
            out NetworkSessionRecoveryDecision decision)
        {
            ThrowIfDisposed();
            return _coordinator.TryReport(in signal, out decision);
        }

        /// <summary>显式执行当前决策，供手动模式或需要业务时机控制的接入方调用。</summary>
        public System.Threading.Tasks.Task<NetworkSessionRecoveryExecutionResult<TResult>> ExecuteCurrentAsync(
            object? state = null,
            System.Threading.CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            NetworkSessionRecoveryDecision decision;
            long generation;
            System.Threading.CancellationToken runtimeToken;
            lock (_gate)
            {
                decision = _coordinator.CurrentDecision;
                generation = _generation;
                if (_executionCancellation == null)
                    _executionCancellation = new System.Threading.CancellationTokenSource();
                runtimeToken = _executionCancellation.Token;
            }

            var execution = ExecuteTrackedAsync(
                decision,
                state,
                generation,
                runtimeToken,
                cancellationToken);
            lock (_gate)
            {
                if (generation == _generation) _pendingExecution = execution;
            }
            return execution;
        }

        /// <summary>发布标准恢复完成信号，使当前动作和去重状态进入新的已恢复代次。</summary>
        public bool CompleteRecovery(
            string? correlationContext = null,
            string? detail = null,
            int frame = -1)
        {
            var signal = new NetworkSessionRecoverySignal(
                NetworkSessionRecoverySignalKind.Recovered,
                SyncHealthSeverity.Info,
                frame,
                correlationContext: correlationContext,
                detail: detail);
            return TryReport(in signal, out _);
        }

        /// <summary>取消或隔离当前执行，并清除协调器当前决策和去重索引。</summary>
        public void Reset()
        {
            ThrowIfDisposed();
            _coordinator.Reset();
        }

        private void ResetExecutionLifecycle()
        {
            System.Threading.CancellationTokenSource? previous;
            lock (_gate)
            {
                _generation++;
                _resetCount++;
                previous = _executionCancellation;
                _executionCancellation = null;
                _pendingExecution = System.Threading.Tasks.Task.FromResult(
                    default(NetworkSessionRecoveryExecutionResult<TResult>));
                _lastExecution = default;
                _hasLastExecution = false;
                if (previous != null && !_options.CancelExecutionOnReset)
                    _retiredCancellationSources.Add(previous);
            }

            if (previous != null && _options.CancelExecutionOnReset)
                CancelAndDispose(previous);
        }

        /// <summary>读取协调器的累计信号与决策诊断。</summary>
        public NetworkSessionRecoveryDiagnostics GetRecoveryDiagnostics()
        {
            return _coordinator.GetDiagnostics();
        }

        /// <summary>读取动作生命周期的累计诊断。</summary>
        public NetworkSessionRecoveryRuntimeDiagnostics GetRuntimeDiagnostics()
        {
            lock (_gate)
            {
                return new NetworkSessionRecoveryRuntimeDiagnostics(
                    _acceptedDecisionCount,
                    _startedExecutionCount,
                    _completedExecutionCount,
                    _staleExecutionCount,
                    _faultedExecutionCount,
                    _resetCount,
                    _generation);
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            System.Threading.CancellationTokenSource? current;
            System.Threading.CancellationTokenSource[] retired;
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _generation++;
                _coordinator.DecisionPublished -= HandleDecisionPublished;
                _coordinator.ResetPerformed -= HandleCoordinatorReset;
                current = _executionCancellation;
                _executionCancellation = null;
                retired = _retiredCancellationSources.ToArray();
                _retiredCancellationSources.Clear();
            }

            if (current != null) CancelAndDispose(current);
            foreach (var source in retired) CancelAndDispose(source);
        }

        private void AcceptDecision(in NetworkSessionRecoveryDecision decision)
        {
            System.Threading.CancellationTokenSource? previous;
            System.Threading.CancellationTokenSource current;
            long generation;
            lock (_gate)
            {
                previous = _executionCancellation;
                current = new System.Threading.CancellationTokenSource();
                _executionCancellation = current;
                generation = ++_generation;
                _acceptedDecisionCount++;
                _pendingExecution = System.Threading.Tasks.Task.FromResult(
                    default(NetworkSessionRecoveryExecutionResult<TResult>));
                if (previous != null && !_options.CancelSupersededExecution)
                    _retiredCancellationSources.Add(previous);
            }

            if (previous != null && _options.CancelSupersededExecution)
                CancelAndDispose(previous);

            if (_options.ExecutionMode == NetworkSessionRecoveryExecutionMode.Automatic &&
                (decision.HasAction || _options.ExecuteNoActionDecisions))
            {
                var execution = ExecuteAutomaticAsync(decision, generation, current.Token);
                lock (_gate)
                {
                    if (generation == _generation) _pendingExecution = execution;
                }
                ObserveAutomaticExecution(execution);
            }

            try { DecisionAccepted?.Invoke(decision); } catch { }
        }

        private void HandleDecisionPublished(NetworkSessionRecoveryDecision decision)
        {
            lock (_gate)
            {
                if (_disposed) return;
            }
            AcceptDecision(in decision);
        }

        private void HandleCoordinatorReset()
        {
            lock (_gate)
            {
                if (_disposed) return;
            }
            ResetExecutionLifecycle();
        }

        private async System.Threading.Tasks.Task<NetworkSessionRecoveryExecutionResult<TResult>> ExecuteAutomaticAsync(
            NetworkSessionRecoveryDecision decision,
            long generation,
            System.Threading.CancellationToken runtimeToken)
        {
            object? state;
            try
            {
                state = _options.ExecutionStateProvider?.Invoke(decision);
            }
            catch
            {
                lock (_gate) _faultedExecutionCount++;
                throw;
            }

            return await ExecuteTrackedAsync(
                decision,
                state,
                generation,
                runtimeToken,
                default).ConfigureAwait(false);
        }

        private async System.Threading.Tasks.Task<NetworkSessionRecoveryExecutionResult<TResult>> ExecuteTrackedAsync(
            NetworkSessionRecoveryDecision decision,
            object? state,
            long generation,
            System.Threading.CancellationToken runtimeToken,
            System.Threading.CancellationToken callerToken)
        {
            lock (_gate) _startedExecutionCount++;
            using var linked = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(
                runtimeToken,
                callerToken);
            NetworkSessionRecoveryExecutionResult<TResult> result;
            try
            {
                result = await _executor.ExecuteAsync(
                    decision,
                    state,
                    linked.Token).ConfigureAwait(false);
            }
            catch
            {
                lock (_gate) _faultedExecutionCount++;
                throw;
            }

            var publish = true;
            lock (_gate)
            {
                var stale = generation != _generation || _disposed;
                if (stale) _staleExecutionCount++;
                if (stale && _options.SuppressStaleExecutionCompletion)
                {
                    publish = false;
                }
                else
                {
                    _lastExecution = result;
                    _hasLastExecution = true;
                    _completedExecutionCount++;
                }
            }

            if (publish)
            {
                try { ExecutionCompleted?.Invoke(result); } catch { }
            }
            return result;
        }

        private async void ObserveAutomaticExecution(
            System.Threading.Tasks.Task<NetworkSessionRecoveryExecutionResult<TResult>> execution)
        {
            try
            {
                await execution.ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                try { _options.BackgroundExecutionFailure?.Invoke(exception); } catch { }
            }
        }

        private static void CancelAndDispose(System.Threading.CancellationTokenSource source)
        {
            try { source.Cancel(); } catch { }
            source.Dispose();
        }

        private void ThrowIfDisposed()
        {
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(NetworkSessionRecoveryRuntime<TResult>));
            }
        }
    }
}
