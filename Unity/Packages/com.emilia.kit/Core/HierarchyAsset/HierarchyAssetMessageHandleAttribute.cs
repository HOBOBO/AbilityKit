using System;

namespace Emilia.Kit
{
    [AttributeUsage(AttributeTargets.Class)]
    public class HierarchyAssetMessageHandleAttribute : Attribute
    {
        public Type targetType;
        public string message;

        public HierarchyAssetMessageHandleAttribute(Type targetType, string message)
        {
            this.targetType = targetType;
            this.message = message;
        }
    }
}