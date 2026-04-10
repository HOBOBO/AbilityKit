using System.Collections.Generic;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Share.Impl.Moba.Services.Projectile
{
    public sealed class MobaAreaTriggerRegistry : IService
    {
        private readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();

        public void Register(
            AbilityKit.Core.Common.Projectile.AreaId areaId,
            int templateId,
            int ownerId,
            in AbilityKit.Core.Math.Vec3 center,
            float radius,
            int collisionLayerMask,
            int maxTargets,
            int onEnterTriggerId,
            int onExitTriggerId,
            int[] onExpireTriggerIds)
        {
            if (areaId.Value <= 0) return;
            _entries[areaId.Value] = new Entry(templateId, ownerId, center, radius, collisionLayerMask, maxTargets, onEnterTriggerId, onExitTriggerId, onExpireTriggerIds);
        }

        public void Register(
            AbilityKit.Core.Common.Projectile.AreaId areaId,
            int templateId,
            int ownerId,
            in AbilityKit.Core.Math.Vec3 center,
            float radius,
            int collisionLayerMask,
            int maxTargets,
            int onEnterTriggerId,
            int onExitTriggerId,
            int onExpireTriggerId)
        {
            Register(areaId, templateId, ownerId, in center, radius, collisionLayerMask, maxTargets, onEnterTriggerId, onExitTriggerId, onExpireTriggerId > 0 ? new[] { onExpireTriggerId } : null);
        }

        public void Unregister(AbilityKit.Core.Common.Projectile.AreaId areaId)
        {
            if (areaId.Value <= 0) return;
            _entries.Remove(areaId.Value);
        }

        public bool TryGet(AbilityKit.Core.Common.Projectile.AreaId areaId, out Entry entry)
        {
            if (areaId.Value <= 0)
            {
                entry = default;
                return false;
            }

            return _entries.TryGetValue(areaId.Value, out entry);
        }

        public void Dispose()
        {
            _entries.Clear();
        }

        public readonly struct Entry
        {
            public readonly int TemplateId;
            public readonly int OwnerId;
            public readonly AbilityKit.Core.Math.Vec3 Center;
            public readonly float Radius;
            public readonly int CollisionLayerMask;
            public readonly int MaxTargets;
            public readonly int OnEnterTriggerId;
            public readonly int OnExitTriggerId;
            public readonly int[] OnExpireTriggerIds;

            public Entry(int templateId, int ownerId, in AbilityKit.Core.Math.Vec3 center, float radius, int collisionLayerMask, int maxTargets, int onEnterTriggerId, int onExitTriggerId, int[] onExpireTriggerIds)
            {
                TemplateId = templateId;
                OwnerId = ownerId;
                Center = center;
                Radius = radius;
                CollisionLayerMask = collisionLayerMask;
                MaxTargets = maxTargets;
                OnEnterTriggerId = onEnterTriggerId;
                OnExitTriggerId = onExitTriggerId;
                OnExpireTriggerIds = onExpireTriggerIds;
            }
        }
    }
}
