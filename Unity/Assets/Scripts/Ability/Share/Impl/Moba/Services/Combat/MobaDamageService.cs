using System;
using AbilityKit.Ability.Impl.Moba.Attributes;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class MobaDamageService : IService
    {
        private readonly MobaActorLookupService _actors;
        private readonly MobaDamageEventSnapshotService _snapshots;

        public MobaDamageService(MobaActorLookupService actors, MobaDamageEventSnapshotService snapshots)
        {
            _actors = actors ?? throw new ArgumentNullException(nameof(actors));
            _snapshots = snapshots ?? throw new ArgumentNullException(nameof(snapshots));
        }

        public float ApplyDamage(int attackerActorId, int targetActorId, int damageType, float value, int reasonKind = 0, int reasonParam = 0)
        {
            if (targetActorId <= 0) return 0f;
            if (value <= 0f) return 0f;

            if (!_actors.TryGetActorEntity(targetActorId, out var target) || target == null) return 0f;

            var attrs = target.GetMobaAttrs();
            var oldHp = attrs.Hp;
            var maxHp = attrs.MaxHp;

            var newHp = Clamp(oldHp - value, 0f, maxHp);
            var actual = oldHp - newHp;
            if (actual <= 0f) return 0f;

            attrs.Hp = newHp;
            _snapshots.ReportDamage(attackerActorId, targetActorId, damageType, actual, reasonKind, reasonParam, newHp, maxHp);
            return actual;
        }

        public float ApplyHeal(int healerActorId, int targetActorId, int healType, float value, int reasonKind = 0, int reasonParam = 0)
        {
            if (targetActorId <= 0) return 0f;
            if (value <= 0f) return 0f;

            if (!_actors.TryGetActorEntity(targetActorId, out var target) || target == null) return 0f;

            var attrs = target.GetMobaAttrs();
            var oldHp = attrs.Hp;
            var maxHp = attrs.MaxHp;

            var newHp = Clamp(oldHp + value, 0f, maxHp);
            var actual = newHp - oldHp;
            if (actual <= 0f) return 0f;

            attrs.Hp = newHp;
            _snapshots.ReportHeal(healerActorId, targetActorId, healType, actual, reasonKind, reasonParam, newHp, maxHp);
            return actual;
        }

        private static float Clamp(float v, float min, float max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        public void Dispose()
        {
        }
    }
}
