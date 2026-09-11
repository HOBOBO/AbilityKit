#nullable enable

using System;
using AbilityKit.Core.Markers;
using AbilityKit.Samples.Logic.Infrastructure.Config.Attributes;

namespace AbilityKit.Samples.Logic.Infrastructure.Config
{
    /// <summary>
    /// 鐘舵€佹満琛屼负绫诲瀷娉ㄥ唽琛?
    /// 閫氳繃 StateActionTypeIdAttribute 鑷姩鍙戠幇鍜屾敞鍐岀姸鎬佽涓虹被鍨?
    /// </summary>
    public sealed class StateActionTypeRegistry : KeyedMarkerRegistry<string, StateActionTypeIdAttribute>
    {
        private static readonly Lazy<StateActionTypeRegistry> _instance = new(() => new StateActionTypeRegistry());
        public static StateActionTypeRegistry Instance => _instance.Value;

        private StateActionTypeRegistry()
        {
            ScanCurrentAssembly();
        }

        private void ScanCurrentAssembly()
        {
            var assembly = typeof(StateActionTypeRegistry).Assembly;
            MarkerScanner<StateActionTypeIdAttribute>.Scan(new[] { assembly }, this);
        }

        internal void RegisterByAttribute(StateActionTypeIdAttribute attr, Type implType)
        {
            if (attr == null || implType == null) return;
            Register(attr.ActionName, implType);
        }

        /// <summary>
        /// 根据名称创建行为实例
        /// </summary>
        public object CreateAction(string actionName)
        {
            return GetOrCreateInstance(actionName);
        }
    }
}
