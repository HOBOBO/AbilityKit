using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Diagnostics;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.World.Entitas
{
    internal static class EntitasWorldComposer
    {
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
                var remain = new bool[moduleEntries.Count];
                for (int i = 0; i < indegree.Length; i++)
                {
                    // Nodes not emitted by Kahn's algorithm have indegree > 0.
                    remain[i] = indegree[i] > 0;
                }

                List<int> FindAnyCyclePath()
                {
                    var state = new byte[moduleEntries.Count];
                    var parent = new int[moduleEntries.Count];
                    for (int i = 0; i < parent.Length; i++) parent[i] = -1;

                    List<int> cycle = null;

                    bool Dfs(int u)
                    {
                        state[u] = 1;
                        var outs = outgoing[u];
                        for (int oi = 0; oi < outs.Count; oi++)
                        {
                            var v = outs[oi];
                            if (!remain[v]) continue;

                            if (state[v] == 0)
                            {
                                parent[v] = u;
                                if (Dfs(v)) return true;
                            }
                            else if (state[v] == 1)
                            {
                                // Found a back edge u -> v, reconstruct v..u..v
                                var path = new List<int>(8);
                                path.Add(v);
                                var cur = u;
                                while (cur != -1 && cur != v)
                                {
                                    path.Add(cur);
                                    cur = parent[cur];
                                }
                                path.Add(v);
                                path.Reverse();
                                cycle = path;
                                return true;
                            }
                        }

                        state[u] = 2;
                        return false;
                    }

                    for (int i = 0; i < remain.Length; i++)
                    {
                        if (!remain[i]) continue;
                        if (state[i] != 0) continue;
                        if (Dfs(i)) break;
                    }

                    return cycle;
                }

                var cyclePath = FindAnyCyclePath();
                if (cyclePath == null || cyclePath.Count < 2)
                {
                    throw new InvalidOperationException($"World[{world.Id.Value}/{world.WorldType}] module dependency cycle detected.");
                }

                var sb = new System.Text.StringBuilder(256);
                for (int i = 0; i < cyclePath.Count; i++)
                {
                    var idx = cyclePath[i];
                    if (i > 0) sb.Append(" -> ");
                    sb.Append(moduleEntries[idx].Module.GetType().FullName);
                }

                throw new InvalidOperationException(
                    $"World[{world.Id.Value}/{world.WorldType}] module dependency cycle detected: {sb}");
            }

            moduleEntries = ordered;

            builder.RegisterInstance<WorldId>(world.Id);
            builder.RegisterInstance<string>(world.WorldType);
            builder.RegisterInstance<IWorld>(world);
            builder.RegisterInstance<IEntitasWorld>(world);

            builder.RegisterInstance<global::Entitas.IContexts>(world.Contexts);
            builder.RegisterInstance<global::Entitas.Systems>(world.Systems);

            builder.Register<IEntitasWorldContext>(WorldLifetime.Scoped, r => new EntitasWorldContext(world.Id, world.WorldType, world.Contexts, world.Systems, r));
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

            var report = new WorldCompositionReport(world.Id.Value, world.WorldType);

            for (int i = 0; i < moduleEntries.Count; i++)
            {
                var e = moduleEntries[i];
                var id = e.Info?.Id;
                var order = e.Info?.Order ?? 0;
                report.AddModule(new WorldCompositionReport.ModuleEntry(
                    index: i,
                    sourceIndex: e.Index,
                    order: order,
                    id: id,
                    type: e.Module.GetType().FullName));

                if (e.Module is IEntitasSystemsInstaller)
                {
                    report.AddInstaller(e.Module.GetType().FullName);
                }
            }

            foreach (var t in container.RegisteredServiceTypes)
            {
                report.AddRegisteredService(t.FullName);
            }

            WorldDebugRegistry.Report(report);

            logger.Info($"World compose done: id={world.Id.Value}, type={world.WorldType}");
        }
    }
}
