#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Conditioning;
using AbilityKit.Network.Runtime.Sync;

namespace AbilityKit.Demo.Harness
{
    /// <summary>
    /// 同步 DemoHarness 用来驱动单个示例 carrier 的玩法无关适配器。
    /// Shooter、Moba 或未来示例都可以实现它，而不需要把具体玩法类型泄漏到框架层。
    /// </summary>
    public interface ISyncDemoCarrier
    {
        string CarrierName { get; }

        NetworkSyncModel SyncModel { get; }

        DemoHarnessStepTelemetry Step(in DemoHarnessStepContext context);
    }

    public enum SyncDemoCapabilityStatus
    {
        Supported = 0,
        Unsupported = 1,
        Degraded = 2
    }

    public readonly struct SyncDemoCapabilityResult
    {
        public SyncDemoCapabilityResult(SyncDemoCapabilityStatus status, string reason)
        {
            Status = status;
            Reason = reason ?? string.Empty;
        }

        public SyncDemoCapabilityStatus Status { get; }

        public string Reason { get; }

        public bool CanRun => Status == SyncDemoCapabilityStatus.Supported || Status == SyncDemoCapabilityStatus.Degraded;

        public static SyncDemoCapabilityResult Supported { get; } = new SyncDemoCapabilityResult(SyncDemoCapabilityStatus.Supported, string.Empty);

        public static SyncDemoCapabilityResult Unsupported(string reason)
        {
            return new SyncDemoCapabilityResult(SyncDemoCapabilityStatus.Unsupported, reason);
        }

        public static SyncDemoCapabilityResult Degraded(string reason)
        {
            return new SyncDemoCapabilityResult(SyncDemoCapabilityStatus.Degraded, reason);
        }
    }

    public interface ISyncDemoCarrierCapabilities
    {
        SyncDemoCapabilityResult Supports(in NetworkSyncProfile profile, in NetworkConditionProfile networkProfile);
    }

    /// <summary>
    /// DemoHarness 集成可复用的 carrier 能力选择器。
    /// </summary>
    public static class DemoHarnessCarrierSelector
    {
        public static bool CanRun(
            ISyncDemoCarrier carrier,
            in NetworkSyncProfile profile,
            in NetworkConditionProfile networkProfile)
        {
            if (carrier == null) throw new ArgumentNullException(nameof(carrier));

            if (carrier is ISyncDemoCarrierCapabilities capabilities)
            {
                return capabilities.Supports(in profile, in networkProfile).CanRun;
            }

            return carrier.SyncModel == profile.CompatibilityModel;
        }

        public static bool TrySelectFirst(
            IEnumerable<ISyncDemoCarrier?> carriers,
            in NetworkSyncProfile profile,
            in NetworkConditionProfile networkProfile,
            out ISyncDemoCarrier carrier)
        {
            if (carriers == null) throw new ArgumentNullException(nameof(carriers));

            foreach (var candidate in carriers)
            {
                if (candidate == null)
                {
                    continue;
                }

                if (CanRun(candidate, in profile, in networkProfile))
                {
                    carrier = candidate;
                    return true;
                }
            }

            carrier = null!;
            return false;
        }

        public static ISyncDemoCarrier SelectFirstOrThrow(
            IEnumerable<ISyncDemoCarrier?> carriers,
            in NetworkSyncProfile profile,
            in NetworkConditionProfile networkProfile,
            string subjectName = "demo harness carrier")
        {
            if (string.IsNullOrWhiteSpace(subjectName)) throw new ArgumentException("Subject name is required.", nameof(subjectName));

            if (TrySelectFirst(carriers, in profile, in networkProfile, out var carrier))
            {
                return carrier;
            }

            throw new NotSupportedException(
                $"No {subjectName} supports sync profile '{profile.CompatibilityModel}' under the requested network profile.");
        }
    }

    public enum DemoHarnessRunStatus
    {
        Completed = 0,
        Unsupported = 1,
        Degraded = 2,
        Failed = 3
    }

    /// <summary>
    /// 描述一次 A x B x C 演示运行：同步模型、网络档案与 carrier。
    /// </summary>
    public readonly struct DemoHarnessScenario
    {
        public DemoHarnessScenario(
            string name,
            NetworkSyncModel syncModel,
            NetworkConditionProfile networkProfile,
            string carrierName,
            int stepCount,
            float deltaSeconds,
            int seed = 0)
            : this(
                name,
                NetworkSyncProfiles.FromCompatibilityModel(syncModel),
                networkProfile,
                carrierName,
                stepCount,
                deltaSeconds,
                seed)
        {
        }

        public DemoHarnessScenario(
            string name,
            NetworkSyncProfile syncProfile,
            NetworkConditionProfile networkProfile,
            string carrierName,
            int stepCount,
            float deltaSeconds,
            int seed = 0)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Scenario name is required.", nameof(name));
            if (string.IsNullOrWhiteSpace(carrierName)) throw new ArgumentException("Carrier name is required.", nameof(carrierName));
            if (stepCount <= 0) throw new ArgumentOutOfRangeException(nameof(stepCount));
            if (deltaSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(deltaSeconds));

            Name = name;
            SyncProfile = syncProfile;
            NetworkProfile = networkProfile;
            CarrierName = carrierName;
            StepCount = stepCount;
            DeltaSeconds = deltaSeconds;
            Seed = seed;
        }

        public string Name { get; }

        public NetworkSyncModel SyncModel => SyncProfile.CompatibilityModel;

        public NetworkConditionProfile NetworkProfile { get; }

        public NetworkSyncProfile SyncProfile { get; }

        public string CarrierName { get; }

        public int StepCount { get; }

        public float DeltaSeconds { get; }

        public int Seed { get; }
    }

    public static class DemoHarnessScenarioMatrix
    {
        public static IReadOnlyList<DemoHarnessScenario> Build(
            string namePrefix,
            IEnumerable<NetworkSyncProfile> syncProfiles,
            IEnumerable<NetworkConditionProfile> networkProfiles,
            IEnumerable<string> carrierNames,
            int stepCount,
            float deltaSeconds,
            int seed = 0)
        {
            if (namePrefix == null) throw new ArgumentNullException(nameof(namePrefix));
            if (syncProfiles == null) throw new ArgumentNullException(nameof(syncProfiles));
            if (networkProfiles == null) throw new ArgumentNullException(nameof(networkProfiles));
            if (carrierNames == null) throw new ArgumentNullException(nameof(carrierNames));

            var profiles = CopyProfiles(syncProfiles);
            var networks = CopyNetworkProfiles(networkProfiles);
            var carriers = CopyCarrierNames(carrierNames);
            var scenarios = new List<DemoHarnessScenario>(profiles.Count * networks.Count * carriers.Count);
            for (var carrierIndex = 0; carrierIndex < carriers.Count; carrierIndex++)
            {
                for (var profileIndex = 0; profileIndex < profiles.Count; profileIndex++)
                {
                    for (var networkIndex = 0; networkIndex < networks.Count; networkIndex++)
                    {
                        var profile = profiles[profileIndex];
                        scenarios.Add(new DemoHarnessScenario(
                            FormatName(namePrefix, carriers[carrierIndex], profile, networkIndex),
                            profile,
                            networks[networkIndex],
                            carriers[carrierIndex],
                            stepCount,
                            deltaSeconds,
                            seed));
                    }
                }
            }

            return scenarios.AsReadOnly();
        }

        public static IReadOnlyList<DemoHarnessScenario> BuildRunnable(
            string namePrefix,
            IEnumerable<NetworkSyncProfile> syncProfiles,
            IEnumerable<NetworkConditionProfile> networkProfiles,
            IEnumerable<ISyncDemoCarrier?> carriers,
            int stepCount,
            float deltaSeconds,
            int seed = 0)
        {
            if (namePrefix == null) throw new ArgumentNullException(nameof(namePrefix));
            if (syncProfiles == null) throw new ArgumentNullException(nameof(syncProfiles));
            if (networkProfiles == null) throw new ArgumentNullException(nameof(networkProfiles));
            if (carriers == null) throw new ArgumentNullException(nameof(carriers));

            var profiles = CopyProfiles(syncProfiles);
            var networks = CopyNetworkProfiles(networkProfiles);
            var carrierList = CopyCarriers(carriers);
            var scenarios = new List<DemoHarnessScenario>(profiles.Count * networks.Count * carrierList.Count);
            for (var carrierIndex = 0; carrierIndex < carrierList.Count; carrierIndex++)
            {
                var carrier = carrierList[carrierIndex];
                for (var profileIndex = 0; profileIndex < profiles.Count; profileIndex++)
                {
                    var profile = profiles[profileIndex];
                    for (var networkIndex = 0; networkIndex < networks.Count; networkIndex++)
                    {
                        var network = networks[networkIndex];
                        if (!DemoHarnessCarrierSelector.CanRun(carrier, in profile, in network))
                        {
                            continue;
                        }

                        scenarios.Add(new DemoHarnessScenario(
                            FormatName(namePrefix, carrier.CarrierName, profile, networkIndex),
                            profile,
                            network,
                            carrier.CarrierName,
                            stepCount,
                            deltaSeconds,
                            seed));
                    }
                }
            }

            return scenarios.AsReadOnly();
        }

        private static string FormatName(string namePrefix, string carrierName, in NetworkSyncProfile profile, int networkIndex)
        {
            return string.IsNullOrWhiteSpace(namePrefix)
                ? $"{carrierName} {profile.CompatibilityModel} network#{networkIndex}"
                : $"{namePrefix} {carrierName} {profile.CompatibilityModel} network#{networkIndex}";
        }

        private static List<NetworkSyncProfile> CopyProfiles(IEnumerable<NetworkSyncProfile> syncProfiles)
        {
            var profiles = new List<NetworkSyncProfile>();
            foreach (var profile in syncProfiles)
            {
                profiles.Add(profile);
            }

            return profiles;
        }

        private static List<NetworkConditionProfile> CopyNetworkProfiles(IEnumerable<NetworkConditionProfile> networkProfiles)
        {
            var profiles = new List<NetworkConditionProfile>();
            foreach (var profile in networkProfiles)
            {
                profiles.Add(profile);
            }

            return profiles;
        }

        private static List<string> CopyCarrierNames(IEnumerable<string> carrierNames)
        {
            var names = new List<string>();
            foreach (var carrierName in carrierNames)
            {
                if (string.IsNullOrWhiteSpace(carrierName))
                {
                    throw new ArgumentException("Carrier name is required.", nameof(carrierNames));
                }

                names.Add(carrierName);
            }

            return names;
        }

        private static List<ISyncDemoCarrier> CopyCarriers(IEnumerable<ISyncDemoCarrier?> carriers)
        {
            var carrierList = new List<ISyncDemoCarrier>();
            foreach (var carrier in carriers)
            {
                if (carrier == null)
                {
                    continue;
                }

                carrierList.Add(carrier);
            }

            return carrierList;
        }
    }

    public readonly struct DemoHarnessStepContext
    {
        public DemoHarnessStepContext(in DemoHarnessScenario scenario, int stepIndex, float elapsedSeconds)
            : this(
                in scenario,
                stepIndex,
                SyncTimeAnchor.FromLocalFrame(stepIndex, stepIndex, elapsedSeconds))
        {
        }

        public DemoHarnessStepContext(in DemoHarnessScenario scenario, int stepIndex, in SyncTimeAnchor timeAnchor)
        {
            if (stepIndex < 0) throw new ArgumentOutOfRangeException(nameof(stepIndex));

            Scenario = scenario;
            StepIndex = stepIndex;
            TimeAnchor = timeAnchor;
        }

        public DemoHarnessScenario Scenario { get; }

        public int StepIndex { get; }

        public SyncTimeAnchor TimeAnchor { get; }

        public float ElapsedSeconds => (float)TimeAnchor.ElapsedSeconds;

        public float DeltaSeconds => Scenario.DeltaSeconds;
    }

    public readonly struct DemoHarnessStepTelemetry
    {
        private static readonly SyncHealthEvent[] EmptyEvents = Array.Empty<SyncHealthEvent>();

        private readonly SyncHealthEvent[]? _healthEvents;

        public DemoHarnessStepTelemetry(
            SyncTickResult tickResult,
            SyncReconciliationReport reconciliationReport,
            NetworkConditioningStats networkStats,
            double remoteJitter = 0d,
            long acceptedHits = 0,
            long rejectedHits = 0,
            params SyncHealthEvent[]? healthEvents)
        {
            if (remoteJitter < 0d) throw new ArgumentOutOfRangeException(nameof(remoteJitter));
            if (acceptedHits < 0) throw new ArgumentOutOfRangeException(nameof(acceptedHits));
            if (rejectedHits < 0) throw new ArgumentOutOfRangeException(nameof(rejectedHits));

            TickResult = tickResult;
            ReconciliationReport = reconciliationReport;
            NetworkStats = networkStats;
            RemoteJitter = remoteJitter;
            AcceptedHits = acceptedHits;
            RejectedHits = rejectedHits;
            _healthEvents = healthEvents != null && healthEvents.Length > 0 ? healthEvents : null;
        }

        public SyncTickResult TickResult { get; }

        public SyncReconciliationReport ReconciliationReport { get; }

        public NetworkConditioningStats NetworkStats { get; }

        public double RemoteJitter { get; }

        public long AcceptedHits { get; }

        public long RejectedHits { get; }

        /// <summary>
        /// Gameplay-agnostic health signals emitted during this step (snapshot flow, interpolation,
        /// recovery, input, validation). Empty when the carrier only reports reconciliation data.
        /// </summary>
        public IReadOnlyList<SyncHealthEvent> HealthEvents => _healthEvents ?? EmptyEvents;
    }

    public readonly struct DemoHarnessMetrics
    {
        private readonly SyncHealthReport? _healthReport;

        public DemoHarnessMetrics(
            int stepsRun,
            int totalTicks,
            int lastFrame,
            int reconciliationCount,
            int fullSnapshotRequestCount,
            int totalReplayTicks,
            int maxReplayTicks,
            double totalRemoteJitter,
            double maxRemoteJitter,
            long acceptedHits,
            long rejectedHits,
            NetworkConditioningStats networkStats,
            int healthEventCount = 0,
            int healthWarningCount = 0,
            int healthErrorCount = 0,
            SyncHealthReport? healthReport = null)
        {
            StepsRun = stepsRun;
            TotalTicks = totalTicks;
            LastFrame = lastFrame;
            ReconciliationCount = reconciliationCount;
            FullSnapshotRequestCount = fullSnapshotRequestCount;
            TotalReplayTicks = totalReplayTicks;
            MaxReplayTicks = maxReplayTicks;
            TotalRemoteJitter = totalRemoteJitter;
            MaxRemoteJitter = maxRemoteJitter;
            AcceptedHits = acceptedHits;
            RejectedHits = rejectedHits;
            NetworkStats = networkStats;
            HealthEventCount = healthEventCount;
            HealthWarningCount = healthWarningCount;
            HealthErrorCount = healthErrorCount;
            _healthReport = healthReport;
        }

        public int StepsRun { get; }

        public int TotalTicks { get; }

        public int LastFrame { get; }

        public int ReconciliationCount { get; }

        public int FullSnapshotRequestCount { get; }

        public int TotalReplayTicks { get; }

        public int MaxReplayTicks { get; }

        public double TotalRemoteJitter { get; }

        public double MaxRemoteJitter { get; }

        public double AverageRemoteJitter => StepsRun == 0 ? 0d : TotalRemoteJitter / StepsRun;

        public long AcceptedHits { get; }

        public long RejectedHits { get; }

        public NetworkConditioningStats NetworkStats { get; }

        /// <summary>Total number of <see cref="SyncHealthEvent"/>s observed across all steps.</summary>
        public int HealthEventCount { get; }

        /// <summary>Number of observed health events with <see cref="SyncHealthSeverity.Warning"/> severity.</summary>
        public int HealthWarningCount { get; }

        /// <summary>Number of observed health events with <see cref="SyncHealthSeverity.Error"/> severity.</summary>
        public int HealthErrorCount { get; }

        /// <summary>Machine-readable health summary grouped by event kind and severity.</summary>
        public SyncHealthReport HealthReport => _healthReport ?? SyncHealthReport.Empty;
    }

    public readonly struct DemoHarnessRunResult
    {
        public DemoHarnessRunResult(DemoHarnessScenario scenario, bool completed, string failureReason, DemoHarnessMetrics metrics)
            : this(scenario, completed ? DemoHarnessRunStatus.Completed : DemoHarnessRunStatus.Failed, failureReason, metrics)
        {
        }

        public DemoHarnessRunResult(DemoHarnessScenario scenario, DemoHarnessRunStatus status, string reason, DemoHarnessMetrics metrics)
        {
            Scenario = scenario;
            Status = status;
            Reason = reason ?? string.Empty;
            Metrics = metrics;
        }

        public DemoHarnessScenario Scenario { get; }

        public DemoHarnessRunStatus Status { get; }

        public bool Completed => Status == DemoHarnessRunStatus.Completed || Status == DemoHarnessRunStatus.Degraded;

        public string FailureReason => Status == DemoHarnessRunStatus.Failed ? Reason : string.Empty;

        public string Reason { get; }

        public DemoHarnessMetrics Metrics { get; }
    }

    public readonly struct DemoHarnessBatchSummaryRow
    {
        public DemoHarnessBatchSummaryRow(string carrierName, NetworkSyncModel syncModel, DemoHarnessRunStatus status, int count)
        {
            if (string.IsNullOrWhiteSpace(carrierName)) throw new ArgumentException("Carrier name is required.", nameof(carrierName));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));

            CarrierName = carrierName;
            SyncModel = syncModel;
            Status = status;
            Count = count;
        }

        public string CarrierName { get; }

        public NetworkSyncModel SyncModel { get; }

        public DemoHarnessRunStatus Status { get; }

        public int Count { get; }
    }

    public readonly struct DemoHarnessBatchSummary
    {
        public DemoHarnessBatchSummary(IReadOnlyList<DemoHarnessRunResult> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));

            var rows = new List<DemoHarnessBatchSummaryRow>();
            for (var i = 0; i < results.Count; i++)
            {
                var result = results[i];
                var scenario = result.Scenario;
                var rowIndex = FindRow(rows, scenario.CarrierName, scenario.SyncModel, result.Status);
                if (rowIndex >= 0)
                {
                    var row = rows[rowIndex];
                    rows[rowIndex] = new DemoHarnessBatchSummaryRow(row.CarrierName, row.SyncModel, row.Status, row.Count + 1);
                }
                else
                {
                    rows.Add(new DemoHarnessBatchSummaryRow(scenario.CarrierName, scenario.SyncModel, result.Status, 1));
                }
            }

            Rows = rows.AsReadOnly();
        }

        public IReadOnlyList<DemoHarnessBatchSummaryRow> Rows { get; }

        public int CountFor(string carrierName, NetworkSyncModel syncModel, DemoHarnessRunStatus status)
        {
            if (carrierName == null) throw new ArgumentNullException(nameof(carrierName));

            var rowIndex = FindRow(Rows, carrierName, syncModel, status);
            return rowIndex < 0 ? 0 : Rows[rowIndex].Count;
        }

        private static int FindRow(IReadOnlyList<DemoHarnessBatchSummaryRow> rows, string carrierName, NetworkSyncModel syncModel, DemoHarnessRunStatus status)
        {
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (string.Equals(row.CarrierName, carrierName, StringComparison.Ordinal) &&
                    row.SyncModel == syncModel &&
                    row.Status == status)
                {
                    return i;
                }
            }

            return -1;
        }
    }

    public readonly struct DemoHarnessBatchResult
    {
        public DemoHarnessBatchResult(IReadOnlyList<DemoHarnessRunResult> results)
        {
            Results = results ?? throw new ArgumentNullException(nameof(results));
            Summary = new DemoHarnessBatchSummary(results);

            var completed = 0;
            var unsupported = 0;
            var degraded = 0;
            var failed = 0;
            for (var i = 0; i < results.Count; i++)
            {
                var result = results[i];
                if (result.Completed)
                {
                    completed++;
                }

                if (result.Status == DemoHarnessRunStatus.Unsupported)
                {
                    unsupported++;
                }
                else if (result.Status == DemoHarnessRunStatus.Degraded)
                {
                    degraded++;
                }
                else if (result.Status == DemoHarnessRunStatus.Failed)
                {
                    failed++;
                }
            }

            ScenarioCount = results.Count;
            CompletedCount = completed;
            UnsupportedCount = unsupported;
            DegradedCount = degraded;
            FailedCount = failed;
        }

        public IReadOnlyList<DemoHarnessRunResult> Results { get; }

        public DemoHarnessBatchSummary Summary { get; }

        public int ScenarioCount { get; }

        public int CompletedCount { get; }

        public int UnsupportedCount { get; }

        public int DegradedCount { get; }

        public int FailedCount { get; }

        public bool AllCompleted => CompletedCount == ScenarioCount;
    }

    public sealed class DemoHarnessRunner
    {
        private readonly Func<DemoHarnessScenario, SyncClock> _clockFactory;

        public DemoHarnessRunner()
            : this(null)
        {
        }

        /// <summary>
        /// Creates a runner with a custom clock factory. Hosts inject a factory backed by a
        /// <see cref="TimeSyncBridge"/> so produced anchors carry an estimated server clock; the
        /// default factory builds a bare local-frame clock with no server-time stamping.
        /// </summary>
        public DemoHarnessRunner(Func<DemoHarnessScenario, SyncClock>? clockFactory)
        {
            _clockFactory = clockFactory ?? DefaultClockFactory;
        }

        private static SyncClock DefaultClockFactory(DemoHarnessScenario scenario)
        {
            return new SyncClock(scenario.DeltaSeconds);
        }

        public DemoHarnessBatchResult RunMany(IEnumerable<DemoHarnessScenario> scenarios, IEnumerable<ISyncDemoCarrier?> carriers)
        {
            if (scenarios == null) throw new ArgumentNullException(nameof(scenarios));
            if (carriers == null) throw new ArgumentNullException(nameof(carriers));

            var carrierList = new List<ISyncDemoCarrier>();
            foreach (var carrier in carriers)
            {
                if (carrier != null)
                {
                    carrierList.Add(carrier);
                }
            }

            var results = new List<DemoHarnessRunResult>();
            foreach (var scenario in scenarios)
            {
                var match = FindCarrier(in scenario, carrierList);
                if (match.Carrier == null)
                {
                    results.Add(match.UnsupportedReason.Length == 0
                        ? Failed(scenario, $"No carrier found for '{scenario.CarrierName}' with sync profile '{scenario.SyncModel}'.")
                        : Unsupported(scenario, match.UnsupportedReason));
                    continue;
                }

                results.Add(Run(in scenario, match.Carrier, match.Capability));
            }

            return new DemoHarnessBatchResult(results.AsReadOnly());
        }

        public DemoHarnessRunResult Run(in DemoHarnessScenario scenario, ISyncDemoCarrier carrier)
        {
            if (carrier == null) throw new ArgumentNullException(nameof(carrier));

            if (!string.Equals(carrier.CarrierName, scenario.CarrierName, StringComparison.Ordinal))
            {
                return Failed(scenario, $"Carrier mismatch: scenario requires '{scenario.CarrierName}', carrier is '{carrier.CarrierName}'.");
            }

            var capability = EvaluateCapability(in scenario, carrier);
            if (!capability.CanRun)
            {
                return carrier is ISyncDemoCarrierCapabilities
                    ? Unsupported(scenario, capability.Reason)
                    : Failed(scenario, $"Sync profile mismatch: scenario requires '{scenario.SyncModel}', carrier is '{carrier.SyncModel}'.");
            }

            return Run(in scenario, carrier, capability);
        }

        private DemoHarnessRunResult Run(in DemoHarnessScenario scenario, ISyncDemoCarrier carrier, SyncDemoCapabilityResult capability)
        {
            var metrics = new MetricsBuilder();
            var clock = _clockFactory(scenario) ?? new SyncClock(scenario.DeltaSeconds);
            for (var step = 0; step < scenario.StepCount; step++)
            {
                var timeAnchor = clock.Advance();
                var context = new DemoHarnessStepContext(in scenario, step, timeAnchor);
                var telemetry = carrier.Step(in context);
                metrics.Observe(in telemetry);
            }

            var status = capability.Status == SyncDemoCapabilityStatus.Degraded
                ? DemoHarnessRunStatus.Degraded
                : DemoHarnessRunStatus.Completed;
            return new DemoHarnessRunResult(scenario, status, capability.Reason, metrics.Build());
        }

        private static DemoHarnessRunResult Failed(in DemoHarnessScenario scenario, string reason)
        {
            return new DemoHarnessRunResult(scenario, DemoHarnessRunStatus.Failed, reason, default);
        }

        private static DemoHarnessRunResult Unsupported(in DemoHarnessScenario scenario, string reason)
        {
            return new DemoHarnessRunResult(scenario, DemoHarnessRunStatus.Unsupported, reason, default);
        }

        private static CarrierMatch FindCarrier(in DemoHarnessScenario scenario, IReadOnlyList<ISyncDemoCarrier> carriers)
        {
            var unsupportedReason = string.Empty;
            for (var i = 0; i < carriers.Count; i++)
            {
                var carrier = carriers[i];
                if (!string.Equals(carrier.CarrierName, scenario.CarrierName, StringComparison.Ordinal))
                {
                    continue;
                }

                var capability = EvaluateCapability(in scenario, carrier);
                if (capability.CanRun)
                {
                    return new CarrierMatch(carrier, capability, string.Empty);
                }

                if (carrier is ISyncDemoCarrierCapabilities)
                {
                    unsupportedReason = capability.Reason.Length == 0
                        ? $"Carrier '{scenario.CarrierName}' does not support sync profile '{scenario.SyncModel}'."
                        : capability.Reason;
                }
            }

            return new CarrierMatch(null, default, unsupportedReason);
        }

        private static SyncDemoCapabilityResult EvaluateCapability(in DemoHarnessScenario scenario, ISyncDemoCarrier carrier)
        {
            if (carrier is ISyncDemoCarrierCapabilities capabilities)
            {
                return capabilities.Supports(scenario.SyncProfile, scenario.NetworkProfile);
            }

            return carrier.SyncModel == scenario.SyncModel
                ? SyncDemoCapabilityResult.Supported
                : SyncDemoCapabilityResult.Unsupported($"Sync profile mismatch: scenario requires '{scenario.SyncModel}', carrier is '{carrier.SyncModel}'.");
        }

        private readonly struct CarrierMatch
        {
            public CarrierMatch(ISyncDemoCarrier? carrier, SyncDemoCapabilityResult capability, string unsupportedReason)
            {
                Carrier = carrier;
                Capability = capability;
                UnsupportedReason = unsupportedReason ?? string.Empty;
            }

            public ISyncDemoCarrier? Carrier { get; }

            public SyncDemoCapabilityResult Capability { get; }

            public string UnsupportedReason { get; }
        }

        private sealed class MetricsBuilder
        {
            private int _stepsRun;
            private int _totalTicks;
            private int _lastFrame;
            private int _reconciliationCount;
            private int _fullSnapshotRequestCount;
            private int _totalReplayTicks;
            private int _maxReplayTicks;
            private double _totalRemoteJitter;
            private double _maxRemoteJitter;
            private long _acceptedHits;
            private long _rejectedHits;
            private NetworkConditioningStats _networkStats;
            private readonly SyncHealthEventAggregator _health =
                new SyncHealthEventAggregator();

            public void Observe(in DemoHarnessStepTelemetry telemetry)
            {
                _stepsRun++;
                _totalTicks += telemetry.TickResult.Ticks;
                _lastFrame = telemetry.TickResult.Frame;

                var report = telemetry.ReconciliationReport;
                if (report.DidReconcile)
                {
                    _reconciliationCount++;
                }

                if (report.NeedsFullSnapshot)
                {
                    _fullSnapshotRequestCount++;
                }

                _totalReplayTicks += report.ReplayTicks;
                if (report.ReplayTicks > _maxReplayTicks)
                {
                    _maxReplayTicks = report.ReplayTicks;
                }

                _totalRemoteJitter += telemetry.RemoteJitter;
                if (telemetry.RemoteJitter > _maxRemoteJitter)
                {
                    _maxRemoteJitter = telemetry.RemoteJitter;
                }

                _acceptedHits += telemetry.AcceptedHits;
                _rejectedHits += telemetry.RejectedHits;
                _networkStats = telemetry.NetworkStats;

                _health.Publish(telemetry.HealthEvents);
            }

            public DemoHarnessMetrics Build()
            {
                var healthReport = _health.CreateReport();
                return new DemoHarnessMetrics(
                    _stepsRun,
                    _totalTicks,
                    _lastFrame,
                    _reconciliationCount,
                    _fullSnapshotRequestCount,
                    _totalReplayTicks,
                    _maxReplayTicks,
                    _totalRemoteJitter,
                    _maxRemoteJitter,
                    _acceptedHits,
                    _rejectedHits,
                    _networkStats,
                    checked((int)healthReport.EventCount),
                    checked((int)healthReport.WarningCount),
                    checked((int)healthReport.ErrorCount),
                    healthReport);
            }
        }
    }
}
