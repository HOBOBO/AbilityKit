using System;
using System.Diagnostics;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Diagnostics;

namespace AbilityKit.Demo.Moba.Services
{
    public interface IMobaBattleDiagnosticSnapshotCapture
    {
        BattleDiagnosticSessionSnapshot CaptureSnapshot();
    }

    [WorldService(typeof(IMobaBattleDiagnosticSnapshotCapture), WorldLifetime.Scoped)]
    public sealed class MobaBattleDiagnosticSnapshotCapture :
        IMobaBattleDiagnosticSnapshotCapture,
        IService
    {
        private readonly IBattleDiagnosticReadOnlySession _session;
        private readonly IBattleDiagnosticEventSnapshotSource _events;
        private readonly IBattleDiagnosticStateSnapshotSource _state;
        private readonly IBattleDiagnosticTraceSnapshotSource _trace;
        private readonly IBattleDiagnosticAttributeSnapshotSource _attributes;
        private readonly IBattleDiagnosticBuffSnapshotSource _buffs;
        private readonly IBattleDiagnosticTagSnapshotSource _tags;
        private readonly IBattleDiagnosticEffectSnapshotSource _effects;
        private readonly Func<long> _timestampProvider;

        public MobaBattleDiagnosticSnapshotCapture(
            IBattleDiagnosticReadOnlySession session,
            IBattleDiagnosticEventSnapshotSource events,
            IBattleDiagnosticStateSnapshotSource state,
            IBattleDiagnosticTraceSnapshotSource trace,
            IBattleDiagnosticAttributeSnapshotSource attributes,
            IBattleDiagnosticBuffSnapshotSource buffs,
            IBattleDiagnosticTagSnapshotSource tags,
            IBattleDiagnosticEffectSnapshotSource effects)
            : this(
                session,
                events,
                state,
                trace,
                attributes,
                buffs,
                tags,
                effects,
                Stopwatch.GetTimestamp)
        {
        }

        internal MobaBattleDiagnosticSnapshotCapture(
            IBattleDiagnosticReadOnlySession session,
            IBattleDiagnosticEventSnapshotSource events,
            IBattleDiagnosticStateSnapshotSource state,
            IBattleDiagnosticTraceSnapshotSource trace,
            IBattleDiagnosticAttributeSnapshotSource attributes,
            IBattleDiagnosticBuffSnapshotSource buffs,
            IBattleDiagnosticTagSnapshotSource tags,
            IBattleDiagnosticEffectSnapshotSource effects,
            Func<long> timestampProvider)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _trace = trace ?? throw new ArgumentNullException(nameof(trace));
            _attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
            _buffs = buffs ?? throw new ArgumentNullException(nameof(buffs));
            _tags = tags ?? throw new ArgumentNullException(nameof(tags));
            _effects = effects ?? throw new ArgumentNullException(nameof(effects));
            _timestampProvider = timestampProvider ?? throw new ArgumentNullException(nameof(timestampProvider));

            var scope = _session.SessionInfo.Scope;
            EnsureScope(scope, _events.Scope, nameof(events));
            EnsureScope(scope, _state.Scope, nameof(state));
            EnsureScope(scope, _trace.Scope, nameof(trace));
            EnsureScope(scope, _attributes.Scope, nameof(attributes));
            EnsureScope(scope, _buffs.Scope, nameof(buffs));
            EnsureScope(scope, _tags.Scope, nameof(tags));
            EnsureScope(scope, _effects.Scope, nameof(effects));
        }

        public BattleDiagnosticSessionSnapshot CaptureSnapshot()
        {
            var capturedAtTimestamp = _timestampProvider();
            var events = _events.CaptureEventSnapshot();
            var state = _state.CaptureStateSnapshot();
            var trace = _trace.CaptureTraceSnapshot();
            var attributes = _attributes.CaptureAttributeSnapshot();
            var buffs = _buffs.CaptureBuffSnapshot();
            var tags = _tags.CaptureTagSnapshot();
            var effects = _effects.CaptureEffectSnapshot();
            var sessionInfo = _session.SessionInfo;

            return new BattleDiagnosticSessionSnapshot(
                in sessionInfo,
                capturedAtTimestamp,
                events,
                state,
                trace,
                attributes,
                buffs,
                tags,
                effects);
        }

        public void Dispose()
        {
        }

        private static void EnsureScope(
            BattleDiagnosticSessionScope expected,
            BattleDiagnosticSessionScope actual,
            string parameterName)
        {
            if (expected != actual)
            {
                throw new ArgumentException(
                    "All diagnostic snapshot sources must use the session scope.",
                    parameterName);
            }
        }
    }
}
