using System;
using System.Collections.Generic;
using System.Reflection;
using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.World.Entitas
{
    public static class AutoSystemInstaller
    {
        public static void Install(
            global::Entitas.IContexts contexts,
            global::Entitas.Systems systems,
            IWorldResolver services,
            IReadOnlyList<Assembly> assemblies,
            IReadOnlyList<string> namespacePrefixes)
        {
            if (contexts == null) throw new ArgumentNullException(nameof(contexts));
            if (systems == null) throw new ArgumentNullException(nameof(systems));
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (assemblies == null || assemblies.Count == 0) return;
            if (namespacePrefixes == null || namespacePrefixes.Count == 0) return;

            var candidates = new List<(WorldSystemPhase phase, int order, Type type)>(64);

            for (int ai = 0; ai < assemblies.Count; ai++)
            {
                var asm = assemblies[ai];
                if (asm == null) continue;

                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types; }

                if (types == null) continue;

                for (int i = 0; i < types.Length; i++)
                {
                    var t = types[i];
                    if (t == null) continue;
                    if (t.IsAbstract || t.IsInterface) continue;

                    var ns = t.Namespace;
                    if (string.IsNullOrEmpty(ns)) continue;

                    var nsOk = false;
                    for (int pi = 0; pi < namespacePrefixes.Count; pi++)
                    {
                        var p = namespacePrefixes[pi];
                        if (string.IsNullOrEmpty(p)) continue;
                        if (ns.StartsWith(p, StringComparison.Ordinal)) { nsOk = true; break; }
                    }
                    if (!nsOk) continue;

                    var attr = t.GetCustomAttribute<WorldSystemAttribute>(inherit: false);
                    if (attr == null) continue;

                    candidates.Add((attr.Phase, attr.Order, t));
                }
            }

            if (candidates.Count == 0) return;
            candidates.Sort((a, b) =>
            {
                var c = ((int)a.phase).CompareTo((int)b.phase);
                if (c != 0) return c;
                c = a.order.CompareTo(b.order);
                if (c != 0) return c;
                return string.CompareOrdinal(a.type.FullName, b.type.FullName);
            });

            var phaseFeatures = new Dictionary<WorldSystemPhase, global::Entitas.Systems>();

            for (int i = 0; i < candidates.Count; i++)
            {
                var phase = candidates[i].phase;
                var t = candidates[i].type;

                if (!phaseFeatures.TryGetValue(phase, out var feature) || feature == null)
                {
                    feature = new global::Entitas.Systems();
                    phaseFeatures[phase] = feature;
                }

                object instance;
                try
                {
                    instance = Activator.CreateInstance(t, contexts, services);
                }
                catch (MissingMethodException e)
                {
                    throw new InvalidOperationException(
                        $"[AutoSystemInstaller] System '{t.FullName}' must have a constructor (Entitas.IContexts contexts, IWorldResolver services).", e);
                }

                if (instance is global::Entitas.ISystem es)
                {
                    feature.Add(es);
                }
                else
                {
                    throw new InvalidOperationException($"[AutoSystemInstaller] Type '{t.FullName}' is marked with [WorldSystem] but does not implement Entitas.ISystem.");
                }
            }

            // Add features in phase order to keep execution order stable.
            for (var phase = WorldSystemPhase.PreExecute; phase <= WorldSystemPhase.PostExecute; phase++)
            {
                if (phaseFeatures.TryGetValue(phase, out var feature) && feature != null)
                {
                    systems.Add(feature);
                }
            }
        }
    }
}
