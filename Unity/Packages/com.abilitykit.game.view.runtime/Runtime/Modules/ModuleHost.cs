using System;
using System.Collections.Generic;
using AbilityKit.Game.View.Foundation;

namespace AbilityKit.Game.View.Modules
{
    public sealed class ModuleHost<TContext, TModule> where TModule : class, IGameModule<TContext>
    {
        private readonly Action<string>? _fail;
        private readonly List<TModule> _modules;
        private bool _isAttached;

        public ModuleHost(List<TModule>? modules = null, Action<string>? fail = null)
        {
            _modules = modules ?? new List<TModule>(8);
            _fail = fail;
        }

        public IReadOnlyList<TModule> Modules => _modules;
        public bool IsAttached => _isAttached;

        public bool TryGetModuleById(string id, out TModule? module)
        {
            module = null;
            if (string.IsNullOrEmpty(id)) return false;

            for (var i = 0; i < _modules.Count; i++)
            {
                var candidate = _modules[i];
                if (candidate is not IGameModuleId mid) continue;
                if (!string.Equals(mid.Id, id, StringComparison.Ordinal)) continue;

                module = candidate;
                return true;
            }

            return false;
        }

        public bool TrySortByDependencies()
        {
            if (_modules.Count == 0) return true;

            using var moduleIdsLease = ViewFrameworkPools.GetList<string>(_modules.Count);
            var moduleIds = moduleIdsLease.List;
            FillModuleIds(moduleIds);

            var initialIndex = new Dictionary<TModule, int>(ReferenceEqualityComparer<TModule>.Instance);
            var idToModule = new Dictionary<string, TModule>(StringComparer.Ordinal);
            var dependencyIds = new Dictionary<TModule, List<string>>(ReferenceEqualityComparer<TModule>.Instance);
            var dependents = new Dictionary<TModule, List<TModule>>(ReferenceEqualityComparer<TModule>.Instance);
            var inDegree = new Dictionary<TModule, int>(ReferenceEqualityComparer<TModule>.Instance);

            for (var i = 0; i < _modules.Count; i++)
            {
                var module = _modules[i];
                if (module == null)
                {
                    Fail($"Module at index {i} is null.");
                    return false;
                }

                if (module is not IGameModuleId mid || string.IsNullOrEmpty(mid.Id))
                {
                    Fail($"Module at index {i} ({module.GetType().Name}) does not implement IGameModuleId or Id is empty.");
                    return false;
                }

                if (idToModule.ContainsKey(mid.Id))
                {
                    Fail($"Duplicate module id '{mid.Id}'. Modules={string.Join(", ", moduleIds)}");
                    return false;
                }

                initialIndex[module] = i;
                idToModule[mid.Id] = module;
                inDegree[module] = 0;
                dependents[module] = new List<TModule>(4);
            }

            for (var i = 0; i < _modules.Count; i++)
            {
                var module = _modules[i];
                if (module is not IGameModuleDependencies deps || deps.Dependencies == null) continue;

                var mid = (IGameModuleId)module;
                var declared = new List<string>();
                foreach (var depId in deps.Dependencies)
                {
                    if (string.IsNullOrEmpty(depId))
                    {
                        Fail($"Module '{mid.Id}' declares an empty dependency id. Modules={string.Join(", ", moduleIds)}");
                        return false;
                    }

                    if (!idToModule.TryGetValue(depId, out var depModule) || depModule == null)
                    {
                        Fail($"Module '{mid.Id}' depends on missing module '{depId}'. Modules={string.Join(", ", moduleIds)}");
                        return false;
                    }

                    declared.Add(depId);
                    inDegree[module] = inDegree[module] + 1;
                    dependents[depModule].Add(module);
                }

                if (declared.Count > 0)
                {
                    dependencyIds[module] = declared;
                }
            }

            using var readyLease = ViewFrameworkPools.GetList<TModule>(_modules.Count);
            using var sortedLease = ViewFrameworkPools.GetList<TModule>(_modules.Count);
            var ready = readyLease.List;
            var sorted = sortedLease.List;

            for (var i = 0; i < _modules.Count; i++)
            {
                var module = _modules[i];
                if (inDegree[module] == 0) ready.Add(module);
            }

            SortByInitialIndex(ready, initialIndex);
            while (ready.Count > 0)
            {
                var next = ready[0];
                ready.RemoveAt(0);
                sorted.Add(next);

                var outgoing = dependents[next];
                for (var i = 0; i < outgoing.Count; i++)
                {
                    var dependent = outgoing[i];
                    inDegree[dependent] = inDegree[dependent] - 1;
                    if (inDegree[dependent] == 0)
                    {
                        ready.Add(dependent);
                    }
                }

                SortByInitialIndex(ready, initialIndex);
            }

            if (sorted.Count != _modules.Count)
            {
                using var stuckLease = ViewFrameworkPools.GetList<string>(_modules.Count);
                var stuck = stuckLease.List;
                for (var i = 0; i < _modules.Count; i++)
                {
                    var module = _modules[i];
                    if (inDegree[module] <= 0) continue;

                    var mid = (IGameModuleId)module;
                    if (dependencyIds.TryGetValue(module, out var ids) && ids != null && ids.Count > 0)
                    {
                        stuck.Add($"{mid.Id} <- [{string.Join(", ", ids)}]");
                    }
                    else
                    {
                        stuck.Add(mid.Id);
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
                Fail("Attach called while already attached.");
                return;
            }

            var attachedCount = 0;
            try
            {
                for (; attachedCount < _modules.Count; attachedCount++)
                {
                    _modules[attachedCount]?.OnAttach(ctx);
                }

                _isAttached = true;
            }
            catch (Exception attachException)
            {
                var exceptions = new List<Exception> { attachException };
                for (var i = attachedCount - 1; i >= 0; i--)
                {
                    if (_modules[i] == null) continue;
                    try
                    {
                        _modules[i].OnDetach(ctx);
                    }
                    catch (Exception rollbackException)
                    {
                        exceptions.Add(rollbackException);
                    }
                }

                if (exceptions.Count == 1) throw;
                throw new AggregateException("Module attach failed and rollback encountered errors.", exceptions);
            }
        }

        public void Detach(in TContext ctx)
        {
            if (!_isAttached)
            {
                Fail("Detach called while not attached.");
                return;
            }

            List<Exception>? exceptions = null;
            try
            {
                for (var i = _modules.Count - 1; i >= 0; i--)
                {
                    if (_modules[i] == null) continue;
                    try
                    {
                        _modules[i].OnDetach(ctx);
                    }
                    catch (Exception exception)
                    {
                        (exceptions ??= new List<Exception>()).Add(exception);
                    }
                }
            }
            finally
            {
                _isAttached = false;
            }

            if (exceptions != null)
            {
                throw new AggregateException("One or more modules failed to detach.", exceptions);
            }
        }

        public void Tick(in TContext ctx, float deltaTime)
        {
            for (var i = 0; i < _modules.Count; i++)
            {
                if (_modules[i] is IGameModuleTick<TContext> tick)
                {
                    tick.Tick(ctx, deltaTime);
                }
            }
        }

        public void RebindAll(in TContext ctx)
        {
            for (var i = 0; i < _modules.Count; i++)
            {
                if (_modules[i] is IGameModuleRebind<TContext> rebind)
                {
                    rebind.RebindAll(ctx);
                }
            }
        }

        public void ForEach<TInterface>(Action<TInterface> visitor) where TInterface : class
        {
            if (visitor == null) return;
            for (var i = 0; i < _modules.Count; i++)
            {
                if (_modules[i] is TInterface typed) visitor(typed);
            }
        }

        public void ForEachReverse<TInterface>(Action<TInterface> visitor) where TInterface : class
        {
            if (visitor == null) return;
            for (var i = _modules.Count - 1; i >= 0; i--)
            {
                if (_modules[i] is TInterface typed) visitor(typed);
            }
        }

        private void FillModuleIds(List<string> ids)
        {
            for (var i = 0; i < _modules.Count; i++)
            {
                if (_modules[i] is IGameModuleId mid && !string.IsNullOrEmpty(mid.Id))
                {
                    ids.Add(mid.Id);
                }
                else
                {
                    ids.Add(_modules[i]?.GetType().Name ?? "<null>");
                }
            }
        }

        private void Fail(string message)
        {
            _fail?.Invoke(message);
        }

        private static void SortByInitialIndex(List<TModule> list, Dictionary<TModule, int> initialIndex)
        {
            list.Sort((a, b) => initialIndex[a].CompareTo(initialIndex[b]));
        }

        private sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T> where T : class
        {
            public static readonly ReferenceEqualityComparer<T> Instance = new ReferenceEqualityComparer<T>();
            public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
