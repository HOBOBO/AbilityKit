using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Server;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class MobaDamageEventSnapshotService_Obsolete : IService
    {
        private readonly MobaLobbyStateService _lobby;

        private FrameIndex _lastFrame;

        private readonly List<MobaDamageEventSnapshotCodec.Entry> _events = new List<MobaDamageEventSnapshotCodec.Entry>(64);
        private readonly List<MobaDamageEventSnapshotCodec.Entry> _drain = new List<MobaDamageEventSnapshotCodec.Entry>(64);

        public MobaDamageEventSnapshotService_Obsolete(MobaLobbyStateService lobby)
        {
            _lobby = lobby ?? throw new ArgumentNullException(nameof(lobby));
            _lastFrame = new FrameIndex(-999999);
        }

        public void ReportDamage(int attackerActorId, int targetActorId, int damageType, float value, int reasonKind, int reasonParam, float targetHp, float targetMaxHp)
        {
            if (targetActorId <= 0) return;
            if (value == 0f) return;
            _events.Add(MobaDamageEventSnapshotCodec.Entry.Damage(attackerActorId, targetActorId, damageType, value, reasonKind, reasonParam, targetHp, targetMaxHp));
        }

        public void ReportHeal(int healerActorId, int targetActorId, int healType, float value, int reasonKind, int reasonParam, float targetHp, float targetMaxHp)
        {
            if (targetActorId <= 0) return;
            if (value == 0f) return;
            _events.Add(MobaDamageEventSnapshotCodec.Entry.Heal(healerActorId, targetActorId, healType, value, reasonKind, reasonParam, targetHp, targetMaxHp));
        }

        public bool TryGetSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot)
        {
            if (!_lobby.Started)
            {
                snapshot = default;
                return false;
            }

            if (frame.Value == _lastFrame.Value)
            {
                snapshot = default;
                return false;
            }
            _lastFrame = frame;

            if (_events.Count == 0)
            {
                snapshot = default;
                return false;
            }

            _drain.Clear();
            _drain.AddRange(_events);
            _events.Clear();

            var payload = MobaDamageEventSnapshotCodec.Serialize(_drain.ToArray());
            snapshot = new WorldStateSnapshot((int)MobaOpCode.DamageEventSnapshot, payload);
            return true;
        }

        public void Dispose()
        {
        }
    }
}
