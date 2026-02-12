#if UNITY_EDITOR
using System;

namespace AbilityKit.Ability.Editor.Utilities
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    internal sealed class TriggerPlanExportHandlerAttribute : Attribute
    {
        public TriggerPlanExportHandlerAttribute(int order = 0)
        {
            Order = order;
        }

        public int Order { get; }
    }
}
#endif
