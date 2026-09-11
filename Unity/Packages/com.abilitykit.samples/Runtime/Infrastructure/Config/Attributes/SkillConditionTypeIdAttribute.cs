#nullable enable

using System;

namespace AbilityKit.Samples.Logic.Infrastructure.Config.Attributes
{
    /// <summary>
    /// 鎶€鑳芥潯浠剁被鍨嬫爣璇嗗睘鎬?
    /// 鐢ㄤ簬鏍囪瀹炵幇浜?ISkillCondition 鎺ュ彛鐨勬潯浠剁被鍨?(HasEnoughMana, TargetInRange 绛?
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class SkillConditionTypeIdAttribute : AbilityKit.Core.Markers.MarkerAttribute
    {
        public string ConditionName { get; }

        public SkillConditionTypeIdAttribute(string conditionName)
        {
            ConditionName = conditionName;
        }

        public override void OnScanned(Type implType, AbilityKit.Core.Markers.IMarkerRegistry registry)
        {
            if (registry is SkillConditionTypeRegistry keyedRegistry)
            {
                keyedRegistry.RegisterByAttribute(this, implType);
            }
        }
    }
}
