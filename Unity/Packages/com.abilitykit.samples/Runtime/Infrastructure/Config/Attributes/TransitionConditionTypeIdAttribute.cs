#nullable enable

using System;

namespace AbilityKit.Samples.Logic.Infrastructure.Config.Attributes
{
    /// <summary>
    /// 鐘舵€佽浆鎹㈡潯浠剁被鍨嬫爣璇嗗睘鎬?
    /// 鐢ㄤ簬鏍囪瀹炵幇浜?ITransitionCondition 鎺ュ彛鐨勬潯浠剁被鍨?
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class TransitionConditionTypeIdAttribute : AbilityKit.Core.Markers.MarkerAttribute
    {
        public string ConditionName { get; }

        public TransitionConditionTypeIdAttribute(string conditionName)
        {
            ConditionName = conditionName;
        }

        public override void OnScanned(Type implType, AbilityKit.Core.Markers.IMarkerRegistry registry)
        {
            if (registry is TransitionConditionTypeRegistry keyedRegistry)
            {
                keyedRegistry.RegisterByAttribute(this, implType);
            }
        }
    }
}
