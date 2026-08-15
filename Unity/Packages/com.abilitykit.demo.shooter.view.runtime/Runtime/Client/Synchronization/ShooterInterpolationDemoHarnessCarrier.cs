#nullable enable

using System;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Network.Runtime;
using AbilityKit.Protocol.Shooter;
using AbilityKit.Network.Runtime.Conditioning;
using AbilityKit.Demo.Harness;
using AbilityKit.Network.Runtime.Sync;

namespace AbilityKit.Demo.Shooter.View
{
    /// <summary>
    /// 让框架 DemoHarness 驱动现有 Shooter 权威插值同步策略的轻量适配器。
    /// 它与 <see cref="ShooterDemoHarnessCarrier"/> 对称，但接收
    /// <see cref="ClientPlaybackPolicy.AuthoritativeInterpolation"/> profile，而不是预测回滚 profile，
    /// 因此验收矩阵可以通过同一条 <see cref="DemoHarnessRunner"/> 管线验证两种同步模型。
    /// </summary>
    public sealed class ShooterInterpolationDemoHarnessCarrier : ISyncDemoCarrier, ISyncDemoCarrierCapabilities
    {
        public const string DefaultCarrierName = "ShooterInterpolation";

        private readonly IClientSyncStrategy<ShooterPlayerCommand, ShooterRemoteSnapshotSample> _strategy;
        private readonly Func<NetworkConditioningStats> _networkStats;
        private readonly Func<double> _remoteJitter;
        private readonly Func<long> _acceptedHits;
        private readonly Func<long> _rejectedHits;

        public ShooterInterpolationDemoHarnessCarrier(
            IClientSyncStrategy<ShooterPlayerCommand, ShooterRemoteSnapshotSample> strategy,
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
            return ShooterDemoHarnessCarrierProfileRules.SupportsAuthoritativeInterpolation(in profile, in networkProfile);
        }

        public DemoHarnessStepTelemetry Step(in DemoHarnessStepContext context)
        {
            LastTimeAnchor = context.TimeAnchor;
            var tick = _strategy.Tick(context.DeltaSeconds);
            var report = _strategy.GetReconciliationReport();

            var healthEvents = CollectFastReconnectHealthEvents();

            return new DemoHarnessStepTelemetry(
                tick,
                report,
                _networkStats(),
                _remoteJitter(),
                _acceptedHits(),
                _rejectedHits(),
                healthEvents);
        }

        private SyncHealthEvent[]? CollectFastReconnectHealthEvents()
        {
            return ShooterDemoHarnessCarrierProfileRules.CollectFastReconnectHealthEvents(_strategy);
        }
    }
}
