using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Diagnostics;

namespace AbilityKit.Game.Editor
{
    internal sealed class BattleDebugDiagnosticAttributesViewModel
    {
        internal readonly struct AttributeSample
        {
            public AttributeSample(int frame, float baseValue, float finalValue, int modifierCount)
            {
                Frame = frame;
                BaseValue = baseValue;
                FinalValue = finalValue;
                ModifierCount = modifierCount;
            }

            public int Frame { get; }
            public float BaseValue { get; }
            public float FinalValue { get; }
            public int ModifierCount { get; }
        }

        private const int MaxSamples = 240;
        private readonly List<AttributeSample> _history = new List<AttributeSample>(MaxSamples);
        private IBattleDiagnosticReadOnlySession _historySession;
        private BattleDiagnosticSessionScope _historyScope;
        private long _historyActorId;
        private int _historyAttributeId;
        private long _historyRevision = -1;
        private long _lastRequestId;
        private IBattleDiagnosticReadOnlySession _lastSession;
        private BattleDiagnosticSessionScope _lastScope;
        private long _lastStoreRevision = -1;
        private long _lastActorId;
        private int _lastFrame;
        private bool _hasCachedResult;
        private IReadOnlyList<BattleDiagnosticActorAttribute> _attributes =
            Array.Empty<BattleDiagnosticActorAttribute>();
        private IReadOnlyList<BattleDiagnosticActorAttributeModifier> _modifiers =
            Array.Empty<BattleDiagnosticActorAttributeModifier>();

        public IReadOnlyList<BattleDiagnosticActorAttribute> Attributes => _attributes;
        public IReadOnlyList<BattleDiagnosticActorAttributeModifier> Modifiers => _modifiers;
        public BattleDiagnosticQueryStatus AttributeQueryStatus { get; private set; }
        public BattleDiagnosticQueryStatus ModifierQueryStatus { get; private set; }
        public string StatusMessage { get; private set; } = string.Empty;
        public long StoreRevision => _lastStoreRevision;
        public IReadOnlyList<AttributeSample> History => _history;

        public void ClearHistory(bool waitForNextRevision = false)
        {
            _history.Clear();
            _historyRevision = waitForNextRevision ? _lastStoreRevision : -1;
        }

        public void TrackAttribute(
            IBattleDiagnosticReadOnlySession session,
            long actorId,
            int attributeId,
            bool recording)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            var scope = session.SessionInfo.Scope;
            if (!ReferenceEquals(_historySession, session) || _historyScope != scope ||
                _historyActorId != actorId || _historyAttributeId != attributeId)
            {
                ClearHistory();
                _historySession = session;
                _historyScope = scope;
                _historyActorId = actorId;
                _historyAttributeId = attributeId;
            }

            if (!recording || attributeId <= 0 ||
                !AttributeQueryStatus.CanDisplayResults ||
                _lastActorId != actorId || _lastScope != scope ||
                _lastStoreRevision != session.ActorAttributeStoreRevision)
                return;

            for (var i = 0; i < _attributes.Count; i++)
            {
                var attribute = _attributes[i];
                if (attribute.AttributeId != attributeId || attribute.ActorId != actorId ||
                    float.IsNaN(attribute.BaseValue) || float.IsInfinity(attribute.BaseValue) ||
                    float.IsNaN(attribute.FinalValue) || float.IsInfinity(attribute.FinalValue))
                    continue;

                var revision = _lastStoreRevision;
                if (revision < _historyRevision ||
                    (_history.Count > 0 && attribute.Frame < _history[_history.Count - 1].Frame))
                    ClearHistory();
                if (revision == _historyRevision) return;

                var sample = new AttributeSample(
                    attribute.Frame, attribute.BaseValue, attribute.FinalValue, attribute.ModifierCount);
                if (_history.Count > 0 && _history[_history.Count - 1].Frame == sample.Frame)
                    _history[_history.Count - 1] = sample;
                else
                {
                    if (_history.Count == MaxSamples) _history.RemoveAt(0);
                    _history.Add(sample);
                }
                _historyRevision = revision;
                return;
            }
        }

        public void InvalidateCache()
        {
            _attributes = Array.Empty<BattleDiagnosticActorAttribute>();
            _modifiers = Array.Empty<BattleDiagnosticActorAttributeModifier>();
            AttributeQueryStatus = default;
            ModifierQueryStatus = default;
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
            var revision = session.ActorAttributeStoreRevision;
            var queryFrame = frame < 0 ? 0 : frame;
            if (_hasCachedResult &&
                ReferenceEquals(_lastSession, session) &&
                _lastScope == scope &&
                _lastStoreRevision == revision &&
                _lastActorId == actorId &&
                _lastFrame == queryFrame)
            {
                return;
            }

            _lastRequestId++;
            if (_lastRequestId <= 0) _lastRequestId = 1;

            var attributeResult = session.QueryActorAttributes(_lastRequestId, queryFrame, actorId);
            var modifierResult = session.QueryActorAttributeModifiers(_lastRequestId, queryFrame, actorId);

            _lastSession = session;
            _lastScope = scope;
            _lastStoreRevision = revision;
            _lastActorId = actorId;
            _lastFrame = queryFrame;
            _hasCachedResult = true;
            AttributeQueryStatus = attributeResult.Status;
            ModifierQueryStatus = modifierResult.Status;
            _attributes = attributeResult.Items ?? Array.Empty<BattleDiagnosticActorAttribute>();
            _modifiers = modifierResult.Items ?? Array.Empty<BattleDiagnosticActorAttributeModifier>();
            StatusMessage = BuildStatusMessage(attributeResult.Status, modifierResult.Status);
        }

        private static string BuildStatusMessage(
            BattleDiagnosticQueryStatus attributeStatus,
            BattleDiagnosticQueryStatus modifierStatus)
        {
            if (!attributeStatus.CanDisplayResults &&
                attributeStatus.Phase != BattleDiagnosticQueryPhase.Empty)
            {
                return $"属性数据不可用：{BattleDebugDisplayText.Availability(attributeStatus.Availability)} {attributeStatus.Message}";
            }

            if (!modifierStatus.CanDisplayResults &&
                modifierStatus.Phase != BattleDiagnosticQueryPhase.Empty)
            {
                return $"属性修改器不可用：{BattleDebugDisplayText.Availability(modifierStatus.Availability)} {modifierStatus.Message}";
            }

            return string.Empty;
        }
    }
}
