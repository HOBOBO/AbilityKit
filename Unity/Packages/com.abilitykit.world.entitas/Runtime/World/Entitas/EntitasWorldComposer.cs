using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.World.Entitas
{
    internal static class EntitasWorldComposer
    {
        private sealed class CompositionReport
        {
            public string WorldId;
            public string WorldType;
            public readonly List<string> Modules = new List<string>(32);
            public readonly List<string> Installers = new List<string>(16);
            public readonly List<string> RegisteredServices = new List<string>(128);
        }

        private static class CompositionRegistry
        {
            private static readonly Dictionary<string, CompositionReport> Reports = new Dictionary<string, CompositionReport>(StringComparer.Ordinal);

            public static void Report(CompositionReport report)
            {
                if (report == null) return;
                Reports[report.WorldId ?? string.Empty] = report;
            }
        }

        public static void Compose(EntitasWorld world, WorldCreateOptions options)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (options == null) throw new ArgumentNullException(nameof(options));

            var builder = options.ServiceBuilder ?? WorldServiceContainerFactory.CreateDefaultOnly();

            var modules = options.Modules;
            var moduleEntries = new List<(IWorldModule Module, int Index, IWorldModuleInfo Info)>();
            if (modules != null)
            {
                for (int i = 0; i < modules.Count; i++)
                {
                    var m = modules[i];
                    if (m == null) continue;
                    moduleEntries.Add((m, i, m as IWorldModuleInfo));
                }
            }

            // Module governance v2:
            // - Duplicate detection (by module type and by optional Id)
            // - Conflict detection
            // - Dependency validation and topological sort (DependsOn)
            // - Stable tie-breaker: Order then source Index
            var moduleTypes = moduleEntries.Select(e => e.Module.GetType()).ToArray();

            var typeSeen = new HashSet<Type>();
            var idSeen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < moduleEntries.Count; i++)
            {
                var t = moduleTypes[i];
                if (!typeSeen.Add(t))
                {
                    throw new InvalidOperationException($"World[{world.Id.Value}/{world.WorldType}] duplicate world module type: {t.FullName}");
                }

                var id = moduleEntries[i].Info?.Id;
                if (!string.IsNullOrEmpty(id) && !idSeen.Add(id))
                {
                    throw new InvalidOperationException($"World[{world.Id.Value}/{world.WorldType}] duplicate world module id: {id}");
                }
            }

            for (int i = 0; i < moduleEntries.Count; i++)
            {
                var info = moduleEntries[i].Info;
                if (info?.ConflictsWith == null || info.ConflictsWith.Length == 0) continue;

                for (int c = 0; c < info.ConflictsWith.Length; c++)
                {
                    var conflict = info.ConflictsWith[c];
                    if (conflict == null) continue;

                    for (int t = 0; t < moduleTypes.Length; t++)
                    {
                        if (conflict.IsAssignableFrom(moduleTypes[t]))
                        {
                            throw new InvalidOperationException(
                                $"World[{world.Id.Value}/{world.WorldType}] module conflict: module={moduleEntries[i].Module.GetType().FullName}, conflictsWith={conflict.FullName}, found={moduleTypes[t].FullName}");
                        }
                    }
                }
            }

            // Build graph edges: dep -> module
            var indegree = new int[moduleEntries.Count];
            var outgoing = new List<int>[moduleEntries.Count];
            for (int i = 0; i < outgoing.Length; i++) outgoing[i] = new List<int>();

            for (int i = 0; i < moduleEntries.Count; i++)
            {
                var info = moduleEntries[i].Info;
                if (info?.DependsOn == null || info.DependsOn.Length == 0) continue;

                for (int d = 0; d < info.DependsOn.Length; d++)
                {
                    var dep = info.DependsOn[d];
                    if (dep == null) continue;

                    var depIndex = -1;
                    for (int t = 0; t < moduleTypes.Length; t++)
                    {
                        if (dep.IsAssignableFrom(moduleTypes[t]))
                        {
                            depIndex = t;
                            break;
                        }
                    }

                    if (depIndex < 0)
                    {
                        throw new InvalidOperationException(
                            $"World[{world.Id.Value}/{world.WorldType}] module dependency missing: module={moduleEntries[i].Module.GetType().FullName}, dependsOn={dep.FullName}");
                    }

                    outgoing[depIndex].Add(i);
                    indegree[i]++;
                }
            }

            // Kahn topo-sort with stable ordering (Order, Index)
            var ready = new List<int>(moduleEntries.Count);
            for (int i = 0; i < indegree.Length; i++)
            {
                if (indegree[i] == 0) ready.Add(i);
            }

            int CompareNodes(int a, int b)
            {
                var ao = moduleEntries[a].Info?.Order ?? 0;
                var bo = moduleEntries[b].Info?.Order ?? 0;
                var c = ao.CompareTo(bo);
                return c != 0 ? c : moduleEntries[a].Index.CompareTo(moduleEntries[b].Index);
            }

            ready.Sort(CompareNodes);

            var ordered = new List<(IWorldModule Module, int Index, IWorldModuleInfo Info)>(moduleEntries.Count);
            while (ready.Count > 0)
            {
                var n = ready[0];
                ready.RemoveAt(0);

                ordered.Add(moduleEntries[n]);

                var outs = outgoing[n];
                for (int oi = 0; oi < outs.Count; oi++)
                {
                    var m = outs[oi];
                    indegree[m]--;
                    if (indegree[m] == 0)
                    {
                        ready.Add(m);
                    }
                }

                if (ready.Count > 1) ready.Sort(CompareNodes);
            }

            if (ordered.Count != moduleEntries.Count)
            {
                throw new InvalidOperationException($"World[{world.Id.Value}/{world.WorldType}] module dependency cycle detected.");
            }

            moduleEntries = ordered;

            builder.RegisterInstance<WorldId>(world.Id);
            builder.RegisterInstance<string>(world.WorldType);
            builder.RegisterInstance<IWorld>(world);
            builder.RegisterInstance<IEntitasWorld>(world);

            builder.RegisterInstance<global::Entitas.IContexts>(world.Contexts);
            builder.RegisterInstance<global::Entitas.Systems>(world.Systems);

            builder.Register<IEntitasWorldContext>(WorldLifetime.Scoped, r => new EntitasWorldContext(world.Id, world.WorldType, world.Contexts, world.Systems, (IWorldServices)r));
            builder.Register<IWorldContext>(WorldLifetime.Scoped, r => r.Resolve<IEntitasWorldContext>());

            for (int i = 0; i < moduleEntries.Count; i++)
            {
                builder.AddModule(moduleEntries[i].Module);
            }

            var container = builder.Build();
            var scope = container.CreateScope();
            world.SetComposition(container, scope);

            var logger = scope.Resolve<IWorldLogger>();
            logger.Info($"World compose start: id={world.Id.Value}, type={world.WorldType}");

            moduleTypes = moduleEntries.Select(e => e.Module.GetType()).ToArray();
            for (int i = 0; i < moduleEntries.Count; i++)
            {
                var e = moduleEntries[i];
                var id = e.Info?.Id;
                var order = e.Info?.Order ?? 0;
                logger.Info($"World module[{i}] (srcIndex={e.Index}, order={order}, id={id ?? "<null>"}): {e.Module.GetType().FullName}");
            }

            logger.Info($"World services registered: {container.RegisteredServiceTypes.Count}");
            foreach (var t in container.RegisteredServiceTypes)
            {
                logger.Info($"World service: {t.FullName}");
            }

            for (int i = 0; i < moduleEntries.Count; i++)
            {
                if (moduleEntries[i].Module is IEntitasSystemsInstaller installer)
                {
                    logger.Info($"World installer[{i}]: {installer.GetType().FullName}");
                    installer.Install(world.Contexts, world.Systems, scope);
                }
            }

            world.Systems.Initialize();

            var report = new CompositionReport
            {
                WorldId = world.Id.Value,
                WorldType = world.WorldType,
            };

            for (int i = 0; i < moduleEntries.Count; i++)
            {
                var e = moduleEntries[i];
                report.Modules.Add(e.Module.GetType().FullName);
                if (e.Module is IEntitasSystemsInstaller) report.Installers.Add(e.Module.GetType().FullName);
            }

            foreach (var t in container.RegisteredServiceTypes)
            {
                report.RegisteredServices.Add(t.FullName);
            }

            CompositionRegistry.Report(report);

            logger.Info($"World compose done: id={world.Id.Value}, type={world.WorldType}");
        }
    }
}
