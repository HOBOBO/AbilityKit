using System;

namespace Emilia.Kit
{
    [AttributeUsage(AttributeTargets.Class)]
    public class EditorHandleAttribute : Attribute
    {
        public Type targetType;

        public EditorHandleAttribute(Type targetType)
        {
            this.targetType = targetType;
        }
    }
}