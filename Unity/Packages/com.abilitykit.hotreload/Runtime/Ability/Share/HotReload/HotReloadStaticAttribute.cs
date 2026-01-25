using System;

namespace AbilityKit.Ability.HotReload
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class HotReloadStaticAttribute : Attribute
    {
        public HotReloadStaticAttribute(string id = null)
        {
            Id = id;
        }

        public string Id { get; }
    }
}
