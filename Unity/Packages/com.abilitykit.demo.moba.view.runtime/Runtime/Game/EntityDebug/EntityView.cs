using System;
using UnityEngine;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.EntityDebug
{
    public sealed class EntityView : MonoBehaviour
    {
        [SerializeField] private int _index;
        [SerializeField] private int _version;

        private EC.IECWorld _world;

        public EC.IEntityId Id => new EC.IEntityId(_index, _version);
        public EC.IECWorld World => _world;

        public bool IsBound => _world != null;

        public void Bind(EC.IECWorld world, EC.IEntityId id)
        {
            _world = world;
            _index = id.Index;
            _version = id.Version;
        }

        public bool TryGetEntity(out EC.IEntity entity)
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
