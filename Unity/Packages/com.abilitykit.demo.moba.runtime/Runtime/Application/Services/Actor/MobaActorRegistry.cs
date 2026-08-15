using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;

namespace AbilityKit.Demo.Moba.Services
{
    [WorldService(typeof(MobaActorRegistry))]
    public sealed class MobaActorRegistry : IService
    {
        private readonly Dictionary<int, global::ActorEntity> _byId = new Dictionary<int, global::ActorEntity>();

        public IEnumerable<KeyValuePair<int, global::ActorEntity>> Entries => _byId;

        private readonly List<int> _orderedIdScratch = new List<int>(16);

        /// <summary>
        /// 按 ActorId 升序填充 id 列表（复用内部 scratch，消费方须在下一调用前用完）。
        /// 快照/投影/跨网络输出必须按此序遍历：字典遍历序跨运行时不保证一致，
        /// 按 id 定序保证两端字节序一致。
        /// </summary>
        public List<int> CopyActorIdsInOrder()
        {
            _orderedIdScratch.Clear();
            _orderedIdScratch.AddRange(_byId.Keys);
            _orderedIdScratch.Sort();
            return _orderedIdScratch;
        }

        public void Register(int actorId, global::ActorEntity entity)
        {
            if (actorId <= 0) throw new ArgumentOutOfRangeException(nameof(actorId));
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            _byId[actorId] = entity;
        }

        public bool Contains(int actorId)
        {
            return actorId > 0 && _byId.ContainsKey(actorId);
        }

        public bool TryGet(int actorId, out global::ActorEntity entity)
        {
            if (TryGetRegistered(actorId, out entity) && entity.isEnabled)
            {
                return true;
            }

            entity = null;
            return false;
        }

        internal bool TryGetRegistered(int actorId, out global::ActorEntity entity)
        {
            entity = null;
            return actorId > 0 &&
                   _byId.TryGetValue(actorId, out entity) &&
                   entity != null;
        }

        public void Unregister(int actorId)
        {
            if (actorId <= 0) return;
            _byId.Remove(actorId);
        }

        public void Clear()
        {
            _byId.Clear();
        }

        public void Dispose()
        {
            _byId.Clear();
        }
    }
}
