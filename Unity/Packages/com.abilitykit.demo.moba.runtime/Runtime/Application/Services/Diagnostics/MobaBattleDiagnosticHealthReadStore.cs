using System;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Diagnostics;

namespace AbilityKit.Demo.Moba.Services
{
    [WorldService(typeof(IBattleDiagnosticHealthReadStore), WorldLifetime.Scoped)]
    public sealed class MobaBattleDiagnosticHealthReadStore :
        IBattleDiagnosticHealthReadStore,
        IService
    {
        private readonly IBattleDiagnosticReadOnlySession _session;
        private readonly MobaBattleDiagnosticEventCollector _eventCollector;
        private readonly MobaBattleDiagnosticStateSampler _stateSampler;

        public MobaBattleDiagnosticHealthReadStore(
            IBattleDiagnosticReadOnlySession session,
            MobaBattleDiagnosticEventCollector eventCollector,
            MobaBattleDiagnosticStateSampler stateSampler)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _eventCollector = eventCollector ?? throw new ArgumentNullException(nameof(eventCollector));
            _stateSampler = stateSampler ?? throw new ArgumentNullException(nameof(stateSampler));
        }

        public BattleDiagnosticHealthSnapshot CaptureHealthSnapshot()
        {
            return new BattleDiagnosticHealthSnapshot(
                _session.SessionInfo,
                _session.EventStoreRevision,
                _session.StateStoreRevision,
                _session.TraceStoreRevision,
                _stateSampler.LastSuccessfulSampleFrame,
                _eventCollector.LastSequence,
                _eventCollector.EnabledChannels,
                _eventCollector.IsFrozen,
                _eventCollector.Store.Metrics,
                _stateSampler.SampleFailureCount,
                _eventCollector.CollectFailureCount,
                _stateSampler.LastSampleError,
                _eventCollector.LastCollectError);
        }

        public void Dispose()
        {
        }
    }
}
