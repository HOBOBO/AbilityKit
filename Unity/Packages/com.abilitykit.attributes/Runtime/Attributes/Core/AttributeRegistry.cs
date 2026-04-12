using System;
using System.Collections.Generic;
using AbilityKit.Attributes.Formula;
using AbilityKit.Attributes.Constraint;

namespace AbilityKit.Attributes.Core
{
    /// <summary>
    /// 属性注册表。
    /// 核心实现，支持 IAttributeRegistry 接口。
    /// 
    /// 重构说明：
    /// - 实现 IAttributeRegistry 接口
    /// - 提供静态 DefaultRegistry 属性用于向后兼容
    /// - 支持属性依赖关系管理
    /// </summary>
    public sealed class AttributeRegistry : IAttributeRegistry
    {
        private sealed class Node
        {
            public string Name;
            public AttributeDef Def;
        }

        private readonly Dictionary<string, int> _byName = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<Node> _nodes = new List<Node>(128);

        private readonly Dictionary<int, List<AttributeId>> _dependents = new Dictionary<int, List<AttributeId>>(128);

        private bool _frozen;

        /// <summary>
        /// 默认注册表实例（向后兼容）
        /// </summary>
        public static AttributeRegistry DefaultRegistry { get; } = new AttributeRegistry();

        AttributeRegistry()  // 私有构造函数，使用 DefaultRegistry
        {
            _nodes.Add(new Node { Name = string.Empty, Def = null });
            _byName[string.Empty] = 0;
        }

        bool IAttributeRegistry.IsFrozen => _frozen;

        public void Freeze()
        {
            BuildDependencyGraphOrThrow();
            _frozen = true;
        }

        public IReadOnlyList<AttributeId> GetDependents(AttributeId id)
        {
            if (!id.IsValid) return null;
            if (_dependents.TryGetValue(id.Id, out var list)) return list;
            return null;
        }

        public IEnumerable<AttributeId> GetDependencies(AttributeId id)
        {
            var def = GetDef(id);
            if (def?.DependsOn != null && def.DependsOn.Length > 0)
            {
                foreach (var dep in def.DependsOn)
                {
                    yield return dep;
                }
            }
            if (def?.Formula is IAttributeDependencyProvider p)
            {
                foreach (var dep in p.GetDependencies())
                {
                    yield return dep;
                }
            }
        }

        private void BuildDependencyGraphOrThrow()
        {
            _dependents.Clear();

            var depsById = new Dictionary<int, List<AttributeId>>(_nodes.Count);
            for (int i = 1; i < _nodes.Count; i++)
            {
                var def = _nodes[i].Def;
                if (def == null) continue;

                var deps = CollectDependencies(def);
                if (deps == null || deps.Count == 0) continue;

                for (int j = deps.Count - 1; j >= 0; j--)
                {
                    var d = deps[j];
                    if (!d.IsValid)
                    {
                        deps.RemoveAt(j);
                        continue;
                    }
                    if (d.Id == i)
                    {
                        throw new InvalidOperationException($"Attribute depends on itself: {def.Name}");
                    }
                }

                if (deps.Count > 0)
                {
                    depsById[i] = deps;
                }
            }

            var visiting = new bool[_nodes.Count];
            var visited = new bool[_nodes.Count];

            for (int i = 1; i < _nodes.Count; i++)
            {
                if (visited[i]) continue;
                if (depsById.ContainsKey(i))
                {
                    DfsDetectCycle(i, depsById, visiting, visited);
                }
                else
                {
                    visited[i] = true;
                }
            }

            foreach (var kv in depsById)
            {
                var to = new AttributeId(kv.Key, _nodes[kv.Key].Name);
                var fromList = kv.Value;
                for (int j = 0; j < fromList.Count; j++)
                {
                    var from = fromList[j];
                    if (!from.IsValid) continue;
                    if (!_dependents.TryGetValue(from.Id, out var list))
                    {
                        list = new List<AttributeId>(4);
                        _dependents[from.Id] = list;
                    }
                    list.Add(to);
                }
            }
        }

        private static void DfsDetectCycle(int id, Dictionary<int, List<AttributeId>> depsById, bool[] visiting, bool[] visited)
        {
            if (visited[id]) return;
            if (visiting[id])
            {
                throw new InvalidOperationException($"Attribute dependency cycle detected at id={id}");
            }

            visiting[id] = true;
            if (depsById.TryGetValue(id, out var deps))
            {
                for (int i = 0; i < deps.Count; i++)
                {
                    var d = deps[i];
                    if (!d.IsValid) continue;
                    DfsDetectCycle(d.Id, depsById, visiting, visited);
                }
            }
            visiting[id] = false;
            visited[id] = true;
        }

        private static List<AttributeId> CollectDependencies(AttributeDef def)
        {
            if (def == null) return null;

            List<AttributeId> deps = null;

            if (def.DependsOn != null && def.DependsOn.Length > 0)
            {
                deps = new List<AttributeId>(def.DependsOn.Length);
                for (int i = 0; i < def.DependsOn.Length; i++)
                {
                    deps.Add(def.DependsOn[i]);
                }
            }

            if (def.Formula is IAttributeDependencyProvider p)
            {
                var fromFormula = p.GetDependencies();
                if (fromFormula != null)
                {
                    deps ??= new List<AttributeId>(4);
                    foreach (var d in fromFormula)
                    {
                        deps.Add(d);
                    }
                }
            }

            return deps;
        }

        public AttributeId Register(AttributeDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (string.IsNullOrEmpty(def.Name)) throw new ArgumentException("AttributeDef.Name is required", nameof(def));

            if (_frozen)
            {
                throw new InvalidOperationException($"AttributeRegistry is frozen. Register is not allowed at runtime. name={def.Name}");
            }

            if (_byName.TryGetValue(def.Name, out var existing) && existing != 0)
            {
                def.Id = new AttributeId(existing, def.Name);
                _nodes[existing].Def = def;
                return def.Id;
            }

            var id = _nodes.Count;
            def.Id = new AttributeId(id, def.Name);
            _nodes.Add(new Node { Name = def.Name, Def = def });
            _byName[def.Name] = id;
            return def.Id;
        }

        public AttributeId Request(string name)
        {
            return Request(name, out _);
        }

        public AttributeId Request(string name, out bool created)
        {
            created = false;
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));

            if (_byName.TryGetValue(name, out var id) && id != 0)
            {
                return new AttributeId(id, name);
            }

            if (_frozen)
            {
                throw new InvalidOperationException($"AttributeRegistry is frozen. Attribute is not registered: {name}");
            }

            created = true;
            var def = new AttributeDef(name);
            return Register(def);
        }

        public bool TryGet(string name, out AttributeId id)
        {
            id = default;
            if (string.IsNullOrEmpty(name)) return false;
            if (_byName.TryGetValue(name, out var v) && v != 0)
            {
                id = new AttributeId(v, name);
                return true;
            }
            return false;
        }

        public string GetName(AttributeId id)
        {
            if (!id.IsValid) return string.Empty;
            if (id.Id <= 0 || id.Id >= _nodes.Count) return string.Empty;
            return _nodes[id.Id].Name ?? string.Empty;
        }

        public AttributeDef GetDef(AttributeId id)
        {
            if (!id.IsValid) return null;
            if (id.Id <= 0 || id.Id >= _nodes.Count) return null;
            return _nodes[id.Id].Def;
        }

        public IAttributeFormula GetFormula(AttributeId id)
        {
            var def = GetDef(id);
            return def?.Formula ?? DefaultAttributeFormula.Instance;
        }

        public IAttributeConstraint GetConstraint(AttributeId id)
        {
            var def = GetDef(id);
            return def?.Constraint;
        }

        public float GetDefaultBaseValue(AttributeId id)
        {
            var def = GetDef(id);
            return def != null ? def.DefaultBaseValue : 0f;
        }

        public string GetGroup(AttributeId id)
        {
            var def = GetDef(id);
            return def?.Group;
        }
    }
}
