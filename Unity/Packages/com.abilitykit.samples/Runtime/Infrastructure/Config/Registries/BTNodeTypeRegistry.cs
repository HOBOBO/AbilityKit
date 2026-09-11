#nullable enable

using System;
using AbilityKit.Core.Markers;
using AbilityKit.Samples.Logic.Infrastructure.Config.Attributes;

namespace AbilityKit.Samples.Logic.Infrastructure.Config
{
    /// <summary>
    /// 行为树节点类型注册表
    /// 通过 BTNodeTypeIdAttribute 自动发现和注册行为树节点类型 (Selector, Sequence, Condition, Action)
    /// </summary>
    public sealed class BTNodeTypeRegistry : KeyedMarkerRegistry<string, BTNodeTypeIdAttribute>
    {
        private static readonly Lazy<BTNodeTypeRegistry> _instance = new(() => new BTNodeTypeRegistry());
        public static BTNodeTypeRegistry Instance => _instance.Value;

        private BTNodeTypeRegistry()
        {
            ScanCurrentAssembly();
        }

        private void ScanCurrentAssembly()
        {
            var assembly = typeof(BTNodeTypeRegistry).Assembly;
            MarkerScanner<BTNodeTypeIdAttribute>.Scan(new[] { assembly }, this);
        }

        internal void RegisterByAttribute(BTNodeTypeIdAttribute attr, Type implType)
        {
            if (attr == null || implType == null) return;
            Register(attr.NodeType, implType);
        }

        /// <summary>
        /// 根据节点类型名称创建节点实例
        /// </summary>
        public object CreateNode(string nodeType)
        {
            return GetOrCreateInstance(nodeType);
        }
    }
}
