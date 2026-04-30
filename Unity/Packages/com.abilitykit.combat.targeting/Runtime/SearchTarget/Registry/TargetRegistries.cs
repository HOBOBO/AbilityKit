using System;
using System.Collections.Generic;
using AbilityKit.Core.Common.Marker;

namespace AbilityKit.Battle.SearchTarget
{
    public sealed class TargetRuleRegistry : IMarkerRegistry
    {
        public static TargetRuleRegistry Instance { get; } = new TargetRuleRegistry();

        private readonly Dictionary<int, Type> _idToType = new Dictionary<int, Type>();
        private readonly Dictionary<Type, int> _typeToId = new Dictionary<Type, int>();
        private bool _scanned;

        private TargetRuleRegistry() { }

        public void Scan(params System.Reflection.Assembly[] assemblies)
        {
            if (_scanned) return;
            _scanned = true;
            MarkerScanner<TargetRuleAttribute>.Scan(assemblies, this);
        }

        public void Register(Type implType)
        {
            if (implType == null || implType.IsAbstract || implType.IsInterface) return;
            if (!typeof(ITargetRule).IsAssignableFrom(implType)) return;
            if (_typeToId.ContainsKey(implType)) return;
        }

        internal void RegisterByAttribute(TargetRuleAttribute attr, Type implType)
        {
            if (implType == null || !typeof(ITargetRule).IsAssignableFrom(implType)) return;
            if (_idToType.ContainsKey(attr.Id)) return;

            _idToType[attr.Id] = implType;
            _typeToId[implType] = attr.Id;
        }

        public bool TryGet(int id, out Type type) => _idToType.TryGetValue(id, out type);

        public ITargetRule Create(int id)
        {
            if (!_idToType.TryGetValue(id, out var type)) return null;
            return Activator.CreateInstance(type) as ITargetRule;
        }

        public int Count => _idToType.Count;
    }

    public sealed class TargetScorerRegistry : IMarkerRegistry
    {
        public static TargetScorerRegistry Instance { get; } = new TargetScorerRegistry();

        private readonly Dictionary<int, Type> _idToType = new Dictionary<int, Type>();
        private bool _scanned;

        private TargetScorerRegistry() { }

        public void Scan(params System.Reflection.Assembly[] assemblies)
        {
            if (_scanned) return;
            _scanned = true;
            MarkerScanner<TargetScorerAttribute>.Scan(assemblies, this);
        }

        public void Register(Type implType)
        {
            if (implType == null || implType.IsAbstract || implType.IsInterface) return;
            if (!typeof(ITargetScorer).IsAssignableFrom(implType)) return;
        }

        internal void RegisterByAttribute(TargetScorerAttribute attr, Type implType)
        {
            if (implType == null || !typeof(ITargetScorer).IsAssignableFrom(implType)) return;
            if (_idToType.ContainsKey(attr.Id)) return;

            _idToType[attr.Id] = implType;
        }

        public bool TryGet(int id, out Type type) => _idToType.TryGetValue(id, out type);

        public ITargetScorer Create(int id)
        {
            if (!_idToType.TryGetValue(id, out var type)) return null;
            return Activator.CreateInstance(type) as ITargetScorer;
        }

        public int Count => _idToType.Count;
    }

    public sealed class TargetSelectorRegistry : IMarkerRegistry
    {
        public static TargetSelectorRegistry Instance { get; } = new TargetSelectorRegistry();

        private readonly Dictionary<int, Type> _idToType = new Dictionary<int, Type>();
        private bool _scanned;

        private TargetSelectorRegistry() { }

        public void Scan(params System.Reflection.Assembly[] assemblies)
        {
            if (_scanned) return;
            _scanned = true;
            MarkerScanner<TargetSelectorAttribute>.Scan(assemblies, this);
        }

        public void Register(Type implType)
        {
            if (implType == null || implType.IsAbstract || implType.IsInterface) return;
            if (!typeof(ITargetSelector).IsAssignableFrom(implType)) return;
        }

        internal void RegisterByAttribute(TargetSelectorAttribute attr, Type implType)
        {
            if (implType == null || !typeof(ITargetSelector).IsAssignableFrom(implType)) return;
            if (_idToType.ContainsKey(attr.Id)) return;

            _idToType[attr.Id] = implType;
        }

        public bool TryGet(int id, out Type type) => _idToType.TryGetValue(id, out type);

        public ITargetSelector Create(int id)
        {
            if (!_idToType.TryGetValue(id, out var type)) return null;
            return Activator.CreateInstance(type) as ITargetSelector;
        }

        public int Count => _idToType.Count;
    }
}
