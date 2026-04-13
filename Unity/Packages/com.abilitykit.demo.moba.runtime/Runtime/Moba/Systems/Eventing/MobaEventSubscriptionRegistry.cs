using System;
using System.Collections.Generic;
using AbilityKit.Core.Common.Log;
using AbilityKit.Core.Common.Event;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Demo.Moba.Systems
{
    public sealed class MobaEventSubscriptionRegistry : IService
    {
        private readonly Dictionary<string, Type> _exact = new Dictionary<string, Type>(StringComparer.Ordinal);
        private readonly List<PrefixEntry> _prefixes = new List<PrefixEntry>(8);

        private readonly struct PrefixEntry
        {
            public readonly string Prefix;
            public readonly Type ArgsType;

            public PrefixEntry(string prefix, Type argsType)
            {
                Prefix = prefix;
                ArgsType = argsType;
            }
        }

        public void RegisterExact<TArgs>(string eventId)
        {
            if (string.IsNullOrEmpty(eventId)) throw new ArgumentException(nameof(eventId));
            _exact[eventId] = typeof(TArgs);
        }

        public void RegisterPrefix<TArgs>(string prefix)
        {
            if (string.IsNullOrEmpty(prefix)) throw new ArgumentException(nameof(prefix));
            _prefixes.Add(new PrefixEntry(prefix, typeof(TArgs)));
        }

        public bool TryGetArgsType(string eventId, out Type argsType)
        {
            argsType = null;
            if (string.IsNullOrEmpty(eventId)) return false;

            if (_exact.TryGetValue(eventId, out argsType) && argsType != null)
            {
                return true;
            }

            for (int i = 0; i < _prefixes.Count; i++)
            {
                var p = _prefixes[i];
                if (eventId.StartsWith(p.Prefix, StringComparison.Ordinal))
                {
                    argsType = p.ArgsType;
                    return argsType != null;
                }
            }

            return false;
        }

        public bool TrySubscribe<TArgs>(IEventBus eventBus, string eventId, Action<TArgs> handler, out IDisposable sub)
        {
            sub = null;
            if (eventBus == null) return false;
            if (string.IsNullOrEmpty(eventId)) return false;

            if (!TryGetArgsType(eventId, out var mappedType) || mappedType == null)
            {
                Log.Warning($"[MobaEventSubscriptionRegistry] Unsupported eventId (no mapping): {eventId}");
                return false;
            }

            if (mappedType != typeof(TArgs))
            {
                Log.Warning($"[MobaEventSubscriptionRegistry] eventId payload type mismatch: eventId={eventId}, mapped={mappedType.Name}, requested={typeof(TArgs).Name}");
                return false;
            }

            var eid = AbilityKit.Demo.Moba.Services.TriggeringIdUtil.GetEventEid(eventId);
            var key = new EventKey<TArgs>(eid);
            sub = eventBus.Subscribe(key, handler);
            return true;
        }

        public void Dispose()
        {
        }
    }
}
