using System;

namespace AbilityKit.Triggering.Runtime
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class TriggerConditionTypeAttribute : Attribute
    {
        public TriggerConditionTypeAttribute(string type)
        {
            Type = type;
            DisplayName = type;
            Category = string.Empty;
            Order = 0;
        }

        public TriggerConditionTypeAttribute(string type, string displayName, string category = "", int order = 0)
        {
            Type = type;
            DisplayName = string.IsNullOrEmpty(displayName) ? type : displayName;
            Category = category ?? string.Empty;
            Order = order;
        }

        public string Type { get; }
        public string DisplayName { get; }
        public string Category { get; }
        public int Order { get; }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class TriggerActionTypeAttribute : Attribute
    {
        public TriggerActionTypeAttribute(string type)
        {
            Type = type;
            DisplayName = type;
            Category = string.Empty;
            Order = 0;
        }

        public TriggerActionTypeAttribute(string type, string displayName, string category = "", int order = 0)
        {
            Type = type;
            DisplayName = string.IsNullOrEmpty(displayName) ? type : displayName;
            Category = category ?? string.Empty;
            Order = order;
        }

        public string Type { get; }
        public string DisplayName { get; }
        public string Category { get; }
        public int Order { get; }
    }
}
