using System;
using System.Collections.Generic;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.Moba.Struct;

namespace AbilityKit.Ability.Host.Extensions.Moba.StartSources
{
    public sealed class MobaGameStartSourceRouter : IMobaGameStartSource
    {
        private readonly Dictionary<MobaGameStartSourceKind, IMobaGameStartSource> _sources = new Dictionary<MobaGameStartSourceKind, IMobaGameStartSource>();
        private readonly List<MobaGameStartSourceKind> _order = new List<MobaGameStartSourceKind>(4);

        public MobaGameStartSourceKind PreferredKind { get; set; } = MobaGameStartSourceKind.Room;

        public MobaGameStartSourceKind Kind => MobaGameStartSourceKind.Unknown;

        public void Register(IMobaGameStartSource source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var kind = source.Kind;
            _sources[kind] = source;

            // Keep deterministic fallback order based on first registration.
            if (!_order.Contains(kind))
            {
                _order.Add(kind);
            }
        }

        public bool TryBuild(PlayerId localPlayerId, out MobaRoomGameStartSpec spec)
        {
            if (TryBuild(PreferredKind, localPlayerId, out spec)) return true;

            for (int i = 0; i < _order.Count; i++)
            {
                var kind = _order[i];
                if (kind == PreferredKind) continue;
                if (TryBuild(kind, localPlayerId, out spec)) return true;
            }

            spec = default;
            return false;
        }

        public bool TryBuild(MobaGameStartSourceKind kind, PlayerId localPlayerId, out MobaRoomGameStartSpec spec)
        {
            if (!_sources.TryGetValue(kind, out var src) || src == null)
            {
                spec = default;
                return false;
            }

            return src.TryBuild(localPlayerId, out spec);
        }
    }
}
