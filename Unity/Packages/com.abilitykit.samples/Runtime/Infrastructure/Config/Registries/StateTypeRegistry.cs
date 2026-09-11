#nullable enable

using System;
using AbilityKit.Core.Markers;
using AbilityKit.Samples.Logic.Infrastructure.Config.Attributes;

namespace AbilityKit.Samples.Logic.Infrastructure.Config
{
    /// <summary>
    /// 状态机状态类型注册表
    /// 閫氳繃 StateTypeIdAttribute 鑷姩鍙戠幇鍜屾敞鍐岀姸鎬佺被鍨?
    /// </summary>
    public sealed class StateTypeRegistry : KeyedMarkerRegistry<string, StateTypeIdAttribute>
    {
        private static readonly Lazy<StateTypeRegistry> _instance = new(() => new StateTypeRegistry());
        public static StateTypeRegistry Instance => _instance.Value;

        private StateTypeRegistry()
        {
            ScanCurrentAssembly();
        }

        private void ScanCurrentAssembly()
        {
            var assembly = typeof(StateTypeRegistry).Assembly;
            MarkerScanner<StateTypeIdAttribute>.Scan(new[] { assembly }, this);
        }

        internal void RegisterByAttribute(StateTypeIdAttribute attr, Type implType)
        {
            if (attr == null || implType == null) return;
            Register(attr.StateName, implType);
        }

        /// <summary>
        /// 鏍规嵁鍚嶇О鍒涘缓鐘舵€佸疄渚?
        /// </summary>
        public object CreateState(string stateName)
        {
            return GetOrCreateInstance(stateName);
        }

        /// <summary>
        /// 灏濊瘯鏍规嵁鍚嶇О鍒涘缓鐘舵€佸疄渚?
        /// </summary>
        public bool TryCreateState(string stateName, out object state)
        {
            if (TryGet(stateName, out var type))
            {
                state = Activator.CreateInstance(type);
                return true;
            }
            state = null;
            return false;
        }
    }
}
