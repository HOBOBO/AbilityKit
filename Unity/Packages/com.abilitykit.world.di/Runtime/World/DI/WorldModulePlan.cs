using System;
using System.Collections.Generic;
using System.Text;

namespace AbilityKit.Ability.World.DI
{
    public sealed class WorldModulePlan
    {
        private readonly WorldModulePlanEntry[] _entries;

        internal WorldModulePlan(WorldModulePlanEntry[] entries)
        {
            _entries = entries ?? throw new ArgumentNullException(nameof(entries));
        }

        public IReadOnlyList<WorldModulePlanEntry> Entries => _entries;
    }

    public readonly struct WorldModulePlanEntry
    {
        public WorldModulePlanEntry(
            IWorldModule module,
            int sourceIndex,
            int order,
            string id)
        {
            Module = module ?? throw new ArgumentNullException(nameof(module));
            SourceIndex = sourceIndex;
            Order = order;
            Id = id;
        }

        public IWorldModule Module { get; }
        public int SourceIndex { get; }
        public int Order { get; }
        public string Id { get; }
        public Type ModuleType => Module.GetType();
    }

    public static class WorldModulePlanner
    {
        public static WorldModulePlan Create(
            IReadOnlyList<IWorldModule> modules,
            string diagnosticContext = null)
        {
            var context = string.IsNullOrEmpty(diagnosticContext)
                ? "World"
                : diagnosticContext;
            var entries = CollectEntries(modules);
            ValidateDuplicates(entries, context);
            ValidateConflicts(entries, context);

            var outgoing = new List<int>[entries.Count];
            var indegree = new int[entries.Count];
            for (var i = 0; i < outgoing.Length; i++)
            {
                outgoing[i] = new List<int>();
            }

            BuildDependencyGraph(entries, outgoing, indegree, context);
            var ordered = Sort(entries, outgoing, indegree, context);
            return new WorldModulePlan(ordered.ToArray());
        }

        private static List<WorldModulePlanEntry> CollectEntries(
            IReadOnlyList<IWorldModule> modules)
        {
            var entries = new List<WorldModulePlanEntry>(modules?.Count ?? 0);
            if (modules == null)
            {
                return entries;
            }

            for (var i = 0; i < modules.Count; i++)
            {
                var module = modules[i];
                if (module == null)
                {
                    continue;
                }

                var info = module as IWorldModuleInfo;
                entries.Add(new WorldModulePlanEntry(
                    module,
                    i,
                    info?.Order ?? 0,
                    info?.Id));
            }

            return entries;
        }

        private static void ValidateDuplicates(
            IReadOnlyList<WorldModulePlanEntry> entries,
            string context)
        {
            var typeSeen = new HashSet<Type>();
            var idSeen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (!typeSeen.Add(entry.ModuleType))
                {
                    throw new InvalidOperationException(
                        $"{context} duplicate world module type: {entry.ModuleType.FullName}");
                }

                if (!string.IsNullOrEmpty(entry.Id) && !idSeen.Add(entry.Id))
                {
                    throw new InvalidOperationException(
                        $"{context} duplicate world module id: {entry.Id}");
                }
            }
        }

        private static void ValidateConflicts(
            IReadOnlyList<WorldModulePlanEntry> entries,
            string context)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                var info = entries[i].Module as IWorldModuleInfo;
                var conflicts = info?.ConflictsWith;
                if (conflicts == null)
                {
                    continue;
                }

                for (var c = 0; c < conflicts.Length; c++)
                {
                    var conflict = conflicts[c];
                    if (conflict == null)
                    {
                        continue;
                    }

                    for (var t = 0; t < entries.Count; t++)
                    {
                        if (!conflict.IsAssignableFrom(entries[t].ModuleType))
                        {
                            continue;
                        }

                        throw new InvalidOperationException(
                            $"{context} module conflict: module={entries[i].ModuleType.FullName}, " +
                            $"conflictsWith={conflict.FullName}, found={entries[t].ModuleType.FullName}");
                    }
                }
            }
        }

        private static void BuildDependencyGraph(
            IReadOnlyList<WorldModulePlanEntry> entries,
            IReadOnlyList<List<int>> outgoing,
            int[] indegree,
            string context)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                var info = entries[i].Module as IWorldModuleInfo;
                var dependencies = info?.DependsOn;
                if (dependencies == null)
                {
                    continue;
                }

                for (var d = 0; d < dependencies.Length; d++)
                {
                    var dependency = dependencies[d];
                    if (dependency == null)
                    {
                        continue;
                    }

                    var dependencyIndex = FindAssignableEntry(entries, dependency);
                    if (dependencyIndex < 0)
                    {
                        throw new InvalidOperationException(
                            $"{context} module dependency missing: module={entries[i].ModuleType.FullName}, " +
                            $"dependsOn={dependency.FullName}");
                    }

                    outgoing[dependencyIndex].Add(i);
                    indegree[i]++;
                }
            }
        }

        private static int FindAssignableEntry(
            IReadOnlyList<WorldModulePlanEntry> entries,
            Type requestedType)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                if (requestedType.IsAssignableFrom(entries[i].ModuleType))
                {
                    return i;
                }
            }

            return -1;
        }

        private static List<WorldModulePlanEntry> Sort(
            IReadOnlyList<WorldModulePlanEntry> entries,
            IReadOnlyList<List<int>> outgoing,
            int[] indegree,
            string context)
        {
            var ready = new List<int>(entries.Count);
            for (var i = 0; i < indegree.Length; i++)
            {
                if (indegree[i] == 0)
                {
                    ready.Add(i);
                }
            }

            int CompareNodes(int left, int right)
            {
                var orderComparison = entries[left].Order.CompareTo(entries[right].Order);
                return orderComparison != 0
                    ? orderComparison
                    : entries[left].SourceIndex.CompareTo(entries[right].SourceIndex);
            }

            ready.Sort(CompareNodes);
            var ordered = new List<WorldModulePlanEntry>(entries.Count);
            while (ready.Count > 0)
            {
                var node = ready[0];
                ready.RemoveAt(0);
                ordered.Add(entries[node]);

                var dependents = outgoing[node];
                for (var i = 0; i < dependents.Count; i++)
                {
                    var dependent = dependents[i];
                    indegree[dependent]--;
                    if (indegree[dependent] == 0)
                    {
                        ready.Add(dependent);
                    }
                }

                if (ready.Count > 1)
                {
                    ready.Sort(CompareNodes);
                }
            }

            if (ordered.Count == entries.Count)
            {
                return ordered;
            }

            var cycle = FindCycle(entries.Count, outgoing, indegree);
            if (cycle == null || cycle.Count < 2)
            {
                throw new InvalidOperationException(
                    $"{context} module dependency cycle detected.");
            }

            var path = new StringBuilder(256);
            for (var i = 0; i < cycle.Count; i++)
            {
                if (i > 0)
                {
                    path.Append(" -> ");
                }

                path.Append(entries[cycle[i]].ModuleType.FullName);
            }

            throw new InvalidOperationException(
                $"{context} module dependency cycle detected: {path}");
        }

        private static List<int> FindCycle(
            int count,
            IReadOnlyList<List<int>> outgoing,
            IReadOnlyList<int> indegree)
        {
            var state = new byte[count];
            var parent = new int[count];
            for (var i = 0; i < parent.Length; i++)
            {
                parent[i] = -1;
            }

            List<int> cycle = null;

            bool Visit(int node)
            {
                state[node] = 1;
                var dependents = outgoing[node];
                for (var i = 0; i < dependents.Count; i++)
                {
                    var dependent = dependents[i];
                    if (indegree[dependent] <= 0)
                    {
                        continue;
                    }

                    if (state[dependent] == 0)
                    {
                        parent[dependent] = node;
                        if (Visit(dependent))
                        {
                            return true;
                        }
                    }
                    else if (state[dependent] == 1)
                    {
                        var path = new List<int>(8) { dependent };
                        var current = node;
                        while (current != -1 && current != dependent)
                        {
                            path.Add(current);
                            current = parent[current];
                        }

                        path.Add(dependent);
                        path.Reverse();
                        cycle = path;
                        return true;
                    }
                }

                state[node] = 2;
                return false;
            }

            for (var i = 0; i < count; i++)
            {
                if (indegree[i] > 0 && state[i] == 0 && Visit(i))
                {
                    break;
                }
            }

            return cycle;
        }
    }
}
