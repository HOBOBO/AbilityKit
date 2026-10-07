using System;

namespace AbilityKit.Demo.Moba.Services
{
    // Detached diagnostic data. No runtime object or mutable tag container crosses into the editor.
    public sealed class MobaTagDebugSnapshot
    {
        public int SchemaVersion { get; set; } = 1;
        public string WorldId { get; set; } = string.Empty;
        public int ActorId { get; set; }
        public int Frame { get; set; }
        public long CapturedUtcTicks { get; set; }
        public string BundleVersion { get; set; } = string.Empty;
        public ulong ResultMask { get; set; }
        public string[] ResultFlags { get; set; } = Array.Empty<string>();
        public MobaTagDebugTag[] Tags { get; set; } = Array.Empty<MobaTagDebugTag>();
        public MobaTagDebugSource[] Sources { get; set; } = Array.Empty<MobaTagDebugSource>();
        public MobaTagDebugGate[] Gates { get; set; } = Array.Empty<MobaTagDebugGate>();
        public MobaTagDebugRule[] Rules { get; set; } = Array.Empty<MobaTagDebugRule>();
        public bool HasRuleDetails { get; set; }
    }

    public sealed class MobaTagDebugTag
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int ParentId { get; set; }
        public int[] SourceIndices { get; set; } = Array.Empty<int>();
    }

    public sealed class MobaTagDebugSource
    {
        public int Index { get; set; }
        public string InstanceId { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public int ConfigId { get; set; }
        public string State { get; set; } = string.Empty;
        public bool IsEffective { get; set; }
        public int SourceActorId { get; set; }
        public int TargetActorId { get; set; }
        public long SourceContextId { get; set; }
        public long RootContextId { get; set; }
        public float ElapsedSeconds { get; set; }
        public float RemainingSeconds { get; set; }
        public int Stack { get; set; }
        public int[] GrantedTagIds { get; set; } = Array.Empty<int>();
        public string[] GrantedTagNames { get; set; } = Array.Empty<string>();
        public string CurrentAction { get; set; } = string.Empty;
        public string CurrentReason { get; set; } = string.Empty;
        public string LastAction { get; set; } = string.Empty;
        public string LastReason { get; set; } = string.Empty;
        public MobaTagDebugRequirement[] Requirements { get; set; } = Array.Empty<MobaTagDebugRequirement>();
        public ulong ExecutionBlockedMask { get; set; }
    }

    public sealed class MobaTagDebugRequirement
    {
        public string Category { get; set; } = string.Empty;
        public bool Satisfied { get; set; }
        public bool RequiredSatisfied { get; set; }
        public bool BlockedSatisfied { get; set; }
        public string[] RequiredTags { get; set; } = Array.Empty<string>();
        public string[] BlockedTags { get; set; } = Array.Empty<string>();
    }

    public sealed class MobaTagDebugGate
    {
        public int Id { get; set; }
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool Blocked { get; set; }
        public string[] BlockingFlags { get; set; } = Array.Empty<string>();
    }

    public sealed class MobaTagDebugRule
    {
        public int MappingId { get; set; }
        public int GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public string ResultFlag { get; set; } = string.Empty;
        public bool Matches { get; set; }
        public MobaTagDebugRuleNode[] Nodes { get; set; } = Array.Empty<MobaTagDebugRuleNode>();
    }

    public sealed class MobaTagDebugRuleNode
    {
        public int NodeId { get; set; }
        public int Depth { get; set; }
        public string Operator { get; set; } = string.Empty;
        public int TagId { get; set; }
        public string TagName { get; set; } = string.Empty;
        public bool Exact { get; set; }
        public int RefGroupId { get; set; }
        public bool Matches { get; set; }
        public int[] SourceIndices { get; set; } = Array.Empty<int>();
    }
}
