#nullable enable

using System;

namespace AbilityKit.Samples.Logic.Infrastructure.Config.Attributes
{
    /// <summary>
    /// 鎶€鑳介樁娈电被鍨嬫爣璇嗗睘鎬?
    /// 鐢ㄤ簬鏍囪瀹炵幇浜?ISkillPhase 鎺ュ彛鐨勯樁娈电被鍨?(PreCheck, CastTime, ApplyEffect 绛?
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class SkillPhaseTypeIdAttribute : AbilityKit.Core.Markers.MarkerAttribute
    {
        public string PhaseName { get; }

        public SkillPhaseTypeIdAttribute(string phaseName)
        {
            PhaseName = phaseName;
        }

        public override void OnScanned(Type implType, AbilityKit.Core.Markers.IMarkerRegistry registry)
        {
            if (registry is SkillPhaseTypeRegistry keyedRegistry)
            {
                keyedRegistry.RegisterByAttribute(this, implType);
            }
        }
    }
}
