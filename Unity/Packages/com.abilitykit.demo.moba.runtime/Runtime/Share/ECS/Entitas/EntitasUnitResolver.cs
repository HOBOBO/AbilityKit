using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Share.ECS.Entitas
{
    public sealed class EntitasUnitResolver : IUnitResolver
    {
        private readonly EntitasActorIdLookup _lookup;
        private readonly Dictionary<int, IUnitFacade> _cache = new Dictionary<int, IUnitFacade>();

        public EntitasUnitResolver(EntitasActorIdLookup lookup)
        {
            _lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
        }

        public bool TryResolve(EcsEntityId id, out IUnitFacade unit)
        {
            if (!id.IsValid)
            {
                unit = null;
                return false;
            }

            if (!_lookup.TryGet(id.ActorId, out _))
            {
                unit = null;
                return false;
            }

            if (_cache.TryGetValue(id.ActorId, out var cached) && cached != null)
            {
                unit = cached;
                return true;
            }

            // NOTE: 目前先用 facade cache 的方式承载 Tags/Attributes/Effects。
            // 后续你把这些容器改成 Entitas Component 挂在 entity 上时，adapter 只需要在此处改为从组件读取即可。
            var created = new EntitasUnitFacade(id.ActorId);
            _cache[id.ActorId] = created;
            unit = created;
            return true;
        }

        public void Clear()
        {
            _cache.Clear();
        }
    }
}
