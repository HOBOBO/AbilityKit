using System;
using AbilityKit.Core.Common.Marker;

namespace AbilityKit.Battle.SearchTarget
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class TargetRuleAttribute : MarkerAttribute
    {
        public readonly int Id;
        public readonly string Name;
        public readonly int Order;

        public TargetRuleAttribute(int id, string name = null, int order = 0)
        {
            Id = id;
            Name = name ?? string.Empty;
            Order = order;
        }

        public override void OnScanned(Type implType, Core.Common.Marker.IMarkerRegistry registry)
        {
            if (registry is TargetRuleRegistry ruleRegistry)
            {
                ruleRegistry.RegisterByAttribute(this, implType);
            }
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class TargetScorerAttribute : MarkerAttribute
    {
        public readonly int Id;
        public readonly string Name;

        public TargetScorerAttribute(int id, string name = null)
        {
            Id = id;
            Name = name ?? string.Empty;
        }

        public override void OnScanned(Type implType, Core.Common.Marker.IMarkerRegistry registry)
        {
            if (registry is TargetScorerRegistry scorerRegistry)
            {
                scorerRegistry.RegisterByAttribute(this, implType);
            }
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class TargetSelectorAttribute : MarkerAttribute
    {
        public readonly int Id;
        public readonly string Name;

        public TargetSelectorAttribute(int id, string name = null)
        {
            Id = id;
            Name = name ?? string.Empty;
        }

        public override void OnScanned(Type implType, Core.Common.Marker.IMarkerRegistry registry)
        {
            if (registry is TargetSelectorRegistry selectorRegistry)
            {
                selectorRegistry.RegisterByAttribute(this, implType);
            }
        }
    }
}
