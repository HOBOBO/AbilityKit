using System;
using System.Collections.Generic;
using AbilityKit.Core.Markers;

namespace AbilityKit.Battle.SearchTarget
{
    public sealed class TargetRuleRegistry : IMarkerRegistry
    {
        public static TargetRuleRegistry Instance { get; } = new TargetRuleRegistry();

        private readonly Dictionary<int, Type> _idToType = new Dictionary<int, Type>();
        private readonly Dictionary<Type, int> _typeToId = new Dictionary<Type, int>();
        private readonly Dictionary<int, Func<ITargetRule>> _factories = new Dictionary<int, Func<ITargetRule>>();
        private readonly object _syncRoot = new object();
        private bool _scanned;

        private TargetRuleRegistry() { }

        public void Scan(params System.Reflection.Assembly[] assemblies)
        {
            lock (_syncRoot)
            {
                if (_scanned) return;
                MarkerScanner<TargetRuleAttribute>.Scan(assemblies, this);
                _scanned = true;
            }
        }

        public void Register(Type implType)
        {
            if (!IsValidType(implType)) return;
            var attr = implType.GetCustomAttributes(typeof(TargetRuleAttribute), false);
            if (attr.Length == 0) return;
            RegisterByAttribute((TargetRuleAttribute)attr[0], implType);
        }

        internal void RegisterByAttribute(TargetRuleAttribute attr, Type implType)
        {
            if (attr == null || !IsValidType(implType)) return;
            lock (_syncRoot)
            {
                if (_idToType.ContainsKey(attr.Id) || _factories.ContainsKey(attr.Id) ||
                    _typeToId.ContainsKey(implType)) return;

                _idToType[attr.Id] = implType;
                _typeToId[implType] = attr.Id;
            }
        }

        private static bool IsValidType(Type implType)
        {
            return implType != null && !implType.IsAbstract && !implType.IsInterface &&
                   typeof(ITargetRule).IsAssignableFrom(implType);
        }

        public bool TryGet(int id, out Type type)
        {
            lock (_syncRoot) return _idToType.TryGetValue(id, out type);
        }

        public void RegisterFactory(int id, Func<ITargetRule> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            lock (_syncRoot)
            {
                if (!_idToType.ContainsKey(id) && !_factories.ContainsKey(id)) _factories[id] = factory;
            }
        }

        public ITargetRule Create(int id)
        {
            Func<ITargetRule> factory;
            Type type;
            lock (_syncRoot)
            {
                if (!_factories.TryGetValue(id, out factory))
                {
                    if (!_idToType.TryGetValue(id, out type) ||
                        type.GetConstructor(Type.EmptyTypes) == null) return null;
                }
                else
                {
                    type = null;
                }
            }

            return factory != null
                ? factory()
                : Activator.CreateInstance(type) as ITargetRule;
        }

        public int Count
        {
            get
            {
                lock (_syncRoot) return _idToType.Count + _factories.Count;
            }
        }
    }

    public sealed class TargetScorerRegistry : IMarkerRegistry
    {
        public static TargetScorerRegistry Instance { get; } = new TargetScorerRegistry();

        private readonly Dictionary<int, Type> _idToType = new Dictionary<int, Type>();
        private readonly Dictionary<Type, int> _typeToId = new Dictionary<Type, int>();
        private readonly Dictionary<int, Func<ITargetScorer>> _factories = new Dictionary<int, Func<ITargetScorer>>();
        private readonly object _syncRoot = new object();
        private bool _scanned;

        private TargetScorerRegistry() { }

        public void Scan(params System.Reflection.Assembly[] assemblies)
        {
            lock (_syncRoot)
            {
                if (_scanned) return;
                MarkerScanner<TargetScorerAttribute>.Scan(assemblies, this);
                _scanned = true;
            }
        }

        public void Register(Type implType)
        {
            if (!IsValidType(implType)) return;
            var attr = implType.GetCustomAttributes(typeof(TargetScorerAttribute), false);
            if (attr.Length == 0) return;
            RegisterByAttribute((TargetScorerAttribute)attr[0], implType);
        }

        internal void RegisterByAttribute(TargetScorerAttribute attr, Type implType)
        {
            if (attr == null || !IsValidType(implType)) return;
            lock (_syncRoot)
            {
                if (_idToType.ContainsKey(attr.Id) || _factories.ContainsKey(attr.Id) ||
                    _typeToId.ContainsKey(implType)) return;
                _idToType[attr.Id] = implType;
                _typeToId[implType] = attr.Id;
            }
        }

        private static bool IsValidType(Type implType)
        {
            return implType != null && !implType.IsAbstract && !implType.IsInterface &&
                   typeof(ITargetScorer).IsAssignableFrom(implType);
        }

        public bool TryGet(int id, out Type type)
        {
            lock (_syncRoot) return _idToType.TryGetValue(id, out type);
        }

        public void RegisterFactory(int id, Func<ITargetScorer> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            lock (_syncRoot)
            {
                if (!_idToType.ContainsKey(id) && !_factories.ContainsKey(id)) _factories[id] = factory;
            }
        }

        public ITargetScorer Create(int id)
        {
            Func<ITargetScorer> factory;
            Type type;
            lock (_syncRoot)
            {
                if (!_factories.TryGetValue(id, out factory))
                {
                    if (!_idToType.TryGetValue(id, out type) ||
                        type.GetConstructor(Type.EmptyTypes) == null) return null;
                }
                else
                {
                    type = null;
                }
            }

            return factory != null
                ? factory()
                : Activator.CreateInstance(type) as ITargetScorer;
        }

        public int Count
        {
            get
            {
                lock (_syncRoot) return _idToType.Count + _factories.Count;
            }
        }
    }

    public sealed class TargetSelectorRegistry : IMarkerRegistry
    {
        public static TargetSelectorRegistry Instance { get; } = new TargetSelectorRegistry();

        private readonly Dictionary<int, Type> _idToType = new Dictionary<int, Type>();
        private readonly Dictionary<Type, int> _typeToId = new Dictionary<Type, int>();
        private readonly Dictionary<int, Func<ITargetSelector>> _factories = new Dictionary<int, Func<ITargetSelector>>();
        private readonly object _syncRoot = new object();
        private bool _scanned;

        private TargetSelectorRegistry() { }

        public void Scan(params System.Reflection.Assembly[] assemblies)
        {
            lock (_syncRoot)
            {
                if (_scanned) return;
                MarkerScanner<TargetSelectorAttribute>.Scan(assemblies, this);
                _scanned = true;
            }
        }

        public void Register(Type implType)
        {
            if (!IsValidType(implType)) return;
            var attr = implType.GetCustomAttributes(typeof(TargetSelectorAttribute), false);
            if (attr.Length == 0) return;
            RegisterByAttribute((TargetSelectorAttribute)attr[0], implType);
        }

        internal void RegisterByAttribute(TargetSelectorAttribute attr, Type implType)
        {
            if (attr == null || !IsValidType(implType)) return;
            lock (_syncRoot)
            {
                if (_idToType.ContainsKey(attr.Id) || _factories.ContainsKey(attr.Id) ||
                    _typeToId.ContainsKey(implType)) return;
                _idToType[attr.Id] = implType;
                _typeToId[implType] = attr.Id;
            }
        }

        private static bool IsValidType(Type implType)
        {
            return implType != null && !implType.IsAbstract && !implType.IsInterface &&
                   typeof(ITargetSelector).IsAssignableFrom(implType);
        }

        public bool TryGet(int id, out Type type)
        {
            lock (_syncRoot) return _idToType.TryGetValue(id, out type);
        }

        public void RegisterFactory(int id, Func<ITargetSelector> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            lock (_syncRoot)
            {
                if (!_idToType.ContainsKey(id) && !_factories.ContainsKey(id)) _factories[id] = factory;
            }
        }

        public ITargetSelector Create(int id)
        {
            Func<ITargetSelector> factory;
            Type type;
            lock (_syncRoot)
            {
                if (!_factories.TryGetValue(id, out factory))
                {
                    if (!_idToType.TryGetValue(id, out type) ||
                        type.GetConstructor(Type.EmptyTypes) == null) return null;
                }
                else
                {
                    type = null;
                }
            }

            return factory != null
                ? factory()
                : Activator.CreateInstance(type) as ITargetSelector;
        }

        public int Count
        {
            get
            {
                lock (_syncRoot) return _idToType.Count + _factories.Count;
            }
        }
    }
}
