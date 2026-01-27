#if UNITY_EDITOR
using System;

namespace Emilia.Kit
{
    public class ObjectDescriptionAttribute : Attribute
    {
        public Type objectType;

        public ObjectDescriptionAttribute(Type objectType)
        {
            this.objectType = objectType;
        }
    }
}
#endif