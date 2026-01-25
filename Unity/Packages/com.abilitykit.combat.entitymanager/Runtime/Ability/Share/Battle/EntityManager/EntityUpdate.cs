using System;

namespace AbilityKit.Ability.Battle.EntityManager
{
    public readonly struct EntityUpdate
    {
        public readonly int Type;
        public readonly object Payload;

        public EntityUpdate(int type, object payload)
        {
            Type = type;
            Payload = payload;
        }

        public T GetPayload<T>()
        {
            return Payload is T v ? v : throw new InvalidCastException($"Payload is not {typeof(T).Name}");
        }

        public bool TryGetPayload<T>(out T value)
        {
            if (Payload is T v)
            {
                value = v;
                return true;
            }

            value = default;
            return false;
        }
    }
}
