using System;
using System.Collections.Generic;
using System.Reflection;
using AbilityKit.BehaviorTree.Nodes;

namespace AbilityKit.BehaviorTree
{
    /// <summary>
    /// 节点注册中心：类id -> 描述符。运行时按描述符工厂实例化节点；
    /// 编辑器从 <see cref="Descriptors"/> 拉取目录构建菜单与属性面板（编辑器主动获取，
    /// 不要求节点继承任何编辑器可见类型）。未知类id 在加载校验时即失败，无反射回退    /// </summary>
    [System.Obsolete("Use the unprefixed BehaviorTree API in segmented namespaces.", false)]
    public sealed class BtNodeRegistry
    {
        private readonly Dictionary<string, BtNodeDescriptor> _descriptors =
            new(StringComparer.Ordinal);

        public IEnumerable<BtNodeDescriptor> Descriptors => _descriptors.Values;

        public bool TryGetDescriptor(string typeId, out BtNodeDescriptor descriptor)
            => _descriptors.TryGetValue(typeId, out descriptor!);

        public bool Contains(string typeId) => _descriptors.ContainsKey(typeId);

        public void Register(BtNodeDescriptor descriptor)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            if (string.IsNullOrEmpty(descriptor.TypeId))
                throw new ArgumentException("Descriptor TypeId must not be empty.", nameof(descriptor));
            if (descriptor.Factory == null)
                throw new ArgumentException("Descriptor Factory must not be null.", nameof(descriptor));

            if (_descriptors.ContainsKey(descriptor.TypeId))
                throw new InvalidOperationException($"BT node type '{descriptor.TypeId}' is already registered.");

            _descriptors.Add(descriptor.TypeId, descriptor);
        }

        /// <summary>注册或替换（覆盖语义用于测试与热重装场景；正式装配路径应使用 <see cref="Register"/>）</summary>
        public void RegisterOrReplace(BtNodeDescriptor descriptor)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
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

        /// <summary>扫描程序集中<see cref="BtNodeTypeAttribute"/> 的节点类并注册</summary>
        public int ScanAssembly(Assembly assembly)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));

            var count = 0;
            foreach (var type in assembly.GetTypes())
            {
                var attribute = type.GetCustomAttribute<BtNodeTypeAttribute>();
                if (attribute == null || type.IsAbstract) continue;
                if (!typeof(NodeBase).IsAssignableFrom(type))
                    throw new InvalidOperationException(
                        $"BT node type '{type.FullName}' carries BtNodeType but does not derive from NodeBase.");

                BtNodeDescriptor descriptor;
                if (Activator.CreateInstance(type) is BtNodeDescriptorProvider provider)
                {
                    descriptor = provider.BuildDescriptor(attribute);
                }
                else
                {
                    var (minChildren, maxChildren) = DefaultChildCounts(attribute.Kind);
                    descriptor = new BtNodeDescriptor(
                        attribute.NodeTypeId, attribute.DisplayName, attribute.Category, attribute.Kind,
                        minChildren, maxChildren,
                        () => (NodeBase)Activator.CreateInstance(type)!);
                }

                RegisterOrReplace(descriptor);
                count++;
            }

            return count;
        }

        private static (int min, int max) DefaultChildCounts(BtNodeKind kind) => kind switch
        {
            BtNodeKind.Composite => (1, -1),
            BtNodeKind.Decorator => (1, 1),
            _ => (0, 0),
        };
    }
}
