using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Diagnostics;

namespace AbilityKit.Game.Editor
{
    internal sealed class BattleDebugDiagnosticAttributesViewModel
    {
        private long _lastRequestId;
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
                return $"属性数据不可用：{attributeStatus.Availability} {attributeStatus.Message}";
            }

            if (!modifierStatus.CanDisplayResults &&
                modifierStatus.Phase != BattleDiagnosticQueryPhase.Empty)
            {
                return $"属性修改器不可用：{modifierStatus.Availability} {modifierStatus.Message}";
            }

            return string.Empty;
        }
    }
}
