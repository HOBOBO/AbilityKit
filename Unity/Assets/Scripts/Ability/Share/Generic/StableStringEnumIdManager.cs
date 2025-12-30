using System;

namespace AbilityKit.Ability
{
    public class StableStringEnumIdManager<TId> where TId : GenericEnumId<int>
    {
        private readonly StableStringIdRegistry _registry;
        private readonly Func<int, TId> _factory;

        public StableStringEnumIdManager(Func<int, TId> factory)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _registry = new StableStringIdRegistry();
        }

        public TId Register(string name)
        {
            var id = _registry.GetOrRegister(name);
            return _factory(id);
        }

        public bool TryGetId(string name, out TId id)
        {
            if (_registry.TryGetId(name, out var raw))
            {
                id = _factory(raw);
                return true;
            }

            id = null;
            return false;
        }

        public bool TryGetName(TId id, out string name)
        {
            if (id == null)
            {
                name = null;
                return false;
            }

            return _registry.TryGetName(id.Value, out name);
        }
    }
}
