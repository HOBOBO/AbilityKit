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
    /// 让框架 DemoHarness 驱动现有 Shooter 同步策略的轻量适配器。
    /// 它不拥有 Shooter 生命周期；调用方在运行场景前负责启动并配置底层控制器。
    /// </summary>
    public sealed class ShooterDemoHarnessCarrier : ISyncDemoCarrier, ISyncDemoCarrierCapabilities
    {
        public const string DefaultCarrierName = "Shooter";

        private readonly IClientSyncStrategy<ShooterPlayerCommand, ShooterRemoteSnapshotSample> _strategy;
        private readonly Func<NetworkConditioningStats> _networkStats;
        private readonly Func<double> _remoteJitter;
        private readonly Func<long> _acceptedHits;
        private readonly Func<long> _rejectedHits;

        public ShooterDemoHarnessCarrier(
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
            return ShooterDemoHarnessCarrierProfileRules.SupportsPredictRollback(in profile, in networkProfile);
        }

        public DemoHarnessStepTelemetry Step(in DemoHarnessStepContext context)
        {
            LastTimeAnchor = context.TimeAnchor;
            var tick = _strategy.Tick(context.DeltaSeconds);
            var report = _strategy.GetReconciliationReport();

            // §4.4.5：将 Shooter 控制器在本步骤采集到的框架 FastReconnect 健康事件，
            // 转发到共享 DemoHarness 遥测流中。
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
