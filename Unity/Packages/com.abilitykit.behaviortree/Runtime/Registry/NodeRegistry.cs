using System;
using System.Collections.Generic;
using System.Reflection;
using AbilityKit.Deterministic;

namespace AbilityKit.BehaviorTree.Registry
{
    using AbilityKit.BehaviorTree.Definition;
    using AbilityKit.BehaviorTree.Nodes;

    public sealed class NodeRegistry
    {
        private readonly Dictionary<string, NodeDescriptor> _descriptors =
            new(StringComparer.Ordinal);

        public NodeRegistry() { }

        public IEnumerable<NodeDescriptor> Descriptors => _descriptors.Values;

        public bool TryGetDescriptor(string typeId, out NodeDescriptor descriptor)
            => _descriptors.TryGetValue(typeId, out descriptor!);

        public bool Contains(string typeId) => _descriptors.ContainsKey(typeId);

        public void Register(NodeDescriptor descriptor)
        {
            ValidateDescriptor(descriptor);
            if (_descriptors.ContainsKey(descriptor.TypeId))
                throw new InvalidOperationException($"BT node type '{descriptor.TypeId}' is already registered.");

            _descriptors.Add(descriptor.TypeId, descriptor);
        }

        public void RegisterOrReplace(NodeDescriptor descriptor)
        {
            ValidateDescriptor(descriptor);
            _descriptors[descriptor.TypeId] = descriptor;
        }

        public NodeBase CreateNode(string typeId)
        {
            if (!_descriptors.TryGetValue(typeId, out var descriptor))
                throw new InvalidOperationException($"Unknown BT node type '{typeId}'.");

            var node = descriptor.Factory();
            if (node == null)
                throw new InvalidOperationException($"BT node factory for '{typeId}' returned null.");

            return node;
        }

        public int ScanAssembly(Assembly assembly)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));
            var count = 0;

            foreach (var type in assembly.GetTypes())
            {
                // 兼容扫描 legacy BtNodeTypeAttribute：直接转换单个 descriptor，避免整 registry 往返
                var legacyAttribute = type.GetCustomAttribute<AbilityKit.BehaviorTree.BtNodeTypeAttribute>();
                if (legacyAttribute != null && !type.IsAbstract)
                {
                    if (!typeof(NodeBase).IsAssignableFrom(type))
                        throw new InvalidOperationException(
                            $"BT node type '{type.FullName}' carries BtNodeType but does not derive from NodeBase.");

                    NodeDescriptor legacyDescriptor;
                    if (Activator.CreateInstance(type) is AbilityKit.BehaviorTree.BtNodeDescriptorProvider legacyProvider)
                    {
                        legacyDescriptor = NodeDescriptor.FromLegacy(legacyProvider.BuildDescriptor(legacyAttribute));
                    }
                    else
                    {
                        var (legacyMin, legacyMax) = DefaultChildCounts(legacyAttribute.Kind.ToApi());
                        legacyDescriptor = new NodeDescriptor(
                            legacyAttribute.NodeTypeId, legacyAttribute.DisplayName, legacyAttribute.Category, legacyAttribute.Kind.ToApi(),
                            legacyMin, legacyMax,
                            () => (NodeBase)Activator.CreateInstance(type)!);
                    }
                    RegisterOrReplace(legacyDescriptor);
                    count++;
                }

                var attribute = type.GetCustomAttribute<NodeTypeAttribute>();
                if (attribute == null || type.IsAbstract) continue;
                if (!typeof(NodeBase).IsAssignableFrom(type))
                    throw new InvalidOperationException(
                        $"Behavior tree node type '{type.FullName}' carries NodeType but does not derive from NodeBase.");

                NodeDescriptor descriptor;
                if (Activator.CreateInstance(type) is NodeDescriptorProvider provider)
                {
                    descriptor = provider.BuildDescriptor(attribute);
                }
                else
                {
                    var (minChildren, maxChildren) = DefaultChildCounts(attribute.Kind);
                    descriptor = new NodeDescriptor(
                        attribute.NodeTypeId, attribute.DisplayName, attribute.Category, attribute.Kind,
                        minChildren, maxChildren,
                        () => (NodeBase)Activator.CreateInstance(type)!);
                }
                RegisterOrReplace(descriptor);
                count++;
            }
            return count;
        }

        internal AbilityKit.BehaviorTree.BtNodeRegistry ToLegacy()
        {
            var registry = new AbilityKit.BehaviorTree.BtNodeRegistry();
            foreach (var descriptor in _descriptors.Values)
            {
                registry.RegisterOrReplace(descriptor.ToLegacy());
            }
            return registry;
        }

        internal void ReplaceWithLegacy(AbilityKit.BehaviorTree.BtNodeRegistry source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            _descriptors.Clear();
            foreach (var descriptor in source.Descriptors)
            {
                var canonical = NodeDescriptor.FromLegacy(descriptor);
                _descriptors[canonical.TypeId] = canonical;
            }
        }

        internal static NodeRegistry FromLegacy(AbilityKit.BehaviorTree.BtNodeRegistry source)
        {
            var registry = new NodeRegistry();
            registry.ReplaceWithLegacy(source);
            return registry;
        }

        private static void ValidateDescriptor(NodeDescriptor descriptor)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            if (string.IsNullOrEmpty(descriptor.TypeId))
                throw new ArgumentException("Descriptor TypeId must not be empty.", nameof(descriptor));
            if (descriptor.Factory == null)
                throw new ArgumentException("Descriptor Factory must not be null.", nameof(descriptor));
        }

        private static (int min, int max) DefaultChildCounts(NodeKind kind) => kind switch
        {
            NodeKind.Composite => (1, -1),
            NodeKind.Decorator => (1, 1),
            _ => (0, 0),
        };
    }
}
