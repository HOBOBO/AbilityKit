using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Attributes.Core;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.StateSync;
using AbilityKit.Deterministic;
using MemoryPack;

namespace AbilityKit.Demo.Moba.Rollback
{
    public sealed class MobaActorResourceRollbackProvider : IRollbackStateProvider, IMobaStateRecoveryProvider
    {
        public const int DefaultKey = 10013;
        private readonly MobaActorRegistry _actors;

        public MobaActorResourceRollbackProvider(MobaActorRegistry actors) => _actors = actors ?? throw new ArgumentNullException(nameof(actors));
        public int Key => DefaultKey;
        public string Name => "ActorResources";
        public byte[] Export(FrameIndex frame) => ExportState(frame);
        public void Import(FrameIndex frame, byte[] payload) => ImportState(frame, payload);

        public byte[] ExportState(FrameIndex frame)
        {
            var entries = new List<MobaActorResourceRollbackEntry>();
            foreach (var pair in _actors.Entries)
            {
                var map = pair.Value != null && pair.Value.hasResourceContainer ? pair.Value.resourceContainer.Value?.Map : null;
                if (map == null) continue;
                foreach (var resource in map)
                {
                    if (resource.Value == null) continue;
                    entries.Add(new MobaActorResourceRollbackEntry(
                        pair.Key, (int)resource.Key, resource.Value.Current.RawValue,
                        resource.Value.LastMax.RawValue, resource.Value.MaxAttribute.GetHashCode()));
                }
            }
            entries.Sort((left, right) => left.ActorId != right.ActorId
                ? left.ActorId.CompareTo(right.ActorId)
                : left.ResourceType.CompareTo(right.ResourceType));
            return MemoryPackSerializer.Serialize(new MobaActorResourceRollbackPayload(1, entries.ToArray()));
        }

        public void ImportState(FrameIndex frame, byte[] payload)
        {
            if (payload == null || payload.Length == 0) return;
            var state = MemoryPackSerializer.Deserialize<MobaActorResourceRollbackPayload>(payload);
            if (state.Version != 1) throw new InvalidOperationException($"Unsupported actor resource rollback payload version '{state.Version}'.");
            var grouped = new Dictionary<int, List<MobaActorResourceRollbackEntry>>();
            var entries = state.Entries ?? Array.Empty<MobaActorResourceRollbackEntry>();
            for (var i = 0; i < entries.Length; i++)
            {
                if (!grouped.TryGetValue(entries[i].ActorId, out var list)) grouped.Add(entries[i].ActorId, list = new List<MobaActorResourceRollbackEntry>());
                list.Add(entries[i]);
            }
            foreach (var pair in grouped)
            {
                if (!_actors.TryGetRegistered(pair.Key, out var actor) || actor == null || !actor.hasResourceContainer || actor.resourceContainer.Value == null) continue;
                var map = actor.resourceContainer.Value.Map ??= new Dictionary<ResourceType, ResourceState>();
                map.Clear();
                for (var i = 0; i < pair.Value.Count; i++)
                {
                    var entry = pair.Value[i];
                    map[(ResourceType)entry.ResourceType] = new ResourceState
                    {
                        Current = Fixed64.FromRaw(entry.CurrentRaw),
                        LastMax = Fixed64.FromRaw(entry.LastMaxRaw),
                        MaxAttribute = AttributeId.FromRaw(entry.MaxAttributeId),
                    };
                }
            }
        }

        public void AddStateHash(FrameIndex frame, ref MobaStateHashBuilder hash)
        {
            var payload = ExportState(frame);
            hash.AddInt(Key); hash.AddInt(payload.Length);
            for (var i = 0; i < payload.Length; i++) hash.AddByte(payload[i]);
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaActorResourceRollbackPayload
    {
        [MemoryPackOrder(0)] public readonly int Version;
        [MemoryPackOrder(1)] public readonly MobaActorResourceRollbackEntry[] Entries;
        [MemoryPackConstructor] public MobaActorResourceRollbackPayload(int version, MobaActorResourceRollbackEntry[] entries)
        { Version = version; Entries = entries ?? Array.Empty<MobaActorResourceRollbackEntry>(); }
    }

    [MemoryPackable]
    public readonly partial struct MobaActorResourceRollbackEntry
    {
        [MemoryPackOrder(0)] public readonly int ActorId; [MemoryPackOrder(1)] public readonly int ResourceType;
        [MemoryPackOrder(2)] public readonly long CurrentRaw; [MemoryPackOrder(3)] public readonly long LastMaxRaw;
        [MemoryPackOrder(4)] public readonly int MaxAttributeId;
        public MobaActorResourceRollbackEntry(int actorId, int resourceType, long currentRaw, long lastMaxRaw, int maxAttributeId)
        { ActorId=actorId;ResourceType=resourceType;CurrentRaw=currentRaw;LastMaxRaw=lastMaxRaw;MaxAttributeId=maxAttributeId; }
    }
}
