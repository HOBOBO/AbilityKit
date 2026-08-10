using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Diagnostics;

namespace AbilityKit.Game.Editor
{
    internal sealed class BattleDebugDiagnosticEffectsViewModel
    {
        private long _lastRequestId;
        private BattleDiagnosticSessionScope _lastScope;
        private long _lastStoreRevision = -1;
        private long _lastActorId;
        private int _lastFrame;
        private bool _hasCachedResult;
        private IReadOnlyList<BattleDiagnosticActorEffect> _effects =
            Array.Empty<BattleDiagnosticActorEffect>();

        public IReadOnlyList<BattleDiagnosticActorEffect> Effects => _effects;
        public BattleDiagnosticQueryStatus QueryStatus { get; private set; }
        public string StatusMessage { get; private set; } = string.Empty;
        public long StoreRevision => _lastStoreRevision;

        public void InvalidateCache()
        {
            _effects = Array.Empty<BattleDiagnosticActorEffect>();
            QueryStatus = default;
            StatusMessage = string.Empty;
            _lastStoreRevision = -1;
            _hasCachedResult = false;
        }

        public void RefreshIfNeeded(
            IBattleDiagnosticReadOnlySession session,
            long actorId,
            int frame = 0)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (actorId == 0) throw new ArgumentOutOfRangeException(nameof(actorId));

            var scope = session.SessionInfo.Scope;
            var revision = session.ActorEffectStoreRevision;
            var queryFrame = frame < 0 ? 0 : frame;
            if (_hasCachedResult &&
                _lastScope == scope &&
                _lastStoreRevision == revision &&
                _lastActorId == actorId &&
                _lastFrame == queryFrame)
            {
                return;
            }

            _lastRequestId++;
            if (_lastRequestId <= 0) _lastRequestId = 1;

            var result = session.QueryActorEffects(_lastRequestId, queryFrame, actorId);
            _lastScope = scope;
            _lastStoreRevision = revision;
            _lastActorId = actorId;
            _lastFrame = queryFrame;
            _hasCachedResult = true;
            QueryStatus = result.Status;
            _effects = result.Items ?? Array.Empty<BattleDiagnosticActorEffect>();
            StatusMessage = BuildStatusMessage(result.Status);
        }

        private static string BuildStatusMessage(BattleDiagnosticQueryStatus status)
        {
            if (!status.CanDisplayResults && status.Phase != BattleDiagnosticQueryPhase.Empty)
            {
                return $"Effect 数据不可用：{status.Availability} {status.Message}";
            }

            return string.Empty;
        }
    }
}
