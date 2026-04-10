using System;
using AbilityKit.Core.Common.Marker;

namespace AbilityKit.Core.Common.SnapshotRouting
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class SnapshotRegistryAttribute : MarkerAttribute
    {
        public string RegistryId { get; }

        public SnapshotRegistryAttribute(string registryId)
        {
            RegistryId = registryId;
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public sealed class SnapshotPipelineStageAttribute : MarkerAttribute
    {
        public string RegistryId { get; }
        public int OpCode { get; }
        public int Order { get; }
        public Type PayloadType { get; }

        public SnapshotPipelineStageAttribute(string registryId, int opCode, int order, Type payloadType)
        {
            RegistryId = registryId;
            OpCode = opCode;
            Order = order;
            PayloadType = payloadType;
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public sealed class SnapshotDecoderAttribute : MarkerAttribute
    {
        public string RegistryId { get; }
        public int OpCode { get; }
        public Type PayloadType { get; }

        public SnapshotDecoderAttribute(string registryId, int opCode, Type payloadType)
        {
            RegistryId = registryId;
            OpCode = opCode;
            PayloadType = payloadType;
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public sealed class SnapshotCmdHandlerAttribute : MarkerAttribute
    {
        public string RegistryId { get; }
        public int OpCode { get; }
        public Type PayloadType { get; }

        public SnapshotCmdHandlerAttribute(string registryId, int opCode, Type payloadType)
        {
            RegistryId = registryId;
            OpCode = opCode;
            PayloadType = payloadType;
        }
    }
}
