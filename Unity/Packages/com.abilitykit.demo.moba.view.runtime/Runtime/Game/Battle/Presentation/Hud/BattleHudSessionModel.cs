using System;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleHudSessionModel
    {
        private string _localPlayerId = string.Empty;
        private int _localActorId;
        private int _loadoutRevision;
        private string _boundPlayerId;
        private int _boundLoadoutRevision = -1;

        public string LocalPlayerId => _localPlayerId;
        public int LocalActorId => _localActorId;
        public int LoadoutRevision => _loadoutRevision;

        public bool RequiresLoadoutBinding =>
            !string.IsNullOrEmpty(_localPlayerId) &&
            (!string.Equals(
                 _localPlayerId,
                 _boundPlayerId,
                 StringComparison.OrdinalIgnoreCase) ||
             _loadoutRevision != _boundLoadoutRevision);

        public void Synchronize(string localPlayerId, int localActorId, int loadoutRevision)
        {
            _localPlayerId = localPlayerId ?? string.Empty;
            _localActorId = localActorId > 0 ? localActorId : 0;
            _loadoutRevision = loadoutRevision;
        }

        public void ApplyEnterGameSnapshot(string playerId, int localActorId)
        {
            if (string.IsNullOrEmpty(_localPlayerId) && !string.IsNullOrEmpty(playerId))
            {
                _localPlayerId = playerId;
            }

            if (_localActorId <= 0 && localActorId > 0)
            {
                _localActorId = localActorId;
            }
        }

        public bool ShouldUseEnterGameLoadout(string responsePlayerId, bool hasExplicitLocalControl)
        {
            return !hasExplicitLocalControl ||
                   string.Equals(
                       _localPlayerId,
                       responsePlayerId,
                       StringComparison.OrdinalIgnoreCase);
        }

        public void MarkLoadoutBound()
        {
            if (string.IsNullOrEmpty(_localPlayerId)) return;

            _boundPlayerId = _localPlayerId;
            _boundLoadoutRevision = _loadoutRevision;
        }

        public int ResolveLocalActorId(
            MobaSkillStateSnapshotEntry[] entries,
            Predicate<MobaSkillStateSnapshotEntry> matchesLoadout)
        {
            if (_localActorId > 0) return _localActorId;
            if (entries == null || entries.Length == 0) return 0;

            var matchedActorId = ResolveUniqueActorId(entries, matchesLoadout, out var hasMatch);
            if (hasMatch)
            {
                if (matchedActorId > 0) _localActorId = matchedActorId;
                return matchedActorId;
            }

            var singleActorId = ResolveUniqueActorId(entries, null, out _);
            if (singleActorId > 0) _localActorId = singleActorId;
            return singleActorId;
        }

        public void Reset()
        {
            _localPlayerId = string.Empty;
            _localActorId = 0;
            _loadoutRevision = 0;
            _boundPlayerId = null;
            _boundLoadoutRevision = -1;
        }

        private static int ResolveUniqueActorId(
            MobaSkillStateSnapshotEntry[] entries,
            Predicate<MobaSkillStateSnapshotEntry> filter,
            out bool hasMatch)
        {
            hasMatch = false;
            var actorId = 0;
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry.ActorId <= 0) continue;
                if (filter != null && !filter(entry)) continue;

                hasMatch = true;
                if (actorId <= 0)
                {
                    actorId = entry.ActorId;
                    continue;
                }

                if (actorId != entry.ActorId)
                {
                    return 0;
                }
            }

            return actorId;
        }
    }

    // Retained for existing Unity callers while binding state is now owned by the session model.
    internal sealed class BattleHudSkillTemplateBindingState
    {
        private readonly BattleHudSessionModel _session = new BattleHudSessionModel();

        public bool RequiresBinding(string playerId, int loadoutRevision)
        {
            _session.Synchronize(playerId, _session.LocalActorId, loadoutRevision);
            return _session.RequiresLoadoutBinding;
        }

        public void MarkBound(string playerId, int loadoutRevision)
        {
            _session.Synchronize(playerId, _session.LocalActorId, loadoutRevision);
            _session.MarkLoadoutBound();
        }

        public void Reset()
        {
            _session.Reset();
        }
    }
}
