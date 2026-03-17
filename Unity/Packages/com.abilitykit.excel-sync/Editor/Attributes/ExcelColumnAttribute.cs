using System;

namespace AbilityKit.ExcelSync.Editor
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class ExcelColumnAttribute : Attribute
    {
        public ExcelColumnAttribute(string name)
        {
            Name = name;
        }

        public string Name { get; }
        public int Order { get; set; } = int.MaxValue;
        public bool Ignore { get; set; } = false;
    }
}
