using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Combat.Projectile;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Share.Config;

namespace AbilityKit.Demo.Moba.Services.Projectile
{
    /// <summary>Authored interception responses with immutable launch-time rules.</summary>
    public sealed class MobaProjectileCollisionFilter : IProjectileHitFilter,
        IProjectileCollisionResponseResolver, IProjectileHitFilterSnapshotSource
    {
        private const int SnapshotMagic = 0x4d504346;
        private const int SnapshotVersion = 1;
        private readonly MobaActorRegistry _registry;
        private readonly int _hitUnitSubType;
        private readonly int _trackingTargetActorId;
        private readonly ProjectileCollisionRule[] _rules;

        public MobaProjectileCollisionFilter(MobaActorRegistry registry, int hitUnitSubType = 0,
            int trackingTargetActorId = 0, IReadOnlyList<ProjectileCollisionRule> rules = null)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            if (hitUnitSubType < 0 || trackingTargetActorId < 0)
                throw new ArgumentOutOfRangeException(nameof(hitUnitSubType));
            _hitUnitSubType = hitUnitSubType;
            _trackingTargetActorId = trackingTargetActorId;
            if ((rules?.Count ?? 0) > 256) throw new ArgumentOutOfRangeException(nameof(rules));
            _rules = new ProjectileCollisionRule[rules?.Count ?? 0];
            for (var i = 0; i < _rules.Length; i++)
            {
                if (!rules[i].TryValidate(out var error)) throw new ArgumentException(error, nameof(rules));
                _rules[i] = rules[i];
            }
        }

        public bool ShouldHit(int ownerId, ColliderId collider, int frame) =>
            ResolveCollision(ownerId, collider, frame) is ProjectileCollisionResponse.Hit or ProjectileCollisionResponse.SupportContact;

        public ProjectileCollisionResponse ResolveCollision(int ownerId, ColliderId collider, int frame)
        {
            if (collider.Value == 0 || !_registry.TryGet(ownerId, out var owner) || owner == null)
                return ProjectileCollisionResponse.Ignore;
            global::ActorEntity target = null;
            foreach (var pair in _registry.Entries)
                if (pair.Value != null && pair.Value.hasCollisionId && pair.Value.collisionId.Value.Equals(collider))
                {
                    target = pair.Value;
                    break;
                }

            if (target == null)
            {
                for (var i = 0; i < _rules.Length; i++)
                    if (_rules[i].Role == ProjectileCollisionRole.StaticWorld) return _rules[i].Response;
                return ProjectileCollisionResponse.Block;
            }
            if (target.hasActorId && target.actorId.Value == ownerId) return ProjectileCollisionResponse.Ignore;
            var ownerTeam = owner.hasTeam ? owner.team.Value : Team.None;
            var targetTeam = target.hasTeam ? target.team.Value : Team.None;
            var relation = ownerTeam == Team.None || targetTeam == Team.None
                ? ProjectileCollisionRelation.Neutral
                : ownerTeam == targetTeam ? ProjectileCollisionRelation.Ally : ProjectileCollisionRelation.Enemy;
            var role = _trackingTargetActorId > 0 && target.hasActorId && target.actorId.Value == _trackingTargetActorId
                ? ProjectileCollisionRole.TrackingTarget : ProjectileCollisionRole.OtherActor;
            var response = ProjectileCollisionResponse.Hit;
            for (var i = 0; i < _rules.Length; i++)
            {
                var rule = _rules[i];
                if (rule.Role != role ||
                    rule.MainType != 0 && (!target.hasEntityMainType || target.entityMainType.Value != rule.MainType) ||
                    rule.UnitSubType != 0 && (!target.hasEntityMainType || target.entityMainType.Value != EntityMainType.Unit ||
                                            !target.hasUnitSubType || target.unitSubType.Value != rule.UnitSubType) ||
                    rule.Relation != 0 && rule.Relation != relation) continue;
                response = rule.Response;
                break;
            }
            // Block can intercept without damage. Hit never bypasses existing damage eligibility.
            if (response == ProjectileCollisionResponse.SupportContact)
                return relation == ProjectileCollisionRelation.Ally && (!target.hasAttributeGroup || target.GetMobaAttrs().Hp > 0f)
                    ? response : ProjectileCollisionResponse.Ignore;
            if (response != ProjectileCollisionResponse.Hit) return response;
            if (relation == ProjectileCollisionRelation.Ally) return ProjectileCollisionResponse.Ignore;
            if (_hitUnitSubType > 0 && (!target.hasEntityMainType || target.entityMainType.Value != EntityMainType.Unit ||
                !target.hasUnitSubType || (int)target.unitSubType.Value != _hitUnitSubType)) return ProjectileCollisionResponse.Ignore;
            return ProjectileCollisionResponse.Hit;
        }

        public byte[] CaptureSnapshot()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(SnapshotMagic);
                writer.Write(SnapshotVersion);
                writer.Write(_hitUnitSubType);
                writer.Write(_trackingTargetActorId);
                writer.Write(_rules.Length);
                foreach (var rule in _rules)
                {
                    writer.Write((int)rule.Role);
                    writer.Write((int)rule.MainType);
                    writer.Write((int)rule.UnitSubType);
                    writer.Write((int)rule.Relation);
                    writer.Write((int)rule.Response);
                }
                return stream.ToArray();
            }
        }

        internal static MobaProjectileCollisionFilter Restore(MobaActorRegistry registry, byte[] snapshot)
        {
            using (var stream = new MemoryStream(snapshot, false))
            using (var reader = new BinaryReader(stream))
            {
                if (reader.ReadInt32() != SnapshotMagic || reader.ReadInt32() != SnapshotVersion)
                    throw new InvalidOperationException("Unsupported projectile collision filter snapshot.");
                var subtype = reader.ReadInt32();
                var target = reader.ReadInt32();
                var count = reader.ReadInt32();
                if (count < 0 || count > 256 || stream.Length - stream.Position != count * 20L)
                    throw new InvalidOperationException("Invalid projectile collision rule snapshot size.");
                var rules = new ProjectileCollisionRule[count];
                for (var i = 0; i < count; i++)
                    rules[i] = new ProjectileCollisionRule(new ProjectileCollisionRuleDTO {
                        Role = (ProjectileCollisionRole)reader.ReadInt32(), MainType = reader.ReadInt32(),
                        UnitSubType = reader.ReadInt32(), Relation = (ProjectileCollisionRelation)reader.ReadInt32(),
                        Response = reader.ReadInt32(),
                    });
                return new MobaProjectileCollisionFilter(registry, subtype, target, rules);
            }
        }
    }

    [WorldService(typeof(IProjectileHitFilterResolver))]
    public sealed class MobaProjectileCollisionFilterResolver : IProjectileHitFilterSnapshotResolver, IService
    {
        private readonly MobaActorRegistry _registry;
        public MobaProjectileCollisionFilterResolver(MobaActorRegistry registry) { _registry = registry; }
        public IProjectileHitFilter Resolve(int key) => null;
        public IProjectileHitFilter ResolveSnapshot(byte[] snapshot) => MobaProjectileCollisionFilter.Restore(_registry, snapshot);
        public void Dispose() { }
    }
}
