using System;
using System.Collections.Generic;

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
        
        /// <summary>
        /// 自定义参数，如分隔符等
        /// </summary>
        public Dictionary<string, string> CustomParameters { get; set; } = new Dictionary<string, string>();
    }
}
