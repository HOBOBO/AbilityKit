using System;

namespace AbilityKit.Ability.Share.Common.SnapshotRouting
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class SnapshotRegistryAttribute : Attribute
    {
        public SnapshotRegistryAttribute(string registryId)
        {
            RegistryId = registryId;
        }

        public string RegistryId { get; }
    }
}
