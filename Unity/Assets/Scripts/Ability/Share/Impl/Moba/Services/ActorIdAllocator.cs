using System;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class ActorIdAllocator
    {
        private int _nextId = 1;

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
    }
}
