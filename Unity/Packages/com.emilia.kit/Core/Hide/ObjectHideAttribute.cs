#if UNITY_EDITOR
using System;

namespace Emilia.Kit
{
    public class ObjectHideAttribute : Attribute
    {
        public Type objectType;

        public ObjectHideAttribute(Type objectType)
        {
            this.objectType = objectType;
        }
    }
}
#endif