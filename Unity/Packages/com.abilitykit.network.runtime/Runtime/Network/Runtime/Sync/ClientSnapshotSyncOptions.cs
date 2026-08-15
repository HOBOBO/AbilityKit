#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Network.Runtime.Sync
{
    /// <summary>将项目协议快照映射为框架通用的流信封。</summary>
    public delegate SnapshotStreamEnvelope SnapshotEnvelopeFactory<TSnapshot>(in TSnapshot snapshot);

    /// <summary>返回当前快照流允许接受的最大序列跨度。</summary>
    public delegate int SnapshotSequenceAdvancePolicy<TSnapshot>(in TSnapshot snapshot);

    /// <summary>返回快照包含的实体数量，用于统一诊断。</summary>
    public delegate int SnapshotEntityCountProvider<TSnapshot>(in TSnapshot snapshot);

    /// <summary>将已通过校验的快照应用到项目表现层或状态存储。</summary>
    public delegate void SnapshotApplyHandler<TSnapshot>(in TSnapshot snapshot);

    /// <summary>当快照使数据流进入恢复状态时，选择需要发出的恢复请求。</summary>
    public delegate SnapshotRecoveryRequestKind SnapshotRecoveryStrategy<TSnapshot>(
        in TSnapshot snapshot,
        in SnapshotStreamValidationResult validation);

    /// <summary>执行快照恢复策略选出的恢复请求。</summary>
    public delegate void SnapshotRecoveryHandler<TSnapshot>(
        in TSnapshot snapshot,
        in SnapshotRecoveryRequest request);

    /// <summary>为一次已完成的快照处理构建健康事件。</summary>
    public delegate IReadOnlyList<SyncHealthEvent> ClientSnapshotSyncHealthEventPolicy<TSnapshot>(
        in ClientSnapshotSyncEventContext<TSnapshot> context,
        IReadOnlyList<SyncHealthEvent> standardEvents);

    /// <summary>标识观察器在上报哪一个已确定的管线操作时发生异常。</summary>
    public enum ClientSnapshotSyncObserverStage
    {
        /// <summary>处理结果通知失败。</summary>
        Result = 0,

        /// <summary>恢复请求通知失败。</summary>
        RecoveryRequested = 1,

        /// <summary>重置通知失败。</summary>
        Reset = 2
    }

    /// <summary>接收已从同步主链路隔离的观察器异常。</summary>
    public delegate void ClientSnapshotSyncObserverErrorHandler(
        ClientSnapshotSyncObserverStage stage,
        Exception exception);

    /// <summary>要求项目传输层或会话层执行的恢复操作。</summary>
    public enum SnapshotRecoveryRequestKind
    {
        /// <summary>保持恢复状态，但不向外部派发请求。</summary>
        None = 0,

        /// <summary>请求完整的权威基线快照。</summary>
        FullSnapshot = 1,

        /// <summary>请求可作为完整基线使用的关键帧。</summary>
        KeyFrame = 2,

        /// <summary>请求客户端当前兴趣区域对应的完整权威切片。</summary>
        AoiSlice = 3,

        /// <summary>
        /// 通过已配置的处理器派发项目自定义恢复操作。自定义健康事件可由
        /// <see cref="ClientSnapshotSyncOptions{TSnapshot}.HealthEventPolicy"/> 提供。
        /// </summary>
        Custom = 4
    }

    /// <summary>客户端快照管线发出的协议无关恢复请求。</summary>
    public readonly struct SnapshotRecoveryRequest
    {
        internal SnapshotRecoveryRequest(
            SnapshotRecoveryRequestKind kind,
            in SnapshotStreamValidationResult validation)
        {
            Kind = kind;
            Reason = validation.RecoveryReason;
            Envelope = validation.Envelope;
            GapCount = validation.GapCount;
        }

        /// <summary>选中的恢复操作。</summary>
        public SnapshotRecoveryRequestKind Kind { get; }

        /// <summary>使数据流进入恢复状态的校验原因。</summary>
        public SnapshotStreamRecoveryReason Reason { get; }

        /// <summary>触发恢复的快照信封。</summary>
        public SnapshotStreamEnvelope Envelope { get; }

        /// <summary>因序列缺口触发恢复时缺失的序列数量。</summary>
        public int GapCount { get; }

        /// <summary>项目恢复处理器是否应接收该请求。</summary>
        public bool ShouldDispatch => Kind != SnapshotRecoveryRequestKind.None;
    }

    /// <summary>自定义快照健康事件策略可使用的上下文。</summary>
    public readonly struct ClientSnapshotSyncEventContext<TSnapshot>
    {
        internal ClientSnapshotSyncEventContext(
            in TSnapshot snapshot,
            ClientSnapshotSyncStatus status,
            in SnapshotStreamValidationResult validation,
            in SnapshotRecoveryRequest recoveryRequest,
            int entityCount)
        {
            Snapshot = snapshot;
            Status = status;
            Validation = validation;
            RecoveryRequest = recoveryRequest;
            EntityCount = entityCount;
        }

        /// <summary>本次处理关联的协议快照。</summary>
        public TSnapshot Snapshot { get; }

        /// <summary>本次处理的协议无关结果。</summary>
        public ClientSnapshotSyncStatus Status { get; }

        /// <summary>底层数据流校验结果。</summary>
        public SnapshotStreamValidationResult Validation { get; }

        /// <summary>通用数据流信封。</summary>
        public SnapshotStreamEnvelope Envelope => Validation.Envelope;

        /// <summary>选中的恢复请求；未进入恢复流程时为默认空值。</summary>
        public SnapshotRecoveryRequest RecoveryRequest { get; }

        /// <summary>项目计数器上报的诊断实体数量。</summary>
        public int EntityCount { get; }
    }

    /// <summary>
    /// 观察已完成的管线操作，但不参与校验和提交顺序。
    /// 指标、追踪和诊断应使用观察器；传输动作应使用恢复处理器。
    /// </summary>
    public interface IClientSnapshotSyncObserver<TSnapshot>
    {
        /// <summary>观察一次已完成的处理及其完成后的可见状态。</summary>
        void OnResult(
            in TSnapshot snapshot,
            in ClientSnapshotSyncResult result,
            in ClientSnapshotSyncState state);

        /// <summary>在项目恢复处理器成功执行后观察该恢复请求。</summary>
        void OnRecoveryRequested(
            in TSnapshot snapshot,
            in SnapshotRecoveryRequest request,
            in ClientSnapshotSyncState state);

        /// <summary>观察重置操作及重置前捕获的状态。</summary>
        void OnReset(in ClientSnapshotSyncState previousState);
    }

    /// <summary>空实现观察器基类，便于接入方只重写关心的管线操作。</summary>
    public abstract class ClientSnapshotSyncObserver<TSnapshot> : IClientSnapshotSyncObserver<TSnapshot>
    {
        /// <inheritdoc />
        public virtual void OnResult(
            in TSnapshot snapshot,
            in ClientSnapshotSyncResult result,
            in ClientSnapshotSyncState state)
        {
        }

        /// <inheritdoc />
        public virtual void OnRecoveryRequested(
            in TSnapshot snapshot,
            in SnapshotRecoveryRequest request,
            in ClientSnapshotSyncState state)
        {
        }

        /// <inheritdoc />
        public virtual void OnReset(in ClientSnapshotSyncState previousState)
        {
        }
    }

    /// <summary>将快照观察事件分发给构造时确定的稳定观察器列表。</summary>
    public sealed class CompositeClientSnapshotSyncObserver<TSnapshot> : IClientSnapshotSyncObserver<TSnapshot>
    {
        private readonly IClientSnapshotSyncObserver<TSnapshot>[] _observers;

        /// <summary>复制传入的观察器数组并创建组合观察器。</summary>
        public CompositeClientSnapshotSyncObserver(params IClientSnapshotSyncObserver<TSnapshot>[] observers)
        {
            if (observers == null) throw new ArgumentNullException(nameof(observers));

            _observers = new IClientSnapshotSyncObserver<TSnapshot>[observers.Length];
            for (var i = 0; i < observers.Length; i++)
            {
                _observers[i] = observers[i] ??
                    throw new ArgumentException("Observer entries cannot be null.", nameof(observers));
            }
        }

        /// <summary>组合中包含的观察器数量。</summary>
        public int Count => _observers.Length;

        /// <inheritdoc />
        public void OnResult(
            in TSnapshot snapshot,
            in ClientSnapshotSyncResult result,
            in ClientSnapshotSyncState state)
        {
            for (var i = 0; i < _observers.Length; i++)
            {
                _observers[i].OnResult(in snapshot, in result, in state);
            }
        }

        /// <inheritdoc />
        public void OnRecoveryRequested(
            in TSnapshot snapshot,
            in SnapshotRecoveryRequest request,
            in ClientSnapshotSyncState state)
        {
            for (var i = 0; i < _observers.Length; i++)
            {
                _observers[i].OnRecoveryRequested(in snapshot, in request, in state);
            }
        }

        /// <inheritdoc />
        public void OnReset(in ClientSnapshotSyncState previousState)
        {
            for (var i = 0; i < _observers.Length; i++)
            {
                _observers[i].OnReset(in previousState);
            }
        }
    }

    /// <summary>
    /// <see cref="ClientSnapshotSyncPipeline{TSnapshot}"/> 的构造期扩展选项。
    /// 管线创建时会校验并复制这些值，因此后续修改该对象不会改变正在运行的数据流。
    /// </summary>
    public sealed class ClientSnapshotSyncOptions<TSnapshot>
    {
        /// <summary>创建包含必要参数的快照流构造配置。</summary>
        public ClientSnapshotSyncOptions(
            int minimumSupportedVersion,
            int maximumSupportedVersion,
            SnapshotEnvelopeFactory<TSnapshot> createEnvelope,
            SnapshotApplyHandler<TSnapshot> applySnapshot)
        {
            MinimumSupportedVersion = minimumSupportedVersion;
            MaximumSupportedVersion = maximumSupportedVersion;
            CreateEnvelope = createEnvelope;
            ApplySnapshot = applySnapshot;
        }

        /// <summary>可接受的最低协议结构版本。</summary>
        public int MinimumSupportedVersion { get; set; }

        /// <summary>可接受的最高协议结构版本。</summary>
        public int MaximumSupportedVersion { get; set; }

        /// <summary>必需的项目快照到框架信封映射。</summary>
        public SnapshotEnvelopeFactory<TSnapshot>? CreateEnvelope { get; set; }

        /// <summary>必需的项目状态或表现层应用操作。</summary>
        public SnapshotApplyHandler<TSnapshot>? ApplySnapshot { get; set; }

        /// <summary>可选的逐快照序列缺口容忍策略。</summary>
        public SnapshotSequenceAdvancePolicy<TSnapshot>? MaximumSequenceAdvance { get; set; }

        /// <summary>标准健康事件使用的可选实体计数器。</summary>
        public SnapshotEntityCountProvider<TSnapshot>? EntityCount { get; set; }

        /// <summary>
        /// 为基线缺失、基线不匹配、世界切换或序列缺口选择恢复请求。
        /// 默认使用 <see cref="SnapshotRecoveryRequestKind.FullSnapshot"/>。
        /// </summary>
        public SnapshotRecoveryStrategy<TSnapshot>? RecoveryStrategy { get; set; }

        /// <summary>将选中的恢复请求派发给项目会话层或传输层。</summary>
        public SnapshotRecoveryHandler<TSnapshot>? RecoveryHandler { get; set; }

        /// <summary>
        /// 过滤、追加或替换标准健康事件。返回 <see langword="null"/> 属于非法行为；
        /// 若要保留默认行为，应原样返回传入的标准事件列表。
        /// </summary>
        public ClientSnapshotSyncHealthEventPolicy<TSnapshot>? HealthEventPolicy { get; set; }

        /// <summary>可选的非权威诊断与追踪观察器。</summary>
        public IClientSnapshotSyncObserver<TSnapshot>? Observer { get; set; }

        /// <summary>
        /// 接收 <see cref="Observer"/> 抛出的异常。观察器异常不会离开管线；该可选处理器用于将
        /// 已隔离的异常暴露给项目诊断系统。该处理器自身抛出的异常也会被隔离。
        /// </summary>
        public ClientSnapshotSyncObserverErrorHandler? ObserverErrorHandler { get; set; }

        /// <summary>
        /// 该管线要求启用的同步 Profile。设置后会在构造阶段检查 Profile 内部一致性；
        /// 若同时提供 <see cref="AvailableCapabilities"/>，还会执行端点能力与协议版本协商。
        /// </summary>
        public NetworkSyncProfile? RequiredProfile { get; set; }

        /// <summary>当前接入模块或远端端点实际提供的能力上限。</summary>
        public NetworkSyncCapabilities? AvailableCapabilities { get; set; }

        /// <summary>一次返回当前 Options 中全部可静态判定的配置问题。</summary>
        public NetworkSyncConfigurationReport ValidateConfiguration()
        {
            var issues = new List<NetworkSyncConfigurationIssue>();
            NetworkSyncConfigurationValidator.AppendVersionRangeIssues(
                MinimumSupportedVersion,
                MaximumSupportedVersion,
                issues,
                "SchemaVersions");
            if (CreateEnvelope == null)
            {
                NetworkSyncConfigurationValidator.Add(
                    issues,
                    NetworkSyncConfigurationIssueCode.MissingEnvelopeFactory,
                    nameof(CreateEnvelope),
                    "必须提供项目快照到框架信封的映射。");
            }

            if (ApplySnapshot == null)
            {
                NetworkSyncConfigurationValidator.Add(
                    issues,
                    NetworkSyncConfigurationIssueCode.MissingSnapshotApplyHandler,
                    nameof(ApplySnapshot),
                    "必须提供已通过校验快照的应用操作。");
            }

            if (RequiredProfile.HasValue)
            {
                var profile = RequiredProfile.Value;
                if (AvailableCapabilities.HasValue)
                {
                    var available = AvailableCapabilities.Value;
                    var negotiation = NetworkSyncConfigurationValidator.Negotiate(
                        in profile,
                        MinimumSupportedVersion,
                        MaximumSupportedVersion,
                        in available);
                    for (var i = 0; i < negotiation.Report.Issues.Count; i++)
                    {
                        var issue = negotiation.Report.Issues[i];
                        if (issue.Code != NetworkSyncConfigurationIssueCode.InvalidSchemaVersionRange ||
                            issue.Path == "AvailableCapabilities.SchemaVersions")
                        {
                            issues.Add(issue);
                        }
                    }
                }
                else
                {
                    NetworkSyncConfigurationValidator.AppendProfileIssues(
                        in profile,
                        issues,
                        nameof(RequiredProfile));
                }
            }
            else if (AvailableCapabilities.HasValue)
            {
                NetworkSyncConfigurationValidator.Add(
                    issues,
                    NetworkSyncConfigurationIssueCode.MissingRequiredProfile,
                    nameof(RequiredProfile),
                    "提供端点能力时必须同时指定管线要求的同步 Profile。");
            }

            return new NetworkSyncConfigurationReport(issues);
        }

        /// <summary>必要委托或版本边界无效时抛出异常。</summary>
        public void Validate()
        {
            if (MinimumSupportedVersion < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(MinimumSupportedVersion));
            }

            if (MaximumSupportedVersion < MinimumSupportedVersion)
            {
                throw new ArgumentOutOfRangeException(nameof(MaximumSupportedVersion));
            }

            if (CreateEnvelope == null)
            {
                throw new ArgumentNullException(nameof(CreateEnvelope));
            }

            if (ApplySnapshot == null)
            {
                throw new ArgumentNullException(nameof(ApplySnapshot));
            }

            ValidateConfiguration().ThrowIfInvalid("客户端快照同步 Options");
        }
    }
}
