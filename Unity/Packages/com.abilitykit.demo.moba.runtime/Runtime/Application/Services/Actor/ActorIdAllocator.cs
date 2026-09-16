using System;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;

namespace AbilityKit.Demo.Moba.Services
{
    [WorldService(typeof(ActorIdAllocator))]
    public sealed class ActorIdAllocator : IService
    {
        private int _nextId = 1;

        public int NextId => _nextId;

        public int Next()
        {
            var id = _nextId;
            if (_nextId == int.MaxValue) throw new InvalidOperationException("ActorId overflow");
            _nextId++;
            return id;
        }

        public void Reset(int nextId = 1)
        {
            _nextId = nextId < 1 ? 1 : nextId;
        }

        public void Dispose()
        {
        }
    }
}
