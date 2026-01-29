using System;
using System.Collections.Generic;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.World.Services;
using AbilityKit.Core.Eventing;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class BattleTriggersService : IService
    {
        private readonly AbilityKit.Triggering.Eventing.IEventBus _strongBus;
        private readonly IEventBus _legacyBus;

        private readonly Dictionary<SubscriptionOwner, List<IDisposable>> _strongSubs = new Dictionary<SubscriptionOwner, List<IDisposable>>();
        private readonly Dictionary<SubscriptionOwner, List<IEventSubscription>> _legacySubs = new Dictionary<SubscriptionOwner, List<IEventSubscription>>();

        public BattleTriggersService(AbilityKit.Triggering.Eventing.IEventBus strongBus, IEventBus legacyBus)
        {
            _strongBus = strongBus;
            _legacyBus = legacyBus;
        }

        public bool Publish<TArgs>(EventKey<TArgs> key, in TArgs args)
        {
            if (_strongBus == null) return false;
            _strongBus.Publish(key, in args);
            return true;
        }

        public bool Publish(in TriggerEvent evt)
        {
            if (_legacyBus == null) return false;
            _legacyBus.Publish(in evt);
            return true;
        }

        public IDisposable Subscribe<TArgs>(SubscriptionOwner owner, EventKey<TArgs> key, Action<TArgs> handler)
        {
            if (_strongBus == null) return null;
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var sub = _strongBus.Subscribe(key, handler);
            AddStrongSub(owner, sub);
            return sub;
        }

        public IEventSubscription Subscribe(SubscriptionOwner owner, string eventId, IEventHandler handler)
        {
            if (_legacyBus == null) return null;
            var sub = _legacyBus.Subscribe(eventId, handler);
            AddLegacySub(owner, sub);
            return sub;
        }

        public void UnsubscribeAll(SubscriptionOwner owner)
        {
            if (_strongSubs.TryGetValue(owner, out var list))
            {
                _strongSubs.Remove(owner);
                for (int i = 0; i < list.Count; i++)
                {
                    try
                    {
                        list[i]?.Dispose();
                    }
                    catch
                    {
                    }
                }
            }

            if (_legacySubs.TryGetValue(owner, out var list2))
            {
                _legacySubs.Remove(owner);
                for (int i = 0; i < list2.Count; i++)
                {
                    try
                    {
                        list2[i]?.Unsubscribe();
                    }
                    catch
                    {
                    }
                }
            }
        }

        private void AddStrongSub(SubscriptionOwner owner, IDisposable sub)
        {
            if (sub == null) return;
            if (!_strongSubs.TryGetValue(owner, out var list))
            {
                list = new List<IDisposable>(4);
                _strongSubs.Add(owner, list);
            }
            list.Add(sub);
        }

        private void AddLegacySub(SubscriptionOwner owner, IEventSubscription sub)
        {
            if (sub == null) return;
            if (!_legacySubs.TryGetValue(owner, out var list))
            {
                list = new List<IEventSubscription>(4);
                _legacySubs.Add(owner, list);
            }
            list.Add(sub);
        }

        public void Dispose()
        {
            var strongKeys = new List<SubscriptionOwner>(_strongSubs.Keys);
            for (int i = 0; i < strongKeys.Count; i++) UnsubscribeAll(strongKeys[i]);

            var legacyKeys = new List<SubscriptionOwner>(_legacySubs.Keys);
            for (int i = 0; i < legacyKeys.Count; i++) UnsubscribeAll(legacyKeys[i]);
        }
    }

    public readonly struct SubscriptionOwner : IEquatable<SubscriptionOwner>
    {
        private readonly int _kind;
        private readonly int _a;
        private readonly int _b;

        private SubscriptionOwner(int kind, int a, int b)
        {
            _kind = kind;
            _a = a;
            _b = b;
        }

        public static SubscriptionOwner BuffInstance(int ownerActorId, int buffInstanceId) => new SubscriptionOwner(1, ownerActorId, buffInstanceId);
        public static SubscriptionOwner PassiveSkill(int ownerActorId, int passiveRuntimeId) => new SubscriptionOwner(2, ownerActorId, passiveRuntimeId);
        public static SubscriptionOwner Cast(int castContextId) => new SubscriptionOwner(3, castContextId, 0);

        public bool Equals(SubscriptionOwner other)
        {
            return _kind == other._kind && _a == other._a && _b == other._b;
        }

        public override bool Equals(object obj)
        {
            return obj is SubscriptionOwner other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = _kind;
                hash = (hash * 397) ^ _a;
                hash = (hash * 397) ^ _b;
                return hash;
            }
        }
    }
}
