using System;
using System.Reflection;
using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.World.Services.Attributes
{
    public sealed class AttributeWorldServicesModule : IWorldModule
    {
        private readonly bool _scanAllLoadedAssemblies;
        private readonly WorldServiceProfile _profile;
        private readonly Assembly[] _assemblies;
        private readonly string[] _namespacePrefixes;

        private readonly struct CacheKey : IEquatable<CacheKey>
        {
            public readonly WorldServiceProfile Profile;
            public readonly bool ScanAll;
            public readonly int AssembliesHash;
            public readonly int NamespacePrefixesHash;

            public CacheKey(WorldServiceProfile profile, bool scanAll, int assembliesHash, int namespacePrefixesHash)
            {
                Profile = profile;
                ScanAll = scanAll;
                AssembliesHash = assembliesHash;
                NamespacePrefixesHash = namespacePrefixesHash;
            }

            public bool Equals(CacheKey other)
            {
                return Profile == other.Profile && ScanAll == other.ScanAll && AssembliesHash == other.AssembliesHash && NamespacePrefixesHash == other.NamespacePrefixesHash;
            }

            public override bool Equals(object obj)
            {
                return obj is CacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = (int)Profile;
                    hash = (hash * 397) ^ (ScanAll ? 1 : 0);
                    hash = (hash * 397) ^ AssembliesHash;
                    hash = (hash * 397) ^ NamespacePrefixesHash;
                    return hash;
                }
            }
        }

        private readonly struct Registration
        {
            public readonly Type ServiceType;
            public readonly Type ImplType;
            public readonly WorldLifetime Lifetime;
            public readonly bool IsDefault;

            public Registration(Type serviceType, Type implType, WorldLifetime lifetime, bool isDefault)
            {
                ServiceType = serviceType;
                ImplType = implType;
                Lifetime = lifetime;
                IsDefault = isDefault;
            }
        }

        private static readonly System.Collections.Generic.Dictionary<CacheKey, Registration[]> Cache = new System.Collections.Generic.Dictionary<CacheKey, Registration[]>();

        public AttributeWorldServicesModule(WorldServiceProfile profile, Assembly[] assemblies, string[] namespacePrefixes)
        {
            _profile = profile;
            _assemblies = assemblies;
            _namespacePrefixes = namespacePrefixes;
            _scanAllLoadedAssemblies = false;
        }

        public AttributeWorldServicesModule(WorldServiceProfile profile, bool scanAllLoadedAssemblies, string[] namespacePrefixes)
        {
            _profile = profile;
            _assemblies = null;
            _scanAllLoadedAssemblies = scanAllLoadedAssemblies;
            _namespacePrefixes = namespacePrefixes;
        }

        public void Configure(WorldContainerBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            var assemblies = _assemblies;
            if (assemblies == null || assemblies.Length == 0)
            {
                if (!_scanAllLoadedAssemblies)
                {
                    return;
                }

                assemblies = AppDomain.CurrentDomain.GetAssemblies();
            }

            var assembliesHash = 0;
            for (int i = 0; i < assemblies.Length; i++)
            {
                var asm = assemblies[i];
                if (asm == null) continue;
                var name = asm.FullName;
                if (string.IsNullOrEmpty(name)) continue;
                assembliesHash = (assembliesHash * 397) ^ name.GetHashCode();
            }

            var namespacePrefixesHash = 0;
            if (_namespacePrefixes != null)
            {
                for (int i = 0; i < _namespacePrefixes.Length; i++)
                {
                    var p = _namespacePrefixes[i];
                    if (string.IsNullOrEmpty(p)) continue;
                    namespacePrefixesHash = (namespacePrefixesHash * 397) ^ p.GetHashCode();
                }
            }

            var key = new CacheKey(_profile, _scanAllLoadedAssemblies, assembliesHash, namespacePrefixesHash);
            if (!Cache.TryGetValue(key, out var registrations))
            {
                registrations = BuildRegistrations(assemblies, _profile, _namespacePrefixes);
                Cache[key] = registrations;
            }

            for (int i = 0; i < registrations.Length; i++)
            {
                var r = registrations[i];
                if (r.IsDefault)
                {
                    builder.TryRegisterType(r.ServiceType, r.ImplType, r.Lifetime);
                }
                else
                {
                    builder.RegisterType(r.ServiceType, r.ImplType, r.Lifetime);
                }
            }
        }

        private static Registration[] BuildRegistrations(Assembly[] assemblies, WorldServiceProfile profile, string[] namespacePrefixes)
        {
            var list = new System.Collections.Generic.List<Registration>(64);

            for (int a = 0; a < assemblies.Length; a++)
            {
                var asm = assemblies[a];
                if (asm == null) continue;
                Type[] types;
                try
                {
                    types = asm.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = e.Types;
                }

                if (types == null) continue;

                for (int i = 0; i < types.Length; i++)
                {
                    var t = types[i];
                    if (t == null) continue;
                    if (t.IsAbstract) continue;
                    if (t.IsInterface) continue;

                    if (namespacePrefixes != null && namespacePrefixes.Length > 0)
                    {
                        var ns = t.Namespace;
                        if (string.IsNullOrEmpty(ns)) continue;

                        var ok = false;
                        for (int p = 0; p < namespacePrefixes.Length; p++)
                        {
                            var prefix = namespacePrefixes[p];
                            if (string.IsNullOrEmpty(prefix)) continue;
                            if (ns.StartsWith(prefix, StringComparison.Ordinal))
                            {
                                ok = true;
                                break;
                            }
                        }

                        if (!ok) continue;
                    }

                    var attrs = (WorldServiceAttribute[])t.GetCustomAttributes(typeof(WorldServiceAttribute), false);
                    if (attrs == null || attrs.Length == 0) continue;

                    for (int k = 0; k < attrs.Length; k++)
                    {
                        var attr = attrs[k];
                        if (attr.ServiceType == null) continue;
                        if ((attr.Profile & profile) == 0) continue;
                        if (!attr.ServiceType.IsAssignableFrom(t)) continue;

                        list.Add(new Registration(attr.ServiceType, t, attr.Lifetime, attr.IsDefault));
                    }
                }
            }

            return list.ToArray();
        }
    }
}
