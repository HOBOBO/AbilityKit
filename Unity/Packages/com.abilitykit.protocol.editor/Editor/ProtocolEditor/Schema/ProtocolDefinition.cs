using System;
using System.Collections.Generic;
using UnityEngine;

namespace AbilityKit.ProtocolEditor.Schema
{
    /// <summary>
    /// 【已冻结 / superseded】旧 ScriptableObject 协议定义双轨入口。
    /// 唯一正式入口是 YAML Protocol Workspace（Tools/AbilityKit/Framework/Protocol/Protocol Workspace）。
    /// 本类型自 2026-08 起只读保留：仅供既有 .asset 反序列化与
    /// LegacyProtocolDefinitionMigrationWindow 的一次性迁移读取；
    /// CreateAssetMenu 已移除，配套代码生成器（MemoryPack DTO / OpCodes / 路由胶水）已删除。
    /// </summary>
    [Obsolete("Superseded by the YAML Protocol Workspace. Read-only for one-time migration via LegacyProtocolDefinitionMigrationWindow; new assets can no longer be created.")]
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
