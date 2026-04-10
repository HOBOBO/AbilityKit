using System;
using System.Collections.Generic;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Ability.Share.ECS.Entitas;
using AbilityKit.ECS;

namespace AbilityKit.Ability.Share.ECS.Entitas
{
    public sealed class EntitasUnitResolver : IUnitResolver
    {
        private readonly EntitasActorIdLookup _lookup;
        private readonly Dictionary<int, EntitasUnitFacade> _cache = new Dictionary<int, EntitasUnitFacade>();

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

            // NOTE: 鐩墠鍏堢敤 facade cache 鐨勬柟寮忔壙杞?Tags/Attributes/Effects銆?
            // 鍚庣画浣犳妸杩欎簺瀹瑰櫒鏀规垚 Entitas Component 鎸傚湪 entity 涓婃椂锛宎dapter 鍙渶瑕佸湪姝ゅ鏀逛负浠庣粍浠惰鍙栧嵆鍙€?
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
