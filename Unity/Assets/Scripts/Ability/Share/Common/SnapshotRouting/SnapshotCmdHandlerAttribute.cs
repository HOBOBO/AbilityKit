using System;

namespace AbilityKit.Ability.Share.Common.SnapshotRouting
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public sealed class SnapshotCmdHandlerAttribute : Attribute
    {
        public SnapshotCmdHandlerAttribute(string registryId, int opCode, Type payloadType)
        {
            RegistryId = registryId;
            OpCode = opCode;
            PayloadType = payloadType;
        }

        public string RegistryId { get; }
        public int OpCode { get; }
        public Type PayloadType { get; }
    }
}
