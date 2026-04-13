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

            // NOTE: 閻╊喖澧犻崗鍫㈡暏 facade cache 閻ㄥ嫭鏌熷蹇斿鏉?Tags/Attributes/Effects閵?
            // 閸氬海鐢绘担鐘冲Ω鏉╂瑤绨虹€圭懓娅掗弨瑙勫灇 Entitas Component 閹稿倸婀?entity 娑撳﹥妞傞敍瀹巇apter 閸欘亪娓剁憰浣告躬濮濄倕顦╅弨閫涜礋娴犲海绮嶆禒鎯邦嚢閸欐牕宓嗛崣顖樷偓?
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
