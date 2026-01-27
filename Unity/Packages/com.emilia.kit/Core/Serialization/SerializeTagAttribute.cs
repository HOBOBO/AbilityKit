using System;

namespace Emilia.Kit
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class SerializeTagAttribute : Attribute
    {
        public string[] tags;

        public SerializeTagAttribute(params string[] tags)
        {
            this.tags = tags;
        }
    }
}