using System;
using AbilityKit.Ability.EC;
using UnityEngine;

namespace AbilityKit.Game.EntityDebug
{
    public sealed class EntityView : MonoBehaviour
    {
        [SerializeField] private int _index;
        [SerializeField] private int _version;

        private EntityWorld _world;

        public EntityId Id => new EntityId(_index, _version);
        public EntityWorld World => _world;

        public bool IsBound => _world != null;

        public void Bind(EntityWorld world, EntityId id)
        {
            _world = world;
            _index = id.Index;
            _version = id.Version;
        }

        public bool TryGetEntity(out Entity entity)
        {
            if (_world == null)
            {
                entity = default;
                return false;
            }

            var id = Id;
            if (!_world.IsAlive(id))
            {
                entity = default;
                return false;
            }

            entity = _world.Wrap(id);
            return true;
        }

        public override string ToString()
        {
            return $"EntityView({_index},{_version})";
        }
    }
}
