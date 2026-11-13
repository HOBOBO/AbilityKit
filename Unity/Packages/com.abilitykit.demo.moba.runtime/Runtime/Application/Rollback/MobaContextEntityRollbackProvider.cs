using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Demo.Moba.Services;
using MemoryPack;

namespace AbilityKit.Demo.Moba.Rollback
{
    public sealed class MobaContextEntityRollbackProvider : IRollbackStateProvider, IRollbackStatePreflightProvider
    {
        public const int DefaultKey = 10011;
        private readonly MobaRuntimeContextService _contexts;

        public MobaContextEntityRollbackProvider(MobaRuntimeContextService contexts) =>
            _contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));

        public int Key => DefaultKey;

        public byte[] Export(FrameIndex frame) => MemoryPackSerializer.Serialize(
            new MobaContextEntityRollbackPayload(1, _contexts.Registry.NextEntityId,
                _contexts.Registry.GetRollbackEntityIds()));

        public void ValidateImport(FrameIndex frame, byte[] payload)
        {
            var snapshot = Read(payload);
            _contexts.Registry.ValidateRollbackEntityCursor(snapshot.NextEntityId, snapshot.EntityIds ?? Array.Empty<long>());
            foreach (var id in snapshot.EntityIds ?? Array.Empty<long>())
            {
                if (id <= 0 || id >= snapshot.NextEntityId || !_contexts.Registry.Exists(id))
                    throw new InvalidOperationException($"Confirmed context entity {id} is missing or invalid.");
            }
        }

        public void Import(FrameIndex frame, byte[] payload)
        {
            var snapshot = Read(payload);
            ValidateImport(frame, payload);
            _contexts.RestoreRollbackEntityCursor(snapshot.NextEntityId, snapshot.EntityIds ?? Array.Empty<long>());
        }

        private static MobaContextEntityRollbackPayload Read(byte[] payload)
        {
            if (payload == null || payload.Length == 0) throw new InvalidOperationException("Missing context rollback payload.");
            var snapshot = MemoryPackSerializer.Deserialize<MobaContextEntityRollbackPayload>(payload);
            if (snapshot.Version != 1 || snapshot.NextEntityId < 1)
                throw new InvalidOperationException("Unsupported context rollback payload.");
            return snapshot;
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaContextEntityRollbackPayload
    {
        [MemoryPackOrder(0)] public readonly int Version;
        [MemoryPackOrder(1)] public readonly long NextEntityId;
        [MemoryPackOrder(2)] public readonly long[] EntityIds;
        [MemoryPackConstructor]
        public MobaContextEntityRollbackPayload(int version, long nextEntityId, long[] entityIds)
        { Version = version; NextEntityId = nextEntityId; EntityIds = entityIds; }
    }
}
