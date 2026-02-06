using System;
using System.Collections.Generic;

namespace AbilityKit.Game.Flow.Modules
{
    public sealed class ModuleHost<TContext, TModule> where TModule : class, IGameModule<TContext>
    {
        private readonly Action<string> _fail;
        private readonly List<TModule> _modules;

        private bool _isAttached;

        public IReadOnlyList<TModule> Modules => _modules;

        public ModuleHost(List<TModule> modules, Action<string> fail)
        {
            _modules = modules ?? new List<TModule>(8);
            _fail = fail;
        }

        private void Fail(string message)
        {
            _fail?.Invoke(message);
        }

        public bool TryGetModuleById(string id, out TModule module)
        {
            module = null;
            if (string.IsNullOrEmpty(id)) return false;

            for (int i = 0; i < _modules.Count; i++)
            {
                var m = _modules[i];
                if (m is not IGameModuleId mid) continue;
                if (string.Equals(mid.Id, id, StringComparison.Ordinal))
                {
                    module = m;
                    return true;
                }
            }

            return false;
        }

        public List<string> GetModuleIds()
        {
            var list = new List<string>(_modules.Count);
            for (int i = 0; i < _modules.Count; i++)
            {
                if (_modules[i] is IGameModuleId mid && !string.IsNullOrEmpty(mid.Id))
                {
                    list.Add(mid.Id);
                }
                else
                {
                    list.Add(_modules[i]?.GetType().Name ?? "<null>");
                }
            }
            return list;
        }

        public bool TrySortByDependencies()
        {
            if (_modules.Count <= 1) return true;

            var initialIndex = new Dictionary<TModule, int>(ReferenceEqualityComparer<TModule>.Instance);
            for (int i = 0; i < _modules.Count; i++)
            {
                var m = _modules[i];
                if (m == null)
                {
                    Fail($"Module at index {i} is null.");
                    return false;
                }
                initialIndex[m] = i;
            }

            var idToModule = new Dictionary<string, TModule>(StringComparer.Ordinal);
            for (int i = 0; i < _modules.Count; i++)
            {
                var m = _modules[i];
                if (m is not IGameModuleId mid || string.IsNullOrEmpty(mid.Id))
                {
                    Fail($"Module at index {i} ({m.GetType().Name}) does not implement IGameModuleId or Id is empty.");
                    return false;
                }

                if (idToModule.ContainsKey(mid.Id))
                {
                    Fail($"Duplicate module id '{mid.Id}'. Modules={string.Join(", ", GetModuleIds())}");
                    return false;
                }

                idToModule[mid.Id] = m;
            }

            var dependencyIds = new Dictionary<TModule, List<string>>(ReferenceEqualityComparer<TModule>.Instance);
            var dependents = new Dictionary<TModule, List<TModule>>(ReferenceEqualityComparer<TModule>.Instance);
            var inDegree = new Dictionary<TModule, int>(ReferenceEqualityComparer<TModule>.Instance);

            for (int i = 0; i < _modules.Count; i++)
            {
                var m = _modules[i];
                inDegree[m] = 0;
                dependents[m] = new List<TModule>(4);
            }

            for (int i = 0; i < _modules.Count; i++)
            {
                var m = _modules[i];
                if (m is not IGameModuleDependencies d || d.Dependencies == null) continue;

                var mids = (IGameModuleId)m;
                var list = new List<string>();

                foreach (var depId in d.Dependencies)
                {
                    if (string.IsNullOrEmpty(depId))
                    {
                        Fail($"Module '{mids.Id}' declares an empty dependency id. Modules={string.Join(", ", GetModuleIds())}");
                        return false;
                    }

                    if (!idToModule.TryGetValue(depId, out var depModule) || depModule == null)
                    {
                        Fail($"Module '{mids.Id}' depends on missing module '{depId}'. Modules={string.Join(", ", GetModuleIds())}");
                        return false;
                    }

                    list.Add(depId);
                    inDegree[m] = inDegree[m] + 1;
                    dependents[depModule].Add(m);
                }

                if (list.Count > 0)
                {
                    dependencyIds[m] = list;
                }
            }

            var ready = new List<TModule>(_modules.Count);
            for (int i = 0; i < _modules.Count; i++)
            {
                var m = _modules[i];
                if (inDegree[m] == 0) ready.Add(m);
            }

            ready.Sort((a, b) => initialIndex[a].CompareTo(initialIndex[b]));

            var sorted = new List<TModule>(_modules.Count);
            while (ready.Count > 0)
            {
                var n = ready[0];
                ready.RemoveAt(0);
                sorted.Add(n);

                var outs = dependents[n];
                for (int i = 0; i < outs.Count; i++)
                {
                    var m = outs[i];
                    inDegree[m] = inDegree[m] - 1;
                    if (inDegree[m] == 0)
                    {
                        ready.Add(m);
                    }
                }

                ready.Sort((a, b) => initialIndex[a].CompareTo(initialIndex[b]));
            }

            if (sorted.Count != _modules.Count)
            {
                var stuck = new List<string>();
                for (int i = 0; i < _modules.Count; i++)
                {
                    var m = _modules[i];
                    if (inDegree[m] > 0)
                    {
                        var mid = (IGameModuleId)m;
                        if (dependencyIds.TryGetValue(m, out var ids) && ids != null && ids.Count > 0)
                        {
                            stuck.Add($"{mid.Id} <- [{string.Join(", ", ids)}]");
                        }
                        else
                        {
                            stuck.Add(mid.Id);
                        }
                    }
                }

                Fail($"Cyclic module dependencies detected. Stuck={string.Join("; ", stuck)}");
                return false;
            }

            _modules.Clear();
            _modules.AddRange(sorted);
            return true;
        }

        public void Attach(in TContext ctx)
        {
            if (_isAttached)
            {
                Fail($"Attach called while already attached. Modules={string.Join(", ", GetModuleIds())}");
                return;
            }

            _isAttached = true;
            for (int i = 0; i < _modules.Count; i++)
            {
                _modules[i]?.OnAttach(ctx);
            }
        }

        public void Detach(in TContext ctx)
        {
            if (!_isAttached)
            {
                Fail($"Detach called while not attached. Modules={string.Join(", ", GetModuleIds())}");
                return;
            }

            for (int i = _modules.Count - 1; i >= 0; i--)
            {
                _modules[i]?.OnDetach(ctx);
            }

            _isAttached = false;
        }

        public void Tick(in TContext ctx, float deltaTime)
        {
            for (int i = 0; i < _modules.Count; i++)
            {
                if (_modules[i] is IGameModuleTick<TContext> tick)
                {
                    tick.Tick(ctx, deltaTime);
                }
            }
        }

        public void RebindAll(in TContext ctx)
        {
            for (int i = 0; i < _modules.Count; i++)
            {
                if (_modules[i] is IGameModuleRebind<TContext> rebind)
                {
                    rebind.RebindAll(ctx);
                }
            }
        }

        public void ForEach<TI>(Action<TI> visitor) where TI : class
        {
            if (visitor == null) return;
            for (int i = 0; i < _modules.Count; i++)
            {
                if (_modules[i] is TI t) visitor(t);
            }
        }

        public void ForEachReverse<TI>(Action<TI> visitor) where TI : class
        {
            if (visitor == null) return;
            for (int i = _modules.Count - 1; i >= 0; i--)
            {
                if (_modules[i] is TI t) visitor(t);
            }
        }

        private sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T> where T : class
        {
            public static readonly ReferenceEqualityComparer<T> Instance = new ReferenceEqualityComparer<T>();
            public bool Equals(T x, T y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
