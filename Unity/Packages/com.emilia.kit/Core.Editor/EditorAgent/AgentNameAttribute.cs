using System;

namespace Emilia.Kit.Editor
{
    [AttributeUsage(AttributeTargets.Class)]
    public class AgentNameAttribute : Attribute
    {
        public string Name { get; private set; }

        public AgentNameAttribute(string name)
        {
            Name = name;
        }
    }
}