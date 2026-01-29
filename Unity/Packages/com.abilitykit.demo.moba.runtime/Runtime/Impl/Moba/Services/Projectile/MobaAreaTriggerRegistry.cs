#if false
using System.Collections.Generic;
using AbilityKit.Ability.Share.Common.Projectile;
using AbilityKit.Ability.Share.Math;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Share.Impl.Moba.Services.Projectile
{
    public sealed class MobaAreaTriggerRegistry : IService
    {
        private readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();

        public void Register(AreaId areaId, int ownerId, in Vec3 center, float radius, int onEnterTriggerId, int onExitTriggerId, int onExpireTriggerId)
        {
            if (areaId.Value <= 0) return;
            _entries[areaId.Value] = new Entry(ownerId, center, radius, onEnterTriggerId, onExitTriggerId, onExpireTriggerId);
        }

        public void Unregister(AreaId areaId)
        {
            if (areaId.Value <= 0) return;
            _entries.Remove(areaId.Value);
        }

        public bool TryGet(AreaId areaId, out Entry entry)
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
            public readonly int OwnerId;
            public readonly Vec3 Center;
            public readonly float Radius;
            public readonly int OnEnterTriggerId;
            public readonly int OnExitTriggerId;
            public readonly int OnExpireTriggerId;

            public Entry(int ownerId, in Vec3 center, float radius, int onEnterTriggerId, int onExitTriggerId, int onExpireTriggerId)
            {
                OwnerId = ownerId;
                Center = center;
                Radius = radius;
                OnEnterTriggerId = onEnterTriggerId;
                OnExitTriggerId = onExitTriggerId;
                OnExpireTriggerId = onExpireTriggerId;
            }
        }
    }
}
#endif
