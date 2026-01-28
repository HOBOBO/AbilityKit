using System;
using System.Collections.Generic;

namespace AbilityKit.Triggering.Eventing
{
    public interface IEventSchemaRegistry
    {
        void Register<TArgs>(int eventId, string name = null);
        bool TryGetArgsType(int eventId, out Type argsType);
        bool TryGetName(int eventId, out string name);
    }

    public sealed class EventSchemaRegistry : IEventSchemaRegistry
    {
        private readonly Dictionary<int, Type> _argsTypes = new Dictionary<int, Type>();
        private readonly Dictionary<int, string> _names = new Dictionary<int, string>();

        public void Register<TArgs>(int eventId, string name = null)
        {
            if (eventId == 0) throw new ArgumentOutOfRangeException(nameof(eventId), "eventId must not be 0");

            var t = typeof(TArgs);
            if (_argsTypes.TryGetValue(eventId, out var existing) && existing != t)
            {
                throw new InvalidOperationException($"EventSchemaRegistry argsType mismatch for eventId={eventId}: existing={existing.FullName}, new={t.FullName}");
            }

            _argsTypes[eventId] = t;
            if (!string.IsNullOrEmpty(name))
            {
                _names[eventId] = name;
            }
        }

        public bool TryGetArgsType(int eventId, out Type argsType)
        {
            return _argsTypes.TryGetValue(eventId, out argsType);
        }

        public bool TryGetName(int eventId, out string name)
        {
            return _names.TryGetValue(eventId, out name);
        }
    }
}
