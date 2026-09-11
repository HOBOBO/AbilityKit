#nullable enable

using System;

namespace AbilityKit.Samples.Logic.Infrastructure.Config.Attributes
{
    /// <summary>
    /// 琛屼负鏍戝姩浣滅被鍨嬫爣璇嗗睘鎬?
    /// 鐢ㄤ簬鏍囪瀹炵幇浜?IBTAction 鎺ュ彛鐨勫姩浣滃疄鐜扮被
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class BTActionTypeIdAttribute : AbilityKit.Core.Markers.MarkerAttribute
    {
        public string ActionName { get; }

        public BTActionTypeIdAttribute(string actionName)
        {
            ActionName = actionName;
        }

        public override void OnScanned(Type implType, AbilityKit.Core.Markers.IMarkerRegistry registry)
        {
            if (registry is BTActionTypeRegistry keyedRegistry)
            {
                keyedRegistry.RegisterByAttribute(this, implType);
            }
        }
    }
}
