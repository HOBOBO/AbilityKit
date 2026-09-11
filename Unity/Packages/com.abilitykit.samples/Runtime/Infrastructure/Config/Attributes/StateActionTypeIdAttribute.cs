#nullable enable

using System;

namespace AbilityKit.Samples.Logic.Infrastructure.Config.Attributes
{
    /// <summary>
    /// 鐘舵€佹満琛屼负绫诲瀷鏍囪瘑灞炴€?
    /// 鐢ㄤ簬鏍囪鐘舵€?Enter/Logic/Exit 鏃舵墽琛岀殑琛屼负绫诲瀷
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class StateActionTypeIdAttribute : AbilityKit.Core.Markers.MarkerAttribute
    {
        public string ActionName { get; }

        public StateActionTypeIdAttribute(string actionName)
        {
            ActionName = actionName;
        }

        public override void OnScanned(Type implType, AbilityKit.Core.Markers.IMarkerRegistry registry)
        {
            if (registry is StateActionTypeRegistry keyedRegistry)
            {
                keyedRegistry.RegisterByAttribute(this, implType);
            }
        }
    }
}
