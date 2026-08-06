using System;
using System.Collections.Generic;
using AbilityKit.Core.Logging;
using AbilityKit.Core.Markers;

namespace AbilityKit.Demo.Moba.Services
{
    public sealed class MobaBattleRouteRegistry : IMarkerRegistry
    {
        private readonly List<MobaBattleRouteDescriptor> _descriptors = new List<MobaBattleRouteDescriptor>(16);
        private readonly Dictionary<Key, MobaBattleRouteDescriptor> _byKey = new Dictionary<Key, MobaBattleRouteDescriptor>();

        public int Count => _descriptors.Count;

        public IReadOnlyList<MobaBattleRouteDescriptor> Descriptors => _descriptors;

        public static MobaBattleRouteRegistry CreateDefault()
        {
            var registry = new MobaBattleRouteRegistry();
            if (RegisterGeneratedAndExternalRoutes(registry) == 0)
            {
                if (AppContext.TryGetSwitch(
                        "AbilityKit.Moba.DisableBattleRouteReflectionFallback",
                        out var reflectionFallbackDisabled) && reflectionFallbackDisabled)
                {
                    throw new InvalidOperationException(
                        "The generated MOBA battle route manifest is empty and reflection fallback is disabled.");
                }

                MarkerScanner<MobaBattleRouteAttribute>.Scan(
                    new[] { typeof(MobaBattleRouteRegistry).Assembly },
                    registry);
            }

            return registry;
        }

        private static int RegisterGeneratedAndExternalRoutes(MobaBattleRouteRegistry registry)
        {
            var runtimeAssembly = typeof(MobaBattleRouteRegistry).Assembly;
            var generatedCount = MobaGeneratedBattleRouteManifest.Register(registry);
            var assemblies = MobaRegistryAssemblyDiscovery.GetExternalAssemblies(runtimeAssembly);
            for (int i = 0; i < assemblies.Length; i++)
            {
                MarkerScanner<MobaBattleRouteAttribute>.Scan(new[] { assemblies[i] }, registry);
            }

            return generatedCount;
        }

        public void Register(Type implType)
        {
        }

        public bool Register(MobaBattleRouteDescriptor descriptor)
        {
            if (descriptor.OpCode == 0 || descriptor.Kind == MobaBattleRouteKind.Unknown)
            {
                return false;
            }

            var key = new Key(descriptor.Kind, descriptor.OpCode);
            if (_byKey.TryGetValue(key, out var existing))
            {
                Log.Warning($"[MobaBattleRouteRegistry] Duplicate route declaration ignored. kind={descriptor.Kind}, opCode={descriptor.OpCode}, existing={existing.OwnerType?.FullName}, incoming={descriptor.OwnerType?.FullName}");
                return false;
            }

            _byKey.Add(key, descriptor);
            _descriptors.Add(descriptor);
            return true;
        }

        public bool TryGet(MobaBattleRouteKind kind, int opCode, out MobaBattleRouteDescriptor descriptor)
        {
            return _byKey.TryGetValue(new Key(kind, opCode), out descriptor);
        }

        public void GetByKind(MobaBattleRouteKind kind, List<MobaBattleRouteDescriptor> results)
        {
            if (results == null)
            {
                return;
            }

            for (int i = 0; i < _descriptors.Count; i++)
            {
                var descriptor = _descriptors[i];
                if (descriptor.Kind == kind)
                {
                    results.Add(descriptor);
                }
            }
        }

        private readonly struct Key : IEquatable<Key>
        {
            private readonly MobaBattleRouteKind _kind;
            private readonly int _opCode;

            public Key(MobaBattleRouteKind kind, int opCode)
            {
                _kind = kind;
                _opCode = opCode;
            }

            public bool Equals(Key other)
            {
                return _kind == other._kind && _opCode == other._opCode;
            }

            public override bool Equals(object obj)
            {
                return obj is Key other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((int)_kind * 397) ^ _opCode;
                }
            }
        }
    }
}
