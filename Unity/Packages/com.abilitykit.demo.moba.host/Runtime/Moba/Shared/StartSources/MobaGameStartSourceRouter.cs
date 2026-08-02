using System;
using System.Collections.Generic;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.Moba.Struct;

namespace AbilityKit.Ability.Host.Extensions.Moba.StartSources
{
    public sealed class MobaGameStartSourceRouter : IMobaGameStartSource
    {
        private readonly Dictionary<MobaGameStartSourceKey, Registration> _sources = new Dictionary<MobaGameStartSourceKey, Registration>();
        private readonly List<MobaGameStartSourceKey> _order = new List<MobaGameStartSourceKey>(4);
        private int _nextRegistrationIndex;

        public MobaGameStartSourceKey PreferredKey { get; set; }

        public MobaGameStartSourceKey Key { get; } = new MobaGameStartSourceKey("router");

        public int Priority => int.MinValue;

        public void Register(IMobaGameStartSource source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!source.Key.IsValid) throw new ArgumentException("source key must be valid", nameof(source));

            var key = source.Key;
            if (!_sources.ContainsKey(key))
            {
                _order.Add(key);
            }

            _sources[key] = new Registration(source, _nextRegistrationIndex++);
            SortOrder();
        }

        public bool TryBuild(PlayerId localPlayerId, out MobaRoomGameStartSpec spec)
        {
            if (PreferredKey.IsValid && TryBuild(PreferredKey, localPlayerId, out spec)) return true;

            for (int i = 0; i < _order.Count; i++)
            {
                var key = _order[i];
                if (PreferredKey.IsValid && key == PreferredKey) continue;
                if (TryBuild(key, localPlayerId, out spec)) return true;
            }

            spec = default;
            return false;
        }

        public bool TryBuild(MobaGameStartSourceKey key, PlayerId localPlayerId, out MobaRoomGameStartSpec spec)
        {
            if (!key.IsValid || !_sources.TryGetValue(key, out var registration) || registration.Source == null)
            {
                spec = default;
                return false;
            }

            return registration.Source.TryBuild(localPlayerId, out spec);
        }

        private void SortOrder()
        {
            _order.Sort(CompareRegistrations);
        }

        private int CompareRegistrations(MobaGameStartSourceKey left, MobaGameStartSourceKey right)
        {
            var a = _sources[left];
            var b = _sources[right];
            var priority = b.Source.Priority.CompareTo(a.Source.Priority);
            if (priority != 0) return priority;
            return a.RegistrationIndex.CompareTo(b.RegistrationIndex);
        }

        private readonly struct Registration
        {
            public readonly IMobaGameStartSource Source;
            public readonly int RegistrationIndex;

            public Registration(IMobaGameStartSource source, int registrationIndex)
            {
                Source = source;
                RegistrationIndex = registrationIndex;
            }
        }
    }
}
