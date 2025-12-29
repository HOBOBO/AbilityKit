using System;

namespace AbilityKit.Game.EntityDebug
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public sealed class EntityDebugFieldAttribute : Attribute
    {
    }
}
