using System;
using System.Collections.Generic;
using UnityEngine;

namespace AbilityKit.ProtocolEditor.Schema
{
    [CreateAssetMenu(menuName = "AbilityKit/Protocol/Protocol Definition", fileName = "ProtocolDefinition")]
    public sealed class ProtocolDefinition : ScriptableObject
    {
        public string RegistryId;
        public string Domain;

        public List<MessageDefinition> Messages = new();

        public enum ChannelKind
        {
            SnapshotDecoder = 0,
            SnapshotCmdHandler = 1,
            SnapshotPipelineStage = 2,
        }

        public enum CodecBackend
        {
            CustomBinary = 0,
            Protobuf = 1,
            Json = 2,
        }

        [Serializable]
        public sealed class MessageDefinition
        {
            public string Name;
            public int OpCode;
            public ChannelKind Channel;
            public string PayloadTypeName;
            public int PipelineOrder;
            public CodecBackend Backend;
        }
    }
}
