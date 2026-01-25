using System;

namespace AbilityKit.Ability.Share.Common.SnapshotRouting
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public sealed class SnapshotPipelineStageAttribute : Attribute
    {
        public SnapshotPipelineStageAttribute(string registryId, int opCode, int order, Type payloadType)
        {
            RegistryId = registryId;
            OpCode = opCode;
            Order = order;
            PayloadType = payloadType;
        }

        public string RegistryId { get; }
        public int OpCode { get; }
        public int Order { get; }
        public Type PayloadType { get; }
    }
}
