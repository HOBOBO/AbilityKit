#nullable enable

using System;
using AbilityKit.Ability.Host;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Conditioning;
using AbilityKit.Network.Runtime.DemoHarness;
using AbilityKit.Network.Runtime.Sync;

namespace AbilityKit.Game.Battle.Agent
{
    /// <summary>
    /// Thin adapter that lets the framework demo harness drive an existing Moba sync strategy.
    /// It does not own Moba lifecycle; callers configure the underlying controller before running scenarios.
    /// </summary>
    public sealed class MobaDemoHarnessCarrier : ISyncDemoCarrier, ISyncDemoCarrierCapabilities
    {
        public const string DefaultCarrierName = "Moba";
        public const string MassBattleLodDegradedReason = "Moba carrier runs MassBattleLodSync as all-entities authoritative interpolation: missing DistanceAoi, TeamOrFactionAoi, PriorityBudget, LodFrequency, and RequestAoiSlice runtime enforcement.";
        public const string AuthoritativeInterpolationRequiredReason = "Moba carrier currently supports authoritative interpolation playback only.";
        public const string StateStreamRequiredReason = "Moba carrier requires a fixed-rate or batch state stream.";

        private const InterestPolicy MassBattleLodRequiredInterest = InterestPolicy.DistanceAoi | InterestPolicy.TeamOrFactionAoi | InterestPolicy.PriorityBudget | InterestPolicy.LodFrequency;

        private readonly IClientSyncStrategy<PlayerInputCommand, MobaRemoteSnapshotSample> _strategy;
        private readonly Func<NetworkConditioningStats> _networkStats;
        private readonly Func<double> _remoteJitter;
        private readonly Func<long> _acceptedHits;
        private readonly Func<long> _rejectedHits;

        public MobaDemoHarnessCarrier(
            IClientSyncStrategy<PlayerInputCommand, MobaRemoteSnapshotSample> strategy,
            Func<NetworkConditioningStats>? networkStats = null,
            Func<double>? remoteJitter = null,
            Func<long>? acceptedHits = null,
            Func<long>? rejectedHits = null,
            string carrierName = DefaultCarrierName)
        {
            if (string.IsNullOrWhiteSpace(carrierName)) throw new ArgumentException("Carrier name is required.", nameof(carrierName));

            _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
            _networkStats = networkStats ?? (() => default);
            _remoteJitter = remoteJitter ?? (() => 0d);
            _acceptedHits = acceptedHits ?? (() => 0L);
            _rejectedHits = rejectedHits ?? (() => 0L);
            CarrierName = carrierName;
        }

        public string CarrierName { get; }

        public NetworkSyncModel SyncModel => _strategy.SyncModel;

        public SyncTimeAnchor LastTimeAnchor { get; private set; }

        public SyncDemoCapabilityResult Supports(in NetworkSyncProfile profile, in NetworkConditionProfile networkProfile)
        {
            if (profile.ClientPlayback != ClientPlaybackPolicy.AuthoritativeInterpolation)
            {
                return SyncDemoCapabilityResult.Unsupported(AuthoritativeInterpolationRequiredReason);
            }

            if (!profile.Snapshot.HasFlag(SnapshotPolicy.FixedRateStateStream) && !profile.Snapshot.HasFlag(SnapshotPolicy.BatchSnapshot))
            {
                return SyncDemoCapabilityResult.Unsupported(StateStreamRequiredReason);
            }

            if (profile.CompatibilityModel == NetworkSyncModel.MassBattleLodSync)
            {
                return IsMassBattleLodProfile(in profile)
                    ? SyncDemoCapabilityResult.Degraded(MassBattleLodDegradedReason)
                    : SyncDemoCapabilityResult.Unsupported("MassBattleLodSync profile must declare DistanceAoi, TeamOrFactionAoi, PriorityBudget, LodFrequency, and RequestAoiSlice policies.");
            }

            return SyncDemoCapabilityResult.Supported;
        }

        private static bool IsMassBattleLodProfile(in NetworkSyncProfile profile)
        {
            return (profile.Interest & MassBattleLodRequiredInterest) == MassBattleLodRequiredInterest &&
                   profile.Recovery.HasFlag(RecoveryPolicy.RequestAoiSlice);
        }

        public DemoHarnessStepTelemetry Step(in DemoHarnessStepContext context)
        {
            LastTimeAnchor = context.TimeAnchor;
            var tick = _strategy.Tick(context.DeltaSeconds);
            var report = _strategy.GetReconciliationReport();

            return new DemoHarnessStepTelemetry(
                tick,
                report,
                _networkStats(),
                _remoteJitter(),
                _acceptedHits(),
                _rejectedHits());
        }
    }
}
