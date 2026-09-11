#nullable enable

using System;

namespace AbilityKit.Samples.Logic.Infrastructure.Config.Attributes
{
    /// <summary>
    /// 鐘舵€佹満鐘舵€佺被鍨嬫爣璇嗗睘鎬?
    /// 鐢ㄤ簬鏍囪瀹炵幇浜?IState 鎺ュ彛鐨勭姸鎬佺被鍨?
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class StateTypeIdAttribute : AbilityKit.Core.Markers.MarkerAttribute
    {
        public string StateName { get; }

        public StateTypeIdAttribute(string stateName)
        {
            StateName = stateName;
        }

        public override void OnScanned(Type implType, AbilityKit.Core.Markers.IMarkerRegistry registry)
        {
            if (registry is StateTypeRegistry keyedRegistry)
            {
                keyedRegistry.RegisterByAttribute(this, implType);
            }
        }
    }
}
