using System;

namespace AbilityKit.Game.Debug
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public sealed class EntityDebugFieldAttribute : Attribute
    {
    }
}
