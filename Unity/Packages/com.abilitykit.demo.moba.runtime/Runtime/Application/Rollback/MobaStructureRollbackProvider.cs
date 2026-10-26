using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Demo.Moba.Gameplay;
using AbilityKit.Demo.Moba.Services.StateSync;
using MemoryPack;

namespace AbilityKit.Demo.Moba.Rollback
{
    [MobaRollbackProvider(DefaultKey)]
    public sealed class MobaStructureRollbackProvider : IRollbackStateProvider, IRollbackStatePreflightProvider,
        IMobaStateRecoveryProvider
    {
        public const int DefaultKey = 10043;
        private readonly MobaStructureService _structures;

        public MobaStructureRollbackProvider(MobaStructureService structures)
        {
            _structures = structures ?? throw new ArgumentNullException(nameof(structures));
        }

        public int Key => DefaultKey;
        public string Name => "Structures";
        public byte[] Export(FrameIndex frame) => ExportState(frame);
        public void Import(FrameIndex frame, byte[] payload) => ImportState(frame, payload);

        public byte[] ExportState(FrameIndex frame)
        {
            var cooldowns = new List<MobaStructureAttackCooldownState>();
            _structures.CopyAttackCooldowns(cooldowns);
            var entries = new MobaStructureCooldownEntry[cooldowns.Count];
            for (var i = 0; i < entries.Length; i++)
                entries[i] = new MobaStructureCooldownEntry(cooldowns[i].ActorId, cooldowns[i].NextAttackAtMs);
            return MemoryPackSerializer.Serialize(new MobaStructureRollbackPayload(1, entries));
        }

        public void ValidateImport(FrameIndex frame, byte[] payload) => Read(payload);

        public void ImportState(FrameIndex frame, byte[] payload)
        {
            var state = Read(payload);
            var cooldowns = new MobaStructureAttackCooldownState[state.Cooldowns.Length];
            for (var i = 0; i < cooldowns.Length; i++)
                cooldowns[i] = new MobaStructureAttackCooldownState(
                    state.Cooldowns[i].ActorId, state.Cooldowns[i].NextAttackAtMs);
            _structures.RestoreAttackCooldowns(cooldowns);
        }

        public void AddStateHash(FrameIndex frame, ref MobaStateHashBuilder hash)
        {
            var payload = ExportState(frame);
            hash.AddInt(Key);
            hash.AddInt(payload.Length);
            for (var i = 0; i < payload.Length; i++) hash.AddByte(payload[i]);
        }

        private static MobaStructureRollbackPayload Read(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
                throw new InvalidOperationException("Missing structure rollback payload.");
            var state = MemoryPackSerializer.Deserialize<MobaStructureRollbackPayload>(payload);
            if (state.Version != 1 || state.Cooldowns == null)
                throw new InvalidOperationException("Invalid structure rollback payload.");
            for (var i = 0; i < state.Cooldowns.Length; i++)
            {
                var item = state.Cooldowns[i];
                if (item.ActorId <= 0 || item.NextAttackAtMs < 0 ||
                    (i > 0 && state.Cooldowns[i - 1].ActorId >= item.ActorId))
                    throw new InvalidOperationException("Invalid structure rollback cooldown entry.");
            }
            return state;
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaStructureRollbackPayload
    {
        [MemoryPackOrder(0)] public readonly int Version;
        [MemoryPackOrder(1)] public readonly MobaStructureCooldownEntry[] Cooldowns;

        [MemoryPackConstructor]
        public MobaStructureRollbackPayload(int version, MobaStructureCooldownEntry[] cooldowns)
        { Version = version; Cooldowns = cooldowns; }
    }

    [MemoryPackable]
    public readonly partial struct MobaStructureCooldownEntry
    {
        [MemoryPackOrder(0)] public readonly int ActorId;
        [MemoryPackOrder(1)] public readonly long NextAttackAtMs;
        public MobaStructureCooldownEntry(int actorId, long nextAttackAtMs)
        { ActorId = actorId; NextAttackAtMs = nextAttackAtMs; }
    }
}
