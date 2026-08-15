#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using System.Diagnostics;
using AbilityKit.Network.Runtime.Sync;

namespace AbilityKit.Network.Sdk
{
    /// <summary>可靠事件检查点存储提供器，由接入方决定内存、本地文件或远端持久化方式。</summary>
    public interface IReliableEventCheckpointStore
    {
        /// <summary>按事件流加载最近一次有效确认位置。</summary>
        bool TryLoad(string streamId, out ReliableEventCheckpoint checkpoint);

        /// <summary>保存事件流最近一次有效确认位置。</summary>
        void Save(in ReliableEventCheckpoint checkpoint);

        /// <summary>删除指定事件流的持久化确认位置。</summary>
        bool Remove(string streamId);
    }

    /// <summary>声明存储提供器支持等待异步写入完成。</summary>
    public interface IReliableEventCheckpointStoreFlushable
    {
        /// <summary>等待当前已排队的检查点写入完成。</summary>
        Task FlushAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>检查点 flush 的生命周期触发原因。</summary>
    public enum ReliableEventCheckpointFlushTrigger
    {
        /// <summary>由接入方显式请求。</summary>
        Manual = 0,
        /// <summary>网络连接断开或主动关闭。</summary>
        Disconnect = 1,
        /// <summary>房间成员关系正常结束。</summary>
        RoomLeave = 2,
        /// <summary>应用进入暂停或后台状态。</summary>
        ApplicationPause = 3,
        /// <summary>应用即将退出。</summary>
        ApplicationQuit = 4,
        /// <summary>检查点所有者即将释放。</summary>
        Dispose = 5
    }

    /// <summary>检查点生命周期 flush 的完成状态。</summary>
    public enum ReliableEventCheckpointFlushStatus
    {
        /// <summary>存储未提供 flush 能力，本次操作已跳过。</summary>
        Skipped = 0,
        /// <summary>存储已完成本次 flush。</summary>
        Succeeded = 1,
        /// <summary>存储抛出异常或报告了新的后台失败。</summary>
        Failed = 2,
        /// <summary>本次 flush 被调用方取消。</summary>
        Cancelled = 3,
        /// <summary>熔断器处于打开状态，本次 flush 未访问底层存储。</summary>
        CircuitOpen = 4
    }

    /// <summary>检查点生命周期协调器遇到持久化失败时采用的策略。</summary>
    public enum ReliableEventCheckpointFlushFailurePolicy
    {
        /// <summary>记录诊断并返回失败结果，不阻断断线或退出流程。</summary>
        CaptureAndContinue = 0,

        /// <summary>发布诊断后继续抛出异常，适用于测试门禁或强一致接入。</summary>
        ThrowAfterPublish = 1
    }

    /// <summary>检查点持久化熔断器的运行状态。</summary>
    public enum ReliableEventCheckpointCircuitState
    {
        /// <summary>允许正常访问底层存储。</summary>
        Closed = 0,
        /// <summary>暂时拒绝访问底层存储。</summary>
        Open = 1,
        /// <summary>冷却结束后仅允许一个探测 flush。</summary>
        HalfOpen = 2
    }

    /// <summary>检查点持久化熔断器选项。</summary>
    public sealed class ReliableEventCheckpointCircuitBreakerOptions
    {
        private int _failureThreshold = 3;
        private TimeSpan _breakDuration = TimeSpan.FromSeconds(10);

        /// <summary>连续失败达到该次数后打开熔断器。</summary>
        public int FailureThreshold
        {
            get => _failureThreshold;
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(FailureThreshold));
                _failureThreshold = value;
            }
        }

        /// <summary>熔断器保持打开状态的时长。</summary>
        public TimeSpan BreakDuration
        {
            get => _breakDuration;
            set
            {
                if (value < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(BreakDuration));
                _breakDuration = value;
            }
        }
    }

    /// <summary>熔断器拒绝检查点 flush 时返回或抛出的异常。</summary>
    public sealed class ReliableEventCheckpointCircuitOpenException : InvalidOperationException
    {
        /// <summary>创建熔断拒绝异常。</summary>
        public ReliableEventCheckpointCircuitOpenException(TimeSpan remainingDuration)
            : base("检查点持久化熔断器处于打开状态，本次 flush 已被拒绝。")
        {
            RemainingDuration = remainingDuration < TimeSpan.Zero
                ? TimeSpan.Zero
                : remainingDuration;
        }

        /// <summary>距离下一次半开探测的预计剩余时长。</summary>
        public TimeSpan RemainingDuration { get; }
    }

    /// <summary>检查点存储内部诊断快照。</summary>
    public readonly struct ReliableEventCheckpointStoreDiagnostics
    {
        /// <summary>创建存储内部诊断快照。</summary>
        public ReliableEventCheckpointStoreDiagnostics(
            int failureCount,
            Exception? lastFailure)
        {
            FailureCount = Math.Max(0, failureCount);
            LastFailure = lastFailure;
        }

        /// <summary>累计捕获的后台持久化失败次数。</summary>
        public int FailureCount { get; }

        /// <summary>最近一次后台持久化失败；尚未失败时为 <c>null</c>。</summary>
        public Exception? LastFailure { get; }
    }

    /// <summary>允许生命周期协调器读取存储内部已捕获的异步失败。</summary>
    public interface IReliableEventCheckpointStoreDiagnosticsProvider
    {
        /// <summary>读取存储当前的内部诊断快照。</summary>
        ReliableEventCheckpointStoreDiagnostics GetCheckpointStoreDiagnostics();
    }

    /// <summary>一次底层检查点 flush 失败后的重试决策上下文。</summary>
    public readonly struct ReliableEventCheckpointFlushRetryContext
    {
        /// <summary>创建重试决策上下文。</summary>
        public ReliableEventCheckpointFlushRetryContext(
            long attempt,
            int failedStoreAttempt,
            ReliableEventCheckpointFlushTrigger trigger,
            Exception failure)
        {
            Attempt = attempt;
            FailedStoreAttempt = Math.Max(1, failedStoreAttempt);
            Trigger = trigger;
            Failure = failure ?? throw new ArgumentNullException(nameof(failure));
        }

        /// <summary>生命周期协调器分配的 flush 序号。</summary>
        public long Attempt { get; }

        /// <summary>刚刚失败的底层存储尝试序号，从 1 开始。</summary>
        public int FailedStoreAttempt { get; }

        /// <summary>触发本次 flush 的生命周期原因。</summary>
        public ReliableEventCheckpointFlushTrigger Trigger { get; }

        /// <summary>底层存储抛出、超时或报告的异常。</summary>
        public Exception Failure { get; }
    }

    /// <summary>决定检查点 flush 失败后是否重试及等待时长。</summary>
    public interface IReliableEventCheckpointFlushRetryPolicy
    {
        /// <summary>返回 <c>true</c> 时按输出的非负时长等待后重试。</summary>
        bool TryGetRetryDelay(
            in ReliableEventCheckpointFlushRetryContext context,
            out TimeSpan delay);
    }

    /// <summary>提供有上限的指数退避重试策略。</summary>
    public sealed class ReliableEventCheckpointExponentialBackoffRetryPolicy :
        IReliableEventCheckpointFlushRetryPolicy
    {
        /// <summary>创建指数退避策略。</summary>
        public ReliableEventCheckpointExponentialBackoffRetryPolicy(
            int maxRetryCount = 2,
            TimeSpan? initialDelay = null,
            double multiplier = 2d,
            TimeSpan? maximumDelay = null)
        {
            if (maxRetryCount < 0) throw new ArgumentOutOfRangeException(nameof(maxRetryCount));
            if (multiplier < 1d || double.IsNaN(multiplier) || double.IsInfinity(multiplier))
                throw new ArgumentOutOfRangeException(nameof(multiplier));

            MaxRetryCount = maxRetryCount;
            InitialDelay = initialDelay ?? TimeSpan.FromMilliseconds(100);
            MaximumDelay = maximumDelay ?? TimeSpan.FromSeconds(2);
            if (InitialDelay < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(initialDelay));
            if (MaximumDelay < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(maximumDelay));
            Multiplier = multiplier;
        }

        /// <summary>首次 flush 之外允许执行的最大重试次数。</summary>
        public int MaxRetryCount { get; }

        /// <summary>第一次重试前的等待时长。</summary>
        public TimeSpan InitialDelay { get; }

        /// <summary>每次连续失败后应用到等待时长的倍率。</summary>
        public double Multiplier { get; }

        /// <summary>单次重试等待时长上限。</summary>
        public TimeSpan MaximumDelay { get; }

        /// <inheritdoc />
        public bool TryGetRetryDelay(
            in ReliableEventCheckpointFlushRetryContext context,
            out TimeSpan delay)
        {
            if (context.FailedStoreAttempt > MaxRetryCount)
            {
                delay = TimeSpan.Zero;
                return false;
            }

            var exponent = Math.Max(0, context.FailedStoreAttempt - 1);
            var milliseconds = InitialDelay.TotalMilliseconds * Math.Pow(Multiplier, exponent);
            delay = TimeSpan.FromMilliseconds(Math.Min(milliseconds, MaximumDelay.TotalMilliseconds));
            return true;
        }
    }

    /// <summary>指定某类生命周期 trigger 使用的完整 flush 执行策略。</summary>
    public readonly struct ReliableEventCheckpointFlushPolicy
    {
        /// <summary>创建 trigger 级 flush 执行策略。</summary>
        public ReliableEventCheckpointFlushPolicy(
            ReliableEventCheckpointFlushFailurePolicy failurePolicy,
            TimeSpan? flushAttemptTimeout = null,
            IReliableEventCheckpointFlushRetryPolicy? retryPolicy = null)
        {
            if (flushAttemptTimeout.HasValue &&
                flushAttemptTimeout.Value < TimeSpan.Zero &&
                flushAttemptTimeout.Value != Timeout.InfiniteTimeSpan)
            {
                throw new ArgumentOutOfRangeException(nameof(flushAttemptTimeout));
            }

            FailurePolicy = failurePolicy;
            FlushAttemptTimeout = flushAttemptTimeout;
            RetryPolicy = retryPolicy;
        }

        /// <summary>最终持久化失败是否继续抛出。</summary>
        public ReliableEventCheckpointFlushFailurePolicy FailurePolicy { get; }

        /// <summary>单次底层存储尝试的超时时长。</summary>
        public TimeSpan? FlushAttemptTimeout { get; }

        /// <summary>底层存储失败后采用的重试策略。</summary>
        public IReliableEventCheckpointFlushRetryPolicy? RetryPolicy { get; }
    }

    /// <summary>按生命周期 trigger 选择检查点 flush 执行策略。</summary>
    public interface IReliableEventCheckpointFlushPolicyResolver
    {
        /// <summary>尝试解析 trigger 的完整执行策略。</summary>
        bool TryResolve(
            ReliableEventCheckpointFlushTrigger trigger,
            out ReliableEventCheckpointFlushPolicy policy);
    }

    /// <summary>使用显式映射表解析 trigger 级 flush 执行策略。</summary>
    public sealed class ReliableEventCheckpointTriggerPolicyResolver :
        IReliableEventCheckpointFlushPolicyResolver
    {
        private readonly object _gate = new object();
        private readonly Dictionary<ReliableEventCheckpointFlushTrigger, ReliableEventCheckpointFlushPolicy>
            _policies = new Dictionary<ReliableEventCheckpointFlushTrigger, ReliableEventCheckpointFlushPolicy>();

        /// <summary>新增或替换指定 trigger 的执行策略。</summary>
        public ReliableEventCheckpointTriggerPolicyResolver Set(
            ReliableEventCheckpointFlushTrigger trigger,
            in ReliableEventCheckpointFlushPolicy policy)
        {
            lock (_gate) _policies[trigger] = policy;
            return this;
        }

        /// <summary>移除指定 trigger 的执行策略。</summary>
        public bool Remove(ReliableEventCheckpointFlushTrigger trigger)
        {
            lock (_gate) return _policies.Remove(trigger);
        }

        /// <inheritdoc />
        public bool TryResolve(
            ReliableEventCheckpointFlushTrigger trigger,
            out ReliableEventCheckpointFlushPolicy policy)
        {
            lock (_gate) return _policies.TryGetValue(trigger, out policy);
        }
    }

    /// <summary>一次已经安排的检查点 flush 重试通知。</summary>
    public readonly struct ReliableEventCheckpointFlushRetry
    {
        /// <summary>创建重试通知。</summary>
        public ReliableEventCheckpointFlushRetry(
            long attempt,
            int failedStoreAttempt,
            ReliableEventCheckpointFlushTrigger trigger,
            TimeSpan delay,
            Exception failure)
        {
            Attempt = attempt;
            FailedStoreAttempt = Math.Max(1, failedStoreAttempt);
            Trigger = trigger;
            Delay = delay < TimeSpan.Zero ? TimeSpan.Zero : delay;
            Failure = failure ?? throw new ArgumentNullException(nameof(failure));
        }

        /// <summary>生命周期协调器分配的 flush 序号。</summary>
        public long Attempt { get; }

        /// <summary>触发本次重试的底层存储尝试序号。</summary>
        public int FailedStoreAttempt { get; }

        /// <summary>触发本次 flush 的生命周期原因。</summary>
        public ReliableEventCheckpointFlushTrigger Trigger { get; }

        /// <summary>执行下一次尝试前的等待时长。</summary>
        public TimeSpan Delay { get; }

        /// <summary>触发本次重试的异常。</summary>
        public Exception Failure { get; }
    }

    /// <summary>接收检查点生命周期结构化诊断的扩展点。</summary>
    public interface IReliableEventCheckpointLifecycleDiagnosticsSink
    {
        /// <summary>协调器安排重试时调用。</summary>
        void OnRetryScheduled(in ReliableEventCheckpointFlushRetry retry);

        /// <summary>一次生命周期 flush 最终完成时调用。</summary>
        void OnFlushCompleted(in ReliableEventCheckpointFlushResult result);
    }

    /// <summary>通过委托把检查点生命周期诊断接入项目日志、指标或告警系统。</summary>
    public sealed class DelegatingReliableEventCheckpointLifecycleDiagnosticsSink :
        IReliableEventCheckpointLifecycleDiagnosticsSink
    {
        private readonly Action<ReliableEventCheckpointFlushRetry>? _onRetryScheduled;
        private readonly Action<ReliableEventCheckpointFlushResult>? _onFlushCompleted;

        /// <summary>使用可选的重试与完成回调创建诊断出口。</summary>
        public DelegatingReliableEventCheckpointLifecycleDiagnosticsSink(
            Action<ReliableEventCheckpointFlushRetry>? onRetryScheduled = null,
            Action<ReliableEventCheckpointFlushResult>? onFlushCompleted = null)
        {
            _onRetryScheduled = onRetryScheduled;
            _onFlushCompleted = onFlushCompleted;
        }

        /// <inheritdoc />
        public void OnRetryScheduled(in ReliableEventCheckpointFlushRetry retry)
        {
            _onRetryScheduled?.Invoke(retry);
        }

        /// <inheritdoc />
        public void OnFlushCompleted(in ReliableEventCheckpointFlushResult result)
        {
            _onFlushCompleted?.Invoke(result);
        }
    }

    /// <summary>检查点生命周期协调器选项。</summary>
    public sealed class ReliableEventCheckpointLifecycleOptions
    {
        private TimeSpan? _flushAttemptTimeout;

        /// <summary>持久化失败是否继续向调用方抛出。</summary>
        public ReliableEventCheckpointFlushFailurePolicy FailurePolicy { get; set; } =
            ReliableEventCheckpointFlushFailurePolicy.CaptureAndContinue;

        /// <summary>是否串行执行并发 flush，避免平台后端被重入。</summary>
        public bool SerializeConcurrentFlushes { get; set; } = true;

        /// <summary>是否把存储内部新增的后台失败视为本次 flush 失败。</summary>
        public bool TreatReportedStoreFailureAsFlushFailure { get; set; } = true;

        /// <summary>
        /// 单次底层存储尝试的超时时长；为 <c>null</c> 时不限制。
        /// 存储实现应响应取消令牌，以避免超时后仍在后台继续写入。
        /// </summary>
        public TimeSpan? FlushAttemptTimeout
        {
            get => _flushAttemptTimeout;
            set
            {
                if (value.HasValue &&
                    value.Value < TimeSpan.Zero &&
                    value.Value != Timeout.InfiniteTimeSpan)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(FlushAttemptTimeout),
                        "检查点 flush 超时时长不能为负数。");
                }

                _flushAttemptTimeout = value;
            }
        }

        /// <summary>底层存储尝试失败后的可插拔重试策略；为 <c>null</c> 时不重试。</summary>
        public IReliableEventCheckpointFlushRetryPolicy? RetryPolicy { get; set; }

        /// <summary>接收重试和最终结果的结构化诊断出口。</summary>
        public IReliableEventCheckpointLifecycleDiagnosticsSink? DiagnosticsSink { get; set; }

        /// <summary>按生命周期 trigger 覆盖超时、重试和最终失败策略。</summary>
        public IReliableEventCheckpointFlushPolicyResolver? TriggerPolicyResolver { get; set; }

        /// <summary>连续持久化失败后的熔断策略；为 <c>null</c> 时禁用熔断。</summary>
        public ReliableEventCheckpointCircuitBreakerOptions? CircuitBreaker { get; set; }
    }

    /// <summary>面向常见接入场景的检查点生命周期策略预设。</summary>
    public static class ReliableEventCheckpointLifecyclePresets
    {
        /// <summary>
        /// 创建面向在线客户端的韧性预设：有限超时、指数退避、失败隔离和连续失败熔断。
        /// 暂停与退出阶段使用更短的等待窗口。
        /// </summary>
        public static ReliableEventCheckpointLifecycleOptions CreateResilientClient()
        {
            var lifecycleRetry = new ReliableEventCheckpointExponentialBackoffRetryPolicy(
                maxRetryCount: 1,
                initialDelay: TimeSpan.FromMilliseconds(50),
                maximumDelay: TimeSpan.FromMilliseconds(100));
            var lifecyclePolicy = new ReliableEventCheckpointFlushPolicy(
                ReliableEventCheckpointFlushFailurePolicy.CaptureAndContinue,
                TimeSpan.FromSeconds(1),
                lifecycleRetry);
            var resolver = new ReliableEventCheckpointTriggerPolicyResolver();
            resolver.Set(ReliableEventCheckpointFlushTrigger.ApplicationPause, in lifecyclePolicy);
            resolver.Set(ReliableEventCheckpointFlushTrigger.ApplicationQuit, in lifecyclePolicy);
            resolver.Set(ReliableEventCheckpointFlushTrigger.Dispose, in lifecyclePolicy);

            return new ReliableEventCheckpointLifecycleOptions
            {
                FailurePolicy = ReliableEventCheckpointFlushFailurePolicy.CaptureAndContinue,
                FlushAttemptTimeout = TimeSpan.FromSeconds(2),
                RetryPolicy = new ReliableEventCheckpointExponentialBackoffRetryPolicy(
                    maxRetryCount: 2,
                    initialDelay: TimeSpan.FromMilliseconds(100),
                    maximumDelay: TimeSpan.FromSeconds(1)),
                TriggerPolicyResolver = resolver,
                CircuitBreaker = new ReliableEventCheckpointCircuitBreakerOptions
                {
                    FailureThreshold = 3,
                    BreakDuration = TimeSpan.FromSeconds(10)
                }
            };
        }

        /// <summary>创建适合测试门禁和强一致接入的严格预设。</summary>
        public static ReliableEventCheckpointLifecycleOptions CreateStrictValidation()
        {
            return new ReliableEventCheckpointLifecycleOptions
            {
                FailurePolicy = ReliableEventCheckpointFlushFailurePolicy.ThrowAfterPublish,
                FlushAttemptTimeout = TimeSpan.FromSeconds(5),
                RetryPolicy = new ReliableEventCheckpointExponentialBackoffRetryPolicy(
                    maxRetryCount: 1,
                    initialDelay: TimeSpan.FromMilliseconds(50),
                    maximumDelay: TimeSpan.FromMilliseconds(50))
            };
        }
    }

    /// <summary>单次生命周期 flush 的结构化结果。</summary>
    public readonly struct ReliableEventCheckpointFlushResult
    {
        /// <summary>创建单次 flush 的结构化结果。</summary>
        public ReliableEventCheckpointFlushResult(
            long attempt,
            ReliableEventCheckpointFlushTrigger trigger,
            ReliableEventCheckpointFlushStatus status,
            TimeSpan duration,
            Exception? failure)
            : this(
                attempt,
                trigger,
                status,
                duration,
                failure,
                status == ReliableEventCheckpointFlushStatus.Skipped ? 0 : 1)
        {
        }

        /// <summary>创建包含底层存储尝试次数的结构化结果。</summary>
        public ReliableEventCheckpointFlushResult(
            long attempt,
            ReliableEventCheckpointFlushTrigger trigger,
            ReliableEventCheckpointFlushStatus status,
            TimeSpan duration,
            Exception? failure,
            int storeAttemptCount)
        {
            Attempt = attempt;
            Trigger = trigger;
            Status = status;
            Duration = duration;
            Failure = failure;
            StoreAttemptCount = Math.Max(0, storeAttemptCount);
        }

        /// <summary>协调器为本次 flush 分配的递增序号。</summary>
        public long Attempt { get; }

        /// <summary>触发本次 flush 的生命周期原因。</summary>
        public ReliableEventCheckpointFlushTrigger Trigger { get; }

        /// <summary>本次 flush 的完成状态。</summary>
        public ReliableEventCheckpointFlushStatus Status { get; }

        /// <summary>本次 flush 的执行耗时。</summary>
        public TimeSpan Duration { get; }

        /// <summary>失败状态对应的异常；其他状态为 <c>null</c>。</summary>
        public Exception? Failure { get; }

        /// <summary>本次生命周期 flush 实际执行的底层存储尝试次数。</summary>
        public int StoreAttemptCount { get; }

        /// <summary>首次尝试之外实际执行的重试次数。</summary>
        public int RetryCount => Math.Max(0, StoreAttemptCount - 1);

        /// <summary>本次 flush 是否成功完成。</summary>
        public bool Succeeded => Status == ReliableEventCheckpointFlushStatus.Succeeded;
    }

    /// <summary>生命周期 flush 失败事件。</summary>
    public readonly struct ReliableEventCheckpointLifecycleFailure
    {
        /// <summary>创建生命周期 flush 失败事件。</summary>
        public ReliableEventCheckpointLifecycleFailure(
            long attempt,
            ReliableEventCheckpointFlushTrigger trigger,
            Exception exception)
        {
            Attempt = attempt;
            Trigger = trigger;
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
        }

        /// <summary>失败 flush 的递增序号。</summary>
        public long Attempt { get; }

        /// <summary>触发失败 flush 的生命周期原因。</summary>
        public ReliableEventCheckpointFlushTrigger Trigger { get; }

        /// <summary>存储抛出或报告的异常。</summary>
        public Exception Exception { get; }
    }

    /// <summary>检查点生命周期累计诊断。</summary>
    public readonly struct ReliableEventCheckpointLifecycleDiagnostics
    {
        /// <summary>创建检查点生命周期累计诊断快照。</summary>
        public ReliableEventCheckpointLifecycleDiagnostics(
            long attemptCount,
            long successCount,
            long failureCount,
            long cancelledCount,
            long skippedCount,
            int activeFlushCount,
            ReliableEventCheckpointFlushTrigger lastTrigger,
            ReliableEventCheckpointFlushStatus lastStatus,
            TimeSpan lastDuration,
            Exception? lastFailure)
            : this(
                attemptCount,
                successCount,
                failureCount,
                cancelledCount,
                skippedCount,
                activeFlushCount,
                retryCount: 0,
                timeoutCount: 0,
                lastStoreAttemptCount: 0,
                lastTrigger,
                lastStatus,
                lastDuration,
                lastFailure)
        {
        }

        /// <summary>创建包含重试和超时计数的检查点生命周期累计诊断快照。</summary>
        public ReliableEventCheckpointLifecycleDiagnostics(
            long attemptCount,
            long successCount,
            long failureCount,
            long cancelledCount,
            long skippedCount,
            int activeFlushCount,
            long retryCount,
            long timeoutCount,
            int lastStoreAttemptCount,
            ReliableEventCheckpointFlushTrigger lastTrigger,
            ReliableEventCheckpointFlushStatus lastStatus,
            TimeSpan lastDuration,
            Exception? lastFailure)
            : this(
                attemptCount,
                successCount,
                failureCount,
                cancelledCount,
                skippedCount,
                activeFlushCount,
                retryCount,
                timeoutCount,
                circuitOpenCount: 0,
                consecutiveFailureCount: 0,
                circuitState: ReliableEventCheckpointCircuitState.Closed,
                lastStoreAttemptCount,
                lastTrigger,
                lastStatus,
                lastDuration,
                lastFailure)
        {
        }

        /// <summary>创建包含重试、超时和熔断状态的累计诊断快照。</summary>
        public ReliableEventCheckpointLifecycleDiagnostics(
            long attemptCount,
            long successCount,
            long failureCount,
            long cancelledCount,
            long skippedCount,
            int activeFlushCount,
            long retryCount,
            long timeoutCount,
            long circuitOpenCount,
            int consecutiveFailureCount,
            ReliableEventCheckpointCircuitState circuitState,
            int lastStoreAttemptCount,
            ReliableEventCheckpointFlushTrigger lastTrigger,
            ReliableEventCheckpointFlushStatus lastStatus,
            TimeSpan lastDuration,
            Exception? lastFailure)
        {
            AttemptCount = attemptCount;
            SuccessCount = successCount;
            FailureCount = failureCount;
            CancelledCount = cancelledCount;
            SkippedCount = skippedCount;
            ActiveFlushCount = activeFlushCount;
            RetryCount = retryCount;
            TimeoutCount = timeoutCount;
            CircuitOpenCount = circuitOpenCount;
            ConsecutiveFailureCount = consecutiveFailureCount;
            CircuitState = circuitState;
            LastStoreAttemptCount = lastStoreAttemptCount;
            LastTrigger = lastTrigger;
            LastStatus = lastStatus;
            LastDuration = lastDuration;
            LastFailure = lastFailure;
        }

        /// <summary>已开始的 flush 累计次数。</summary>
        public long AttemptCount { get; }

        /// <summary>成功完成的 flush 累计次数。</summary>
        public long SuccessCount { get; }

        /// <summary>失败的 flush 累计次数。</summary>
        public long FailureCount { get; }

        /// <summary>被取消的 flush 累计次数。</summary>
        public long CancelledCount { get; }

        /// <summary>因存储不支持 flush 而跳过的累计次数。</summary>
        public long SkippedCount { get; }

        /// <summary>当前正在执行或等待存储完成的 flush 数量。</summary>
        public int ActiveFlushCount { get; }

        /// <summary>首次存储尝试之外实际执行的累计重试次数。</summary>
        public long RetryCount { get; }

        /// <summary>底层存储尝试超时的累计次数。</summary>
        public long TimeoutCount { get; }

        /// <summary>因熔断打开而拒绝 flush 的累计次数。</summary>
        public long CircuitOpenCount { get; }

        /// <summary>最近一次成功 flush 之后的连续失败次数。</summary>
        public int ConsecutiveFailureCount { get; }

        /// <summary>当前持久化熔断器状态。</summary>
        public ReliableEventCheckpointCircuitState CircuitState { get; }

        /// <summary>最近一次完成的生命周期 flush 实际执行的存储尝试次数。</summary>
        public int LastStoreAttemptCount { get; }

        /// <summary>最近一次完成的 flush 触发原因。</summary>
        public ReliableEventCheckpointFlushTrigger LastTrigger { get; }

        /// <summary>最近一次完成的 flush 状态。</summary>
        public ReliableEventCheckpointFlushStatus LastStatus { get; }

        /// <summary>最近一次完成的 flush 耗时。</summary>
        public TimeSpan LastDuration { get; }

        /// <summary>最近一次失败异常；尚未失败时为 <c>null</c>。</summary>
        public Exception? LastFailure { get; }
    }

    /// <summary>
    /// 将断线、离房、暂停和退出阶段的 flush 统一为可诊断、可配置的生命周期操作。
    /// </summary>
    public sealed class ReliableEventCheckpointLifecycleCoordinator
    {
        private readonly IReliableEventCheckpointStore? _store;
        private readonly ReliableEventCheckpointLifecycleOptions _options;
        private readonly SemaphoreSlim _flushGate = new SemaphoreSlim(1, 1);
        private readonly object _diagnosticsGate = new object();
        private long _attemptCount;
        private long _successCount;
        private long _failureCount;
        private long _cancelledCount;
        private long _skippedCount;
        private long _retryCount;
        private long _timeoutCount;
        private long _circuitOpenCount;
        private int _activeFlushCount;
        private int _consecutiveFailureCount;
        private ReliableEventCheckpointCircuitState _circuitState;
        private long _circuitOpenUntilTimestamp;
        private ReliableEventCheckpointFlushTrigger _lastTrigger;
        private ReliableEventCheckpointFlushStatus _lastStatus;
        private TimeSpan _lastDuration;
        private Exception? _lastFailure;
        private int _lastStoreAttemptCount;

        /// <summary>使用指定检查点存储和生命周期策略创建协调器。</summary>
        public ReliableEventCheckpointLifecycleCoordinator(
            IReliableEventCheckpointStore? store,
            ReliableEventCheckpointLifecycleOptions? options = null)
        {
            _store = store;
            _options = options ?? new ReliableEventCheckpointLifecycleOptions();
        }

        /// <summary>本次 flush 失败且完成诊断记录后触发。</summary>
        public event Action<ReliableEventCheckpointLifecycleFailure>? Failure;

        /// <summary>底层存储失败且重试策略安排下一次尝试时触发。</summary>
        public event Action<ReliableEventCheckpointFlushRetry>? RetryScheduled;

        /// <summary>执行带生命周期原因的 flush。</summary>
        public async Task<ReliableEventCheckpointFlushResult> FlushAsync(
            ReliableEventCheckpointFlushTrigger trigger,
            CancellationToken cancellationToken = default)
        {
            var serialize = _options.SerializeConcurrentFlushes;
            if (serialize)
            {
                await _flushGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            var attempt = Interlocked.Increment(ref _attemptCount);
            var started = Stopwatch.GetTimestamp();
            var storeAttemptCount = 0;
            var executionPolicy = ResolveExecutionPolicy(trigger);
            var circuitBreaker = _options.CircuitBreaker;
            var isHalfOpenProbe = false;
            Interlocked.Increment(ref _activeFlushCount);
            try
            {
                if (!(_store is IReliableEventCheckpointStoreFlushable flushable))
                {
                    return Complete(
                        attempt,
                        trigger,
                        ReliableEventCheckpointFlushStatus.Skipped,
                        started,
                        null,
                        storeAttemptCount);
                }

                if (!TryEnterCircuit(
                        circuitBreaker,
                        out isHalfOpenProbe,
                        out var circuitOpenFailure))
                {
                    return HandleCircuitOpen(
                        attempt,
                        trigger,
                        started,
                        circuitOpenFailure,
                        executionPolicy.FailurePolicy);
                }

                while (true)
                {
                    storeAttemptCount++;
                    try
                    {
                        await ExecuteStoreAttemptAsync(
                            flushable,
                            executionPolicy.FlushAttemptTimeout,
                            cancellationToken).ConfigureAwait(false);
                        RecordCircuitSuccess();
                        return Complete(
                            attempt,
                            trigger,
                            ReliableEventCheckpointFlushStatus.Succeeded,
                            started,
                            null,
                            storeAttemptCount);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        if (ex is TimeoutException)
                        {
                            Interlocked.Increment(ref _timeoutCount);
                        }

                        var retryContext = new ReliableEventCheckpointFlushRetryContext(
                            attempt,
                            storeAttemptCount,
                            trigger,
                            ex);
                        if (!TryGetRetryDelay(
                                in retryContext,
                                executionPolicy.RetryPolicy,
                                out var retryDelay,
                                out var retryPolicyFailure))
                        {
                            var finalFailure = retryPolicyFailure == null
                                ? ex
                                : new AggregateException(
                                    "检查点 flush 重试策略执行失败。",
                                    ex,
                                    retryPolicyFailure);
                            RecordCircuitFailure(circuitBreaker, isHalfOpenProbe);
                            return HandleFailure(
                                attempt,
                                trigger,
                                started,
                                finalFailure,
                                storeAttemptCount,
                                executionPolicy.FailurePolicy);
                        }

                        var retry = new ReliableEventCheckpointFlushRetry(
                            attempt,
                            storeAttemptCount,
                            trigger,
                            retryDelay,
                            ex);
                        PublishRetryScheduled(in retry);
                        if (retryDelay > TimeSpan.Zero)
                        {
                            await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
                        }

                        Interlocked.Increment(ref _retryCount);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                RecordCircuitCancellation(circuitBreaker, isHalfOpenProbe);
                Complete(
                    attempt,
                    trigger,
                    ReliableEventCheckpointFlushStatus.Cancelled,
                    started,
                    null,
                    storeAttemptCount);
                throw;
            }
            finally
            {
                Interlocked.Decrement(ref _activeFlushCount);
                if (serialize) _flushGate.Release();
            }
        }

        /// <summary>读取当前累计诊断快照。</summary>
        public ReliableEventCheckpointLifecycleDiagnostics GetDiagnostics()
        {
            lock (_diagnosticsGate)
            {
                return new ReliableEventCheckpointLifecycleDiagnostics(
                    Interlocked.Read(ref _attemptCount),
                    _successCount,
                    _failureCount,
                    _cancelledCount,
                    _skippedCount,
                    Volatile.Read(ref _activeFlushCount),
                    Interlocked.Read(ref _retryCount),
                    Interlocked.Read(ref _timeoutCount),
                    _circuitOpenCount,
                    _consecutiveFailureCount,
                    _circuitState,
                    _lastStoreAttemptCount,
                    _lastTrigger,
                    _lastStatus,
                    _lastDuration,
                    _lastFailure);
            }
        }

        private ReliableEventCheckpointFlushResult HandleFailure(
            long attempt,
            ReliableEventCheckpointFlushTrigger trigger,
            long started,
            Exception exception,
            int storeAttemptCount,
            ReliableEventCheckpointFlushFailurePolicy failurePolicy)
        {
            var result = Complete(
                attempt,
                trigger,
                ReliableEventCheckpointFlushStatus.Failed,
                started,
                exception,
                storeAttemptCount);
            PublishFailure(new ReliableEventCheckpointLifecycleFailure(
                attempt,
                trigger,
                exception));
            if (failurePolicy == ReliableEventCheckpointFlushFailurePolicy.ThrowAfterPublish)
            {
                throw exception;
            }

            return result;
        }

        private ReliableEventCheckpointFlushResult HandleCircuitOpen(
            long attempt,
            ReliableEventCheckpointFlushTrigger trigger,
            long started,
            ReliableEventCheckpointCircuitOpenException exception,
            ReliableEventCheckpointFlushFailurePolicy failurePolicy)
        {
            var result = Complete(
                attempt,
                trigger,
                ReliableEventCheckpointFlushStatus.CircuitOpen,
                started,
                exception,
                storeAttemptCount: 0);
            PublishFailure(new ReliableEventCheckpointLifecycleFailure(
                attempt,
                trigger,
                exception));
            if (failurePolicy == ReliableEventCheckpointFlushFailurePolicy.ThrowAfterPublish)
            {
                throw exception;
            }

            return result;
        }

        private ReliableEventCheckpointFlushResult Complete(
            long attempt,
            ReliableEventCheckpointFlushTrigger trigger,
            ReliableEventCheckpointFlushStatus status,
            long started,
            Exception? failure,
            int storeAttemptCount)
        {
            var elapsed = TimeSpan.FromSeconds(
                (Stopwatch.GetTimestamp() - started) / (double)Stopwatch.Frequency);
            lock (_diagnosticsGate)
            {
                switch (status)
                {
                    case ReliableEventCheckpointFlushStatus.Succeeded:
                        _successCount++;
                        break;
                    case ReliableEventCheckpointFlushStatus.Failed:
                        _failureCount++;
                        break;
                    case ReliableEventCheckpointFlushStatus.Cancelled:
                        _cancelledCount++;
                        break;
                    case ReliableEventCheckpointFlushStatus.CircuitOpen:
                        _circuitOpenCount++;
                        break;
                    default:
                        _skippedCount++;
                        break;
                }

                _lastTrigger = trigger;
                _lastStatus = status;
                _lastDuration = elapsed;
                _lastStoreAttemptCount = Math.Max(0, storeAttemptCount);
                if (failure != null) _lastFailure = failure;
            }

            var result = new ReliableEventCheckpointFlushResult(
                attempt,
                trigger,
                status,
                elapsed,
                failure,
                storeAttemptCount);
            PublishFlushCompleted(in result);
            return result;
        }

        private ReliableEventCheckpointFlushPolicy ResolveExecutionPolicy(
            ReliableEventCheckpointFlushTrigger trigger)
        {
            var fallback = new ReliableEventCheckpointFlushPolicy(
                _options.FailurePolicy,
                _options.FlushAttemptTimeout,
                _options.RetryPolicy);
            var resolver = _options.TriggerPolicyResolver;
            if (resolver == null) return fallback;

            try
            {
                return resolver.TryResolve(trigger, out var resolved)
                    ? resolved
                    : fallback;
            }
            catch
            {
                // 项目自定义解析器异常时回退到全局策略，避免生命周期清理流程失去 flush 机会。
                return fallback;
            }
        }

        private bool TryEnterCircuit(
            ReliableEventCheckpointCircuitBreakerOptions? options,
            out bool isHalfOpenProbe,
            out ReliableEventCheckpointCircuitOpenException failure)
        {
            lock (_diagnosticsGate)
            {
                isHalfOpenProbe = false;
                if (options == null)
                {
                    ResetCircuitNoLock();
                    failure = null!;
                    return true;
                }

                if (_circuitState == ReliableEventCheckpointCircuitState.Closed)
                {
                    failure = null!;
                    return true;
                }

                var now = Stopwatch.GetTimestamp();
                if (_circuitState == ReliableEventCheckpointCircuitState.Open &&
                    now >= _circuitOpenUntilTimestamp)
                {
                    _circuitState = ReliableEventCheckpointCircuitState.HalfOpen;
                    isHalfOpenProbe = true;
                    failure = null!;
                    return true;
                }

                var remaining = _circuitState == ReliableEventCheckpointCircuitState.Open
                    ? TimeSpan.FromSeconds(
                        Math.Max(0L, _circuitOpenUntilTimestamp - now) /
                        (double)Stopwatch.Frequency)
                    : TimeSpan.Zero;
                failure = new ReliableEventCheckpointCircuitOpenException(remaining);
                return false;
            }
        }

        private void RecordCircuitSuccess()
        {
            lock (_diagnosticsGate) ResetCircuitNoLock();
        }

        private void RecordCircuitFailure(
            ReliableEventCheckpointCircuitBreakerOptions? options,
            bool isHalfOpenProbe)
        {
            lock (_diagnosticsGate)
            {
                if (options == null)
                {
                    ResetCircuitNoLock();
                    return;
                }

                _consecutiveFailureCount++;
                if (!isHalfOpenProbe &&
                    _consecutiveFailureCount < options.FailureThreshold)
                {
                    _circuitState = ReliableEventCheckpointCircuitState.Closed;
                    return;
                }

                OpenCircuitNoLock(options.BreakDuration);
            }
        }

        private void RecordCircuitCancellation(
            ReliableEventCheckpointCircuitBreakerOptions? options,
            bool isHalfOpenProbe)
        {
            if (!isHalfOpenProbe) return;
            lock (_diagnosticsGate)
            {
                if (options == null)
                {
                    ResetCircuitNoLock();
                    return;
                }

                OpenCircuitNoLock(options.BreakDuration);
            }
        }

        private void OpenCircuitNoLock(TimeSpan breakDuration)
        {
            _circuitState = ReliableEventCheckpointCircuitState.Open;
            var now = Stopwatch.GetTimestamp();
            var durationTicks = breakDuration <= TimeSpan.Zero
                ? 0d
                : breakDuration.TotalSeconds * Stopwatch.Frequency;
            _circuitOpenUntilTimestamp = durationTicks >= long.MaxValue - now
                ? long.MaxValue
                : now + (long)durationTicks;
        }

        private void ResetCircuitNoLock()
        {
            _circuitState = ReliableEventCheckpointCircuitState.Closed;
            _consecutiveFailureCount = 0;
            _circuitOpenUntilTimestamp = 0L;
        }

        private async Task ExecuteStoreAttemptAsync(
            IReliableEventCheckpointStoreFlushable flushable,
            TimeSpan? flushAttemptTimeout,
            CancellationToken cancellationToken)
        {
            var before = ReadStoreDiagnostics();
            await ExecuteStoreFlushWithTimeoutAsync(
                flushable,
                flushAttemptTimeout,
                cancellationToken).ConfigureAwait(false);
            var after = ReadStoreDiagnostics();
            if (_options.TreatReportedStoreFailureAsFlushFailure &&
                after.FailureCount > before.FailureCount)
            {
                throw after.LastFailure ?? new InvalidOperationException(
                    "检查点存储报告了新的后台失败，但未提供异常详情。");
            }
        }

        private async Task ExecuteStoreFlushWithTimeoutAsync(
            IReliableEventCheckpointStoreFlushable flushable,
            TimeSpan? timeout,
            CancellationToken cancellationToken)
        {
            if (!timeout.HasValue || timeout.Value == Timeout.InfiniteTimeSpan)
            {
                await flushable.FlushAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            using (var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                       cancellationToken))
            {
                var flushTask = flushable.FlushAsync(timeoutCancellation.Token);
                if (flushTask.IsCompleted)
                {
                    await flushTask.ConfigureAwait(false);
                    return;
                }

                var timeoutTask = Task.Delay(timeout.Value, cancellationToken);
                var completed = await Task.WhenAny(flushTask, timeoutTask).ConfigureAwait(false);
                if (ReferenceEquals(completed, flushTask))
                {
                    await flushTask.ConfigureAwait(false);
                    return;
                }

                timeoutCancellation.Cancel();
                ObserveLateFlushFailure(flushTask);
                cancellationToken.ThrowIfCancellationRequested();
                throw new TimeoutException(
                    $"检查点存储 flush 在 {timeout.Value.TotalMilliseconds:0.###} 毫秒内未完成。");
            }
        }

        private bool TryGetRetryDelay(
            in ReliableEventCheckpointFlushRetryContext context,
            IReliableEventCheckpointFlushRetryPolicy? policy,
            out TimeSpan delay,
            out Exception? policyFailure)
        {
            if (policy == null)
            {
                delay = TimeSpan.Zero;
                policyFailure = null;
                return false;
            }

            try
            {
                policyFailure = null;
                if (!policy.TryGetRetryDelay(in context, out delay))
                {
                    delay = TimeSpan.Zero;
                    return false;
                }

                if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;
                return true;
            }
            catch (Exception ex)
            {
                delay = TimeSpan.Zero;
                policyFailure = ex;
                return false;
            }
        }

        private ReliableEventCheckpointStoreDiagnostics ReadStoreDiagnostics()
        {
            return _store is IReliableEventCheckpointStoreDiagnosticsProvider provider
                ? provider.GetCheckpointStoreDiagnostics()
                : default;
        }

        private static void ObserveLateFlushFailure(Task flushTask)
        {
            // 超时返回后继续观察不响应取消的存储任务，避免未观察异常进入终结器路径。
            _ = flushTask.ContinueWith(
                task => { _ = task.Exception; },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        private void PublishFailure(ReliableEventCheckpointLifecycleFailure failure)
        {
            try { Failure?.Invoke(failure); } catch { }
        }

        private void PublishRetryScheduled(in ReliableEventCheckpointFlushRetry retry)
        {
            try { RetryScheduled?.Invoke(retry); } catch { }
            try { _options.DiagnosticsSink?.OnRetryScheduled(in retry); } catch { }
        }

        private void PublishFlushCompleted(in ReliableEventCheckpointFlushResult result)
        {
            try { _options.DiagnosticsSink?.OnFlushCompleted(in result); } catch { }
        }
    }

    /// <summary>SDK 加载检查点时使用的策略。</summary>
    public enum ReliableEventCheckpointRestorePolicy
    {
        /// <summary>仅采用 <see cref="ReliableEventSessionOptions{TEvent}.InitialCheckpoint"/>。</summary>
        ExplicitOnly = 0,

        /// <summary>显式检查点优先；未提供时再按事件流从存储提供器加载。</summary>
        PreferExplicitThenStore = 1
    }

    /// <summary>线程安全的进程内检查点存储，适用于默认接入、测试和可替换持久化实现的前置阶段。</summary>
    public sealed class InMemoryReliableEventCheckpointStore : IReliableEventCheckpointStore
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, ReliableEventCheckpoint> _checkpoints =
            new Dictionary<string, ReliableEventCheckpoint>(StringComparer.Ordinal);

        /// <inheritdoc />
        public bool TryLoad(string streamId, out ReliableEventCheckpoint checkpoint)
        {
            if (string.IsNullOrWhiteSpace(streamId))
            {
                checkpoint = default;
                return false;
            }

            lock (_gate)
            {
                return _checkpoints.TryGetValue(streamId, out checkpoint) && checkpoint.IsValid;
            }
        }

        /// <inheritdoc />
        public void Save(in ReliableEventCheckpoint checkpoint)
        {
            if (!checkpoint.IsValid) return;

            lock (_gate)
            {
                if (_checkpoints.TryGetValue(checkpoint.StreamId, out var current) &&
                    string.Equals(current.TimelineId, checkpoint.TimelineId, StringComparison.Ordinal) &&
                    current.LastAcknowledgedSequence > checkpoint.LastAcknowledgedSequence)
                {
                    return;
                }

                _checkpoints[checkpoint.StreamId] = checkpoint;
            }
        }

        /// <inheritdoc />
        public bool Remove(string streamId)
        {
            if (string.IsNullOrWhiteSpace(streamId)) return false;

            lock (_gate)
            {
                return _checkpoints.Remove(streamId);
            }
        }

        /// <summary>删除全部进程内检查点。</summary>
        public void Clear()
        {
            lock (_gate)
            {
                _checkpoints.Clear();
            }
        }
    }

    /// <summary>将存储操作失败以结构化信息暴露给日志、监控和验收工具。</summary>
    public readonly struct ReliableEventCheckpointStoreFailure
    {
        /// <summary>创建存储失败信息。</summary>
        public ReliableEventCheckpointStoreFailure(
            string operation,
            string streamId,
            Exception exception)
        {
            Operation = operation ?? string.Empty;
            StreamId = streamId ?? string.Empty;
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
        }

        /// <summary>获取失败操作名称。</summary>
        public string Operation { get; }

        /// <summary>获取失败所属事件流。</summary>
        public string StreamId { get; }

        /// <summary>获取底层异常。</summary>
        public Exception Exception { get; }
    }

    /// <summary>缓冲存储的运行参数。</summary>
    public sealed class BufferedReliableEventCheckpointStoreOptions
    {
        /// <summary>Flush 时是否在后台写入失败后抛出最后一次异常。</summary>
        public bool ThrowOnFlushFailure { get; set; }
    }

    /// <summary>
    /// 通过委托适配 PlayerPrefs、远端 RPC 或项目自有数据库，避免平台依赖进入 SDK 核心程序集。
    /// </summary>
    public sealed class DelegatingReliableEventCheckpointStore :
        IReliableEventCheckpointStore,
        IReliableEventCheckpointStoreFlushable
    {
        private readonly Func<string, ReliableEventCheckpoint?> _load;
        private readonly Action<ReliableEventCheckpoint> _save;
        private readonly Func<string, bool> _remove;
        private readonly Func<CancellationToken, Task>? _flush;

        /// <summary>创建委托适配器。</summary>
        public DelegatingReliableEventCheckpointStore(
            Func<string, ReliableEventCheckpoint?> load,
            Action<ReliableEventCheckpoint> save,
            Func<string, bool> remove,
            Func<CancellationToken, Task>? flush = null)
        {
            _load = load ?? throw new ArgumentNullException(nameof(load));
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _remove = remove ?? throw new ArgumentNullException(nameof(remove));
            _flush = flush;
        }

        /// <inheritdoc />
        public bool TryLoad(string streamId, out ReliableEventCheckpoint checkpoint)
        {
            var value = _load(streamId);
            checkpoint = value ?? default;
            return value.HasValue && value.Value.IsValid;
        }

        /// <inheritdoc />
        public void Save(in ReliableEventCheckpoint checkpoint)
        {
            if (checkpoint.IsValid) _save(checkpoint);
        }

        /// <inheritdoc />
        public bool Remove(string streamId) => _remove(streamId);

        /// <inheritdoc />
        public Task FlushAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return _flush?.Invoke(cancellationToken) ?? Task.CompletedTask;
        }
    }

    /// <summary>基于简单文本记录的跨平台文件检查点存储。</summary>
    public sealed class FileReliableEventCheckpointStore : IReliableEventCheckpointStore
    {
        private readonly object _gate = new object();
        private readonly string _path;

        /// <summary>使用指定文件创建检查点存储；文件不存在时按空存储处理。</summary>
        public FileReliableEventCheckpointStore(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("文件路径不能为空。", nameof(path));
            _path = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        }

        /// <inheritdoc />
        public bool TryLoad(string streamId, out ReliableEventCheckpoint checkpoint)
        {
            lock (_gate)
            {
                var records = ReadRecords();
                return records.TryGetValue(streamId ?? string.Empty, out checkpoint) && checkpoint.IsValid;
            }
        }

        /// <inheritdoc />
        public void Save(in ReliableEventCheckpoint checkpoint)
        {
            if (!checkpoint.IsValid) return;

            lock (_gate)
            {
                var records = ReadRecords();
                if (records.TryGetValue(checkpoint.StreamId, out var current) &&
                    string.Equals(current.TimelineId, checkpoint.TimelineId, StringComparison.Ordinal) &&
                    current.LastAcknowledgedSequence > checkpoint.LastAcknowledgedSequence)
                {
                    return;
                }

                records[checkpoint.StreamId] = checkpoint;
                WriteRecords(records);
            }
        }

        /// <inheritdoc />
        public bool Remove(string streamId)
        {
            lock (_gate)
            {
                var records = ReadRecords();
                if (!records.Remove(streamId ?? string.Empty)) return false;
                WriteRecords(records);
                return true;
            }
        }

        private Dictionary<string, ReliableEventCheckpoint> ReadRecords()
        {
            var records = new Dictionary<string, ReliableEventCheckpoint>(StringComparer.Ordinal);
            if (!File.Exists(_path)) return records;

            foreach (var line in File.ReadAllLines(_path, Encoding.UTF8))
            {
                var fields = line.Split('\t');
                if (fields.Length != 3) continue;
                try
                {
                    var streamId = Decode(fields[0]);
                    var timelineId = Decode(fields[1]);
                    if (!long.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var sequence))
                        continue;
                    var checkpoint = new ReliableEventCheckpoint(streamId, timelineId, sequence);
                    if (checkpoint.IsValid) records[streamId] = checkpoint;
                }
                catch (FormatException)
                {
                    // 忽略损坏行，保留同一文件中仍可恢复的其他流。
                }
            }

            return records;
        }

        private void WriteRecords(Dictionary<string, ReliableEventCheckpoint> records)
        {
            var temporaryPath = _path + ".tmp." + Guid.NewGuid().ToString("N");
            var lines = new List<string>(records.Count);
            foreach (var pair in records)
            {
                lines.Add(
                    Encode(pair.Value.StreamId) + "\t" +
                    Encode(pair.Value.TimelineId) + "\t" +
                    pair.Value.LastAcknowledgedSequence.ToString(CultureInfo.InvariantCulture));
            }

            File.WriteAllLines(temporaryPath, lines, Encoding.UTF8);
            try
            {
                if (File.Exists(_path))
                {
                    File.Replace(temporaryPath, _path, null);
                }
                else
                {
                    File.Move(temporaryPath, _path);
                }
            }
            catch (PlatformNotSupportedException)
            {
                ReplaceWithoutFileReplace(temporaryPath);
            }
            catch (IOException)
            {
                ReplaceWithoutFileReplace(temporaryPath);
            }
        }

        private void ReplaceWithoutFileReplace(string temporaryPath)
        {
            if (File.Exists(_path)) File.Delete(_path);
            File.Move(temporaryPath, _path);
        }

        private static string Encode(string value) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));

        private static string Decode(string value) =>
            Encoding.UTF8.GetString(Convert.FromBase64String(value));
    }

    /// <summary>
    /// 将高频检查点保存合并到后台批量写入，避免 ACK 路径阻塞在文件或远端存储上。
    /// </summary>
    public sealed class BufferedReliableEventCheckpointStore :
        IReliableEventCheckpointStore,
        IReliableEventCheckpointStoreFlushable,
        IReliableEventCheckpointStoreDiagnosticsProvider,
        IDisposable
    {
        private sealed class PendingWrite
        {
            internal PendingWrite(ReliableEventCheckpoint checkpoint, long version)
            {
                Checkpoint = checkpoint;
                Version = version;
            }

            internal ReliableEventCheckpoint Checkpoint { get; }
            internal long Version { get; }
        }

        private readonly IReliableEventCheckpointStore _inner;
        private readonly BufferedReliableEventCheckpointStoreOptions _options;
        private readonly object _gate = new object();
        private readonly Dictionary<string, PendingWrite> _pending =
            new Dictionary<string, PendingWrite>(StringComparer.Ordinal);
        private readonly Dictionary<string, long> _versions =
            new Dictionary<string, long>(StringComparer.Ordinal);
        private readonly SemaphoreSlim _signal = new SemaphoreSlim(0);
        private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
        private readonly Task _worker;
        private Exception? _lastFailure;
        private int _inFlight;
        private bool _disposed;

        /// <summary>创建后台合并存储。</summary>
        public BufferedReliableEventCheckpointStore(
            IReliableEventCheckpointStore inner,
            BufferedReliableEventCheckpointStoreOptions? options = null)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _options = options ?? new BufferedReliableEventCheckpointStoreOptions();
            _worker = Task.Run(DrainLoopAsync);
        }

        /// <summary>后台写入失败时触发；回调异常会被吞掉以保护写入线程。</summary>
        public event Action<ReliableEventCheckpointStoreFailure>? Failure;

        /// <summary>获取最近一次后台写入异常。</summary>
        public Exception? LastFailure
        {
            get { lock (_gate) return _lastFailure; }
        }

        /// <summary>获取累计后台写入失败次数。</summary>
        public int FailureCount { get; private set; }

        /// <inheritdoc />
        public ReliableEventCheckpointStoreDiagnostics GetCheckpointStoreDiagnostics()
        {
            lock (_gate)
            {
                return new ReliableEventCheckpointStoreDiagnostics(
                    FailureCount,
                    _lastFailure);
            }
        }

        /// <inheritdoc />
        public bool TryLoad(string streamId, out ReliableEventCheckpoint checkpoint)
        {
            lock (_gate)
            {
                if (_pending.TryGetValue(streamId ?? string.Empty, out var pending))
                {
                    checkpoint = pending.Checkpoint;
                    return true;
                }
            }

            return _inner.TryLoad(streamId, out checkpoint);
        }

        /// <inheritdoc />
        public void Save(in ReliableEventCheckpoint checkpoint)
        {
            if (!checkpoint.IsValid) return;

            lock (_gate)
            {
                ThrowIfDisposed();
                var version = NextVersion(checkpoint.StreamId);
                _pending[checkpoint.StreamId] = new PendingWrite(checkpoint, version);
            }

            _signal.Release();
        }

        /// <inheritdoc />
        public bool Remove(string streamId)
        {
            if (string.IsNullOrWhiteSpace(streamId)) return false;

            lock (_gate)
            {
                ThrowIfDisposed();
                NextVersion(streamId);
                _pending.Remove(streamId);
            }

            return _inner.Remove(streamId);
        }

        /// <summary>等待所有已排队检查点完成写入。</summary>
        public async Task FlushAsync(CancellationToken cancellationToken = default)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Exception? failure;
                lock (_gate)
                {
                    failure = _lastFailure;
                    if (_pending.Count == 0 && _inFlight == 0)
                    {
                        if (_options.ThrowOnFlushFailure && failure != null) throw failure;
                        return;
                    }
                }

                await Task.Delay(1, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>退出时同步等待后台写入并释放工作线程资源。</summary>
        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
            }

            try
            {
                FlushAsync().GetAwaiter().GetResult();
            }
            finally
            {
                _cancellation.Cancel();
                _signal.Release();
                try { _worker.GetAwaiter().GetResult(); } catch (OperationCanceledException) { }
                _cancellation.Dispose();
                _signal.Dispose();
                if (_inner is IDisposable disposable) disposable.Dispose();
            }
        }

        private async Task DrainLoopAsync()
        {
            try
            {
                while (true)
                {
                    await _signal.WaitAsync(_cancellation.Token).ConfigureAwait(false);
                    while (true)
                    {
                        PendingWrite[] writes;
                        lock (_gate)
                        {
                            if (_pending.Count == 0) break;
                            writes = new PendingWrite[_pending.Count];
                            _pending.Values.CopyTo(writes, 0);
                            _pending.Clear();
                            _inFlight += writes.Length;
                        }

                        foreach (var write in writes)
                        {
                            try
                            {
                                lock (_gate)
                                {
                                    if (!IsCurrent(write)) continue;

                                    var checkpoint = write.Checkpoint;
                                    _inner.Save(in checkpoint);
                                }
                            }
                            catch (Exception exception)
                            {
                                PublishFailure("save", write.Checkpoint.StreamId, exception);
                            }
                            finally
                            {
                                lock (_gate) _inFlight--;
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
            {
            }
        }

        private bool IsCurrent(PendingWrite write)
        {
            return _versions.TryGetValue(write.Checkpoint.StreamId, out var version) &&
                   version == write.Version;
        }

        private long NextVersion(string streamId)
        {
            var version = _versions.TryGetValue(streamId, out var current) ? current + 1L : 1L;
            _versions[streamId] = version;
            return version;
        }

        private void PublishFailure(string operation, string streamId, Exception exception)
        {
            Action<ReliableEventCheckpointStoreFailure>? handler;
            lock (_gate)
            {
                _lastFailure = exception;
                FailureCount++;
                handler = Failure;
            }

            try { handler?.Invoke(new ReliableEventCheckpointStoreFailure(operation, streamId, exception)); }
            catch { }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(BufferedReliableEventCheckpointStore));
        }
    }

    /// <summary>可靠事件会话装配失败的稳定分类。</summary>
    public enum ReliableEventSessionBuildFailureReason
    {
        /// <summary>没有提供可用的事件流标识。</summary>
        MissingStreamId = 0,
        /// <summary>没有提供标准游标所需的事件字段描述器。</summary>
        MissingDescriptor = 1,
        /// <summary>同时配置了自定义游标和标准游标参数，装配目标不明确。</summary>
        AmbiguousCursorConfiguration = 2,
        /// <summary>没有提供业务事件处理器。</summary>
        MissingEventSink = 3,
        /// <summary>没有提供时间线失效处理器。</summary>
        MissingTimelineInvalidationHandler = 4,
        /// <summary>自动 ACK 模式没有提供远端确认操作。</summary>
        MissingAcknowledgementOperation = 5,
        /// <summary>初始检查点不适用于本次事件流。</summary>
        InvalidInitialCheckpoint = 6,
        /// <summary>自定义游标无法由通用构建器恢复初始检查点。</summary>
        InitialCheckpointRequiresStandardCursor = 7,
        /// <summary>要求应用协商策略，但没有提供同步会话描述。</summary>
        MissingNegotiatedSession = 8,
        /// <summary>同步 Profile 没有启用可靠事件交付。</summary>
        ReliableEventsNotNegotiated = 9,
        /// <summary>协商策略要求持久化检查点，但没有提供保存操作。</summary>
        MissingCheckpointSaveOperation = 10,
        /// <summary>自定义游标无法由框架保证协商要求的乱序缓存策略。</summary>
        BufferedDeliveryRequiresStandardCursor = 11,
        /// <summary>同步会话描述没有通过能力协商。</summary>
        IncompatibleNegotiatedSession = 12,
        /// <summary>存储提供器返回了无效或属于其他事件流的检查点。</summary>
        InvalidStoredCheckpoint = 13
    }

    /// <summary>从已协商同步会话派生的可靠事件运行策略快照。</summary>
    public readonly struct ReliableEventSessionPolicy
    {
        internal ReliableEventSessionPolicy(ReliableEventPolicy profilePolicy)
        {
            ProfilePolicy = profilePolicy;
            AcknowledgementStrategy = (profilePolicy & ReliableEventPolicy.AutomaticAcknowledgement) != 0
                ? ReliableEventAcknowledgementStrategy.Automatic
                : (profilePolicy & ReliableEventPolicy.ExternalAcknowledgement) != 0
                    ? ReliableEventAcknowledgementStrategy.External
                    : ReliableEventAcknowledgementStrategy.Disabled;
            GapPolicy = (profilePolicy & ReliableEventPolicy.BufferedOutOfOrder) != 0
                ? ReliableEventGapPolicy.BufferWithinCapacity
                : ReliableEventGapPolicy.Reject;
            PersistsCheckpoint = (profilePolicy & ReliableEventPolicy.PersistentCheckpoint) != 0;
            UsesAuthoritativeBaselineRecovery =
                (profilePolicy & ReliableEventPolicy.AuthoritativeBaselineRecovery) != 0;
        }

        /// <summary>获取 Profile 选择的原始可靠事件策略位。</summary>
        public ReliableEventPolicy ProfilePolicy { get; }

        /// <summary>获取框架派生的 ACK 所有权。</summary>
        public ReliableEventAcknowledgementStrategy AcknowledgementStrategy { get; }

        /// <summary>获取框架派生的序列空洞处理策略。</summary>
        public ReliableEventGapPolicy GapPolicy { get; }

        /// <summary>获取本次会话是否要求持久化确认游标。</summary>
        public bool PersistsCheckpoint { get; }

        /// <summary>获取本次会话是否支持通过权威基线恢复事件时间线。</summary>
        public bool UsesAuthoritativeBaselineRecovery { get; }
    }

    /// <summary>可靠事件会话无法完成装配时抛出的异常。</summary>
    public sealed class ReliableEventSessionBuildException : InvalidOperationException
    {
        internal ReliableEventSessionBuildException(
            ReliableEventSessionBuildFailureReason reason,
            string message)
            : base(message)
        {
            Reason = reason;
        }

        /// <summary>获取可供日志、测试和工具稳定判断的失败原因。</summary>
        public ReliableEventSessionBuildFailureReason Reason { get; }
    }

    /// <summary>
    /// 可靠事件会话装配选项。可提供自定义兼容游标，或由 SDK 根据流标识和描述器创建标准游标。
    /// </summary>
    public sealed class ReliableEventSessionOptions<TEvent>
    {
        /// <summary>标准游标使用的事件流标识。</summary>
        public string? StreamId { get; set; }

        /// <summary>标准游标读取业务事件字段的描述器。</summary>
        public ReliableEventDescriptor<TEvent>? Descriptor { get; set; }

        /// <summary>标准游标配置；提供自定义游标时必须为空。</summary>
        public ReliableEventCursorOptions? CursorOptions { get; set; }

        /// <summary>接入方提供的兼容游标；设置后不再由 SDK 创建标准游标。</summary>
        public IReliableEventDeliveryCursor<TEvent>? Cursor { get; set; }

        /// <summary>标准游标开始会话前需要恢复的可选检查点。</summary>
        public ReliableEventCheckpoint? InitialCheckpoint { get; set; }

        /// <summary>用于自动加载、保存和清理可靠事件检查点的存储提供器。</summary>
        public IReliableEventCheckpointStore? CheckpointStore { get; set; }

        /// <summary>框架创建会话时选择检查点来源的策略。</summary>
        public ReliableEventCheckpointRestorePolicy CheckpointRestorePolicy { get; set; } =
            ReliableEventCheckpointRestorePolicy.PreferExplicitThenStore;

        /// <summary>可靠事件交付状态机配置。</summary>
        public ReliableEventDeliveryOptions DeliveryOptions { get; set; } =
            new ReliableEventDeliveryOptions();

        /// <summary>已通过本地与远端能力协商的同步会话描述。</summary>
        public NetworkSyncSessionDescriptor? NegotiatedSession { get; set; }

        /// <summary>是否由框架根据 <see cref="NegotiatedSession"/> 派生 ACK、乱序和恢复策略。</summary>
        public bool ApplyNegotiatedPolicy { get; set; }

        /// <summary>业务事件处理器；成功返回后框架才提交投递位置。</summary>
        public Action<TEvent>? EventSink { get; set; }

        /// <summary>时间线失效处理器，通常用于触发全量快照恢复。</summary>
        public Action<ReliableEventDeliveryFailure>? TimelineInvalidated { get; set; }

        /// <summary>自动 ACK 模式使用的远端确认操作。</summary>
        public Func<string, long, Task<long>>? Acknowledge { get; set; }

        /// <summary>确认位置变化后的可选检查点保存操作。</summary>
        public Action<ReliableEventCheckpoint>? SaveCheckpoint { get; set; }

        /// <summary>所有可观察交付失败的可选诊断回调。</summary>
        public Action<ReliableEventDeliveryFailure>? FailureObserved { get; set; }

        /// <summary>会话创建后是否先等待权威基线。</summary>
        public bool AwaitAuthoritativeBaseline { get; set; } = true;

        internal ReliableEventSessionOptions<TEvent> Snapshot()
        {
            if (DeliveryOptions == null) throw new ArgumentNullException(nameof(DeliveryOptions));

            return new ReliableEventSessionOptions<TEvent>
            {
                StreamId = StreamId,
                Descriptor = Descriptor,
                CursorOptions = CloneCursorOptions(CursorOptions),
                Cursor = Cursor,
                InitialCheckpoint = InitialCheckpoint,
                CheckpointStore = CheckpointStore,
                CheckpointRestorePolicy = CheckpointRestorePolicy,
                DeliveryOptions = CloneDeliveryOptions(DeliveryOptions),
                NegotiatedSession = NegotiatedSession,
                ApplyNegotiatedPolicy = ApplyNegotiatedPolicy,
                EventSink = EventSink,
                TimelineInvalidated = TimelineInvalidated,
                Acknowledge = Acknowledge,
                SaveCheckpoint = SaveCheckpoint,
                FailureObserved = FailureObserved,
                AwaitAuthoritativeBaseline = AwaitAuthoritativeBaseline
            };
        }

        private static ReliableEventCursorOptions? CloneCursorOptions(
            ReliableEventCursorOptions? options)
        {
            if (options == null) return null;

            return new ReliableEventCursorOptions
            {
                GapPolicy = options.GapPolicy,
                MaxPendingEvents = options.MaxPendingEvents,
                BaselineAcknowledgementPolicy = options.BaselineAcknowledgementPolicy,
                RequireBaselineAtObservedWatermark = options.RequireBaselineAtObservedWatermark,
                InferRetentionGapFromFirstAvailableSequence =
                    options.InferRetentionGapFromFirstAvailableSequence,
                BindTimelineOnAdmission = options.BindTimelineOnAdmission
            };
        }

        private static ReliableEventDeliveryOptions CloneDeliveryOptions(
            ReliableEventDeliveryOptions options)
        {
            return new ReliableEventDeliveryOptions
            {
                MaxPendingBatches = options.MaxPendingBatches,
                MaxAcknowledgementAttempts = options.MaxAcknowledgementAttempts,
                AcknowledgementStrategy = options.AcknowledgementStrategy,
                AcknowledgeAuthoritativeBaseline = options.AcknowledgeAuthoritativeBaseline,
                ReplayOnlyMatchingTimeline = options.ReplayOnlyMatchingTimeline,
                InvalidateOnEventSinkFailure = options.InvalidateOnEventSinkFailure,
                AcknowledgementRetryDelay = options.AcknowledgementRetryDelay
            };
        }
    }

    /// <summary>持有已完成装配的可靠事件游标和交付运行时。</summary>
    public sealed class ReliableEventSession<TEvent> : IDisposable
    {
        private readonly ReliableEventDeliveryRuntime<TEvent> _delivery;
        private readonly IReliableEventCheckpointStore? _checkpointStore;

        internal ReliableEventSession(
            IReliableEventDeliveryCursor<TEvent> cursor,
            ReliableEventDeliveryRuntime<TEvent> delivery,
            ReliableEventSessionPolicy? negotiatedPolicy,
            IReliableEventCheckpointStore? checkpointStore)
        {
            Cursor = cursor;
            _delivery = delivery;
            NegotiatedPolicy = negotiatedPolicy;
            _checkpointStore = checkpointStore;
        }

        /// <summary>获取本次会话实际使用的可靠事件游标。</summary>
        public IReliableEventDeliveryCursor<TEvent> Cursor { get; }

        /// <summary>获取由同步会话协商结果派生的策略；兼容装配模式下为空。</summary>
        public ReliableEventSessionPolicy? NegotiatedPolicy { get; }

        /// <summary>获取事件流标识。</summary>
        public string StreamId => _delivery.StreamId;

        /// <summary>获取当前时间线标识。</summary>
        public string TimelineId => _delivery.TimelineId;

        /// <summary>获取最后成功投递的序列号。</summary>
        public long LastDeliveredSequence => _delivery.LastDeliveredSequence;

        /// <summary>获取最后确认的序列号。</summary>
        public long LastAcknowledgedSequence => _delivery.LastAcknowledgedSequence;

        /// <summary>获取从服务端批次观察到的最高水位。</summary>
        public long LastObservedWatermark => Cursor.LastObservedWatermark;

        /// <summary>获取当前是否等待权威基线。</summary>
        public bool AwaitingBaseline => _delivery.AwaitingBaseline;

        /// <summary>获取等待权威基线期间缓存的批次数。</summary>
        public int PendingBatchCount => _delivery.PendingBatchCount;

        /// <summary>处理一个协议无关可靠事件批次。</summary>
        public void Handle(in ReliableEventBatch<TEvent> batch)
        {
            _delivery.Handle(in batch);
        }

        /// <summary>要求在继续投递前采用新的权威基线。</summary>
        public void RequireAuthoritativeBaseline()
        {
            _delivery.RequireAuthoritativeBaseline();
        }

        /// <summary>丢弃游标中尚未形成连续投递区间的缓存事件。</summary>
        public void DiscardPending()
        {
            Cursor.DiscardPending();
        }

        /// <summary>创建仅包含当前确认位置的持久化检查点。</summary>
        public ReliableEventCheckpoint CreateCheckpoint()
        {
            return Cursor.CreateCheckpoint();
        }

        /// <summary>从已配置的存储提供器删除当前事件流检查点。</summary>
        public bool RemoveStoredCheckpoint()
        {
            return _checkpointStore?.Remove(StreamId) ?? false;
        }

        /// <summary>等待当前 store 中已排队的检查点完成写入；不支持 flush 的 store 立即完成。</summary>
        public Task FlushCheckpointStoreAsync(CancellationToken cancellationToken = default)
        {
            return _checkpointStore is IReliableEventCheckpointStoreFlushable flushable
                ? flushable.FlushAsync(cancellationToken)
                : Task.CompletedTask;
        }

        /// <summary>采用权威事件水位，并回放符合配置的缓存批次。</summary>
        public bool AdoptAuthoritativeBaseline(string timelineId, long eventWatermark)
        {
            return _delivery.AdoptAuthoritativeBaseline(timelineId, eventWatermark);
        }

        /// <summary>结束会话，并使尚未完成的异步 ACK 失效。</summary>
        public void Dispose()
        {
            _delivery.Dispose();
        }
    }

    /// <summary>校验可靠事件配置并创建具有独立世代的会话句柄。</summary>
    public sealed class ReliableEventSessionBuilder<TEvent>
    {
        private readonly ReliableEventSessionOptions<TEvent> _options;

        /// <summary>创建构建器并持有接入选项快照。</summary>
        public ReliableEventSessionBuilder(ReliableEventSessionOptions<TEvent> options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            _options = options.Snapshot();
        }

        /// <summary>完成游标创建或绑定、检查点恢复和交付运行时装配。</summary>
        public ReliableEventSession<TEvent> Build()
        {
            var negotiatedPolicy = ApplyNegotiatedPolicy();
            ValidateCallbacks();
            var cursor = CreateCursor();
            var delivery = new ReliableEventDeliveryRuntime<TEvent>(_options.DeliveryOptions);
            var saveCheckpoint = CreateCheckpointSaveOperation();
            delivery.BeginGeneration(
                cursor,
                _options.EventSink!,
                _options.TimelineInvalidated!,
                _options.Acknowledge,
                saveCheckpoint,
                _options.FailureObserved,
                _options.AwaitAuthoritativeBaseline);
            return new ReliableEventSession<TEvent>(
                cursor,
                delivery,
                negotiatedPolicy,
                _options.CheckpointStore);
        }

        private ReliableEventSessionPolicy? ApplyNegotiatedPolicy()
        {
            if (!_options.ApplyNegotiatedPolicy)
            {
                return null;
            }

            var session = _options.NegotiatedSession;
            if (session == null)
            {
                throw Failure(
                    ReliableEventSessionBuildFailureReason.MissingNegotiatedSession,
                    "应用协商可靠事件策略时必须提供同步会话描述。");
            }

            if (!session.ConfigurationReport.IsValid)
            {
                throw Failure(
                    ReliableEventSessionBuildFailureReason.IncompatibleNegotiatedSession,
                    "同步会话描述未通过能力协商，不能用于装配可靠事件运行时。");
            }

            var profilePolicy = session.Profile.ReliableEvent;
            if (profilePolicy == ReliableEventPolicy.None)
            {
                throw Failure(
                    ReliableEventSessionBuildFailureReason.ReliableEventsNotNegotiated,
                    $"同步 Profile '{session.ProfileName}' 没有启用可靠事件交付。");
            }

            var policy = new ReliableEventSessionPolicy(profilePolicy);
            if (policy.GapPolicy == ReliableEventGapPolicy.BufferWithinCapacity &&
                _options.Cursor != null)
            {
                throw Failure(
                    ReliableEventSessionBuildFailureReason.BufferedDeliveryRequiresStandardCursor,
                    "协商后的乱序缓存策略必须使用由 SDK 创建并配置的标准游标。");
            }

            _options.DeliveryOptions.AcknowledgementStrategy = policy.AcknowledgementStrategy;
            _options.DeliveryOptions.AcknowledgeAuthoritativeBaseline =
                policy.UsesAuthoritativeBaselineRecovery &&
                policy.AcknowledgementStrategy == ReliableEventAcknowledgementStrategy.Automatic;
            _options.DeliveryOptions.ReplayOnlyMatchingTimeline = true;

            if (_options.Cursor == null)
            {
                var cursorOptions = _options.CursorOptions ?? new ReliableEventCursorOptions();
                cursorOptions.GapPolicy = policy.GapPolicy;
                if (policy.UsesAuthoritativeBaselineRecovery)
                {
                    cursorOptions.BaselineAcknowledgementPolicy =
                        policy.AcknowledgementStrategy == ReliableEventAcknowledgementStrategy.External
                            ? ReliableEventBaselineAcknowledgementPolicy.ConfirmWatermark
                            : ReliableEventBaselineAcknowledgementPolicy.PreserveConfirmedWithinWatermark;
                    cursorOptions.RequireBaselineAtObservedWatermark = true;
                    cursorOptions.InferRetentionGapFromFirstAvailableSequence = true;
                    cursorOptions.BindTimelineOnAdmission = true;
                }

                _options.CursorOptions = cursorOptions;
            }

            return policy;
        }

        private void ValidateCallbacks()
        {
            if (_options.EventSink == null)
            {
                throw Failure(
                    ReliableEventSessionBuildFailureReason.MissingEventSink,
                    "可靠事件会话必须提供业务事件处理器。");
            }

            if (_options.TimelineInvalidated == null)
            {
                throw Failure(
                    ReliableEventSessionBuildFailureReason.MissingTimelineInvalidationHandler,
                    "可靠事件会话必须提供时间线失效处理器。");
            }

            if (_options.DeliveryOptions.AcknowledgementStrategy ==
                    ReliableEventAcknowledgementStrategy.Automatic &&
                _options.Acknowledge == null)
            {
                throw Failure(
                    ReliableEventSessionBuildFailureReason.MissingAcknowledgementOperation,
                    "自动 ACK 模式必须提供远端确认操作。");
            }

            if (_options.ApplyNegotiatedPolicy &&
                _options.NegotiatedSession != null &&
                (_options.NegotiatedSession.Profile.ReliableEvent &
                 ReliableEventPolicy.PersistentCheckpoint) != 0 &&
                _options.SaveCheckpoint == null &&
                _options.CheckpointStore == null)
            {
                throw Failure(
                    ReliableEventSessionBuildFailureReason.MissingCheckpointSaveOperation,
                    "协商策略启用持久化检查点时必须提供存储提供器或保存操作。");
            }
        }

        private IReliableEventDeliveryCursor<TEvent> CreateCursor()
        {
            if (_options.Cursor != null)
            {
                if (_options.Descriptor != null ||
                    _options.CursorOptions != null ||
                    !string.IsNullOrWhiteSpace(_options.StreamId))
                {
                    throw Failure(
                        ReliableEventSessionBuildFailureReason.AmbiguousCursorConfiguration,
                        "提供自定义游标时不能同时配置标准游标参数。");
                }

                var checkpoint = ResolveInitialCheckpoint(_options.Cursor.StreamId);
                if (checkpoint.HasValue)
                {
                    var restoredCheckpoint = checkpoint.Value;
                    if (_options.Cursor is not IReliableEventCheckpointRestorable restorable ||
                        !restorable.TryRestore(in restoredCheckpoint))
                    {
                        throw Failure(
                            ReliableEventSessionBuildFailureReason.InitialCheckpointRequiresStandardCursor,
                            "自定义游标必须实现通用检查点恢复接口，或由接入方在构建会话前完成恢复。");
                    }
                }

                return _options.Cursor;
            }

            if (string.IsNullOrWhiteSpace(_options.StreamId))
            {
                throw Failure(
                    ReliableEventSessionBuildFailureReason.MissingStreamId,
                    "由 SDK 创建标准游标时必须提供事件流标识。");
            }

            if (_options.Descriptor == null)
            {
                throw Failure(
                    ReliableEventSessionBuildFailureReason.MissingDescriptor,
                    "由 SDK 创建标准游标时必须提供事件字段描述器。");
            }

            var cursor = new ReliableEventCursor<TEvent>(
                _options.StreamId!,
                _options.Descriptor,
                _options.CursorOptions);
            var initialCheckpoint = ResolveInitialCheckpoint(cursor.StreamId);
            if (initialCheckpoint.HasValue)
            {
                var checkpoint = initialCheckpoint.Value;
                if (!cursor.TryRestore(in checkpoint))
                {
                    throw Failure(
                        ReliableEventSessionBuildFailureReason.InvalidInitialCheckpoint,
                        "初始检查点的事件流或时间线标识无效。");
                }
            }

            return cursor;
        }

        private ReliableEventCheckpoint? ResolveInitialCheckpoint(string streamId)
        {
            if (_options.InitialCheckpoint.HasValue)
            {
                return _options.InitialCheckpoint;
            }

            if (_options.CheckpointRestorePolicy == ReliableEventCheckpointRestorePolicy.ExplicitOnly ||
                _options.CheckpointStore == null ||
                !_options.CheckpointStore.TryLoad(streamId, out var checkpoint))
            {
                return null;
            }

            if (!checkpoint.IsValid ||
                !string.Equals(checkpoint.StreamId, streamId, StringComparison.Ordinal))
            {
                throw Failure(
                    ReliableEventSessionBuildFailureReason.InvalidStoredCheckpoint,
                    "检查点存储提供器返回了无效或属于其他事件流的确认位置。");
            }

            return checkpoint;
        }

        private Action<ReliableEventCheckpoint>? CreateCheckpointSaveOperation()
        {
            var store = _options.CheckpointStore;
            var observer = _options.SaveCheckpoint;
            if (store == null) return observer;
            if (observer == null) return checkpoint => store.Save(in checkpoint);

            return checkpoint =>
            {
                store.Save(in checkpoint);
                observer(checkpoint);
            };
        }

        private static ReliableEventSessionBuildException Failure(
            ReliableEventSessionBuildFailureReason reason,
            string message)
        {
            return new ReliableEventSessionBuildException(reason, message);
        }
    }
}
