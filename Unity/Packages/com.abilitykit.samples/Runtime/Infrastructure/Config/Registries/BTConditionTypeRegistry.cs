#nullable enable

using System;
using AbilityKit.Core.Markers;
using AbilityKit.Samples.Logic.Infrastructure.Config.Attributes;

namespace AbilityKit.Samples.Logic.Infrastructure.Config
{
    /// <summary>
    /// 行为树条件类型注册表
    /// 閫氳繃 BTConditionTypeIdAttribute 鑷姩鍙戠幇鍜屾敞鍐屾潯浠跺疄鐜扮被鍨?(HasTargetInRange, NoTarget 绛?
    /// </summary>
    public sealed class BTConditionTypeRegistry : KeyedMarkerRegistry<string, BTConditionTypeIdAttribute>
    {
        private static readonly Lazy<BTConditionTypeRegistry> _instance = new(() => new BTConditionTypeRegistry());
        public static BTConditionTypeRegistry Instance => _instance.Value;

        private BTConditionTypeRegistry()
        {
            ScanCurrentAssembly();
        }

        private void ScanCurrentAssembly()
        {
            var assembly = typeof(BTConditionTypeRegistry).Assembly;
            MarkerScanner<BTConditionTypeIdAttribute>.Scan(new[] { assembly }, this);
        }

        internal void RegisterByAttribute(BTConditionTypeIdAttribute attr, Type implType)
        {
            if (attr == null || implType == null) return;
            Register(attr.ConditionName, implType);
        }

        /// <summary>
        /// 根据条件名称创建条件实例
        /// </summary>
        public object CreateCondition(string conditionName)
        {
            return GetOrCreateInstance(conditionName);
        }
    }
}
