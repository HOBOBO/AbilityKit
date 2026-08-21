#nullable enable

using System;
using AbilityKit.Protocol.Catalog;
using MemoryPack;

namespace AbilityKit.Protocol.Shooter
{
    /// <summary>Registers Shooter battle decoders, including compatibility-aware snapshot codecs.</summary>
    public static class ShooterProtocolDecoderModule
    {
        public const string CatalogId = "abilitykit.shooter.battle";

        public static void Register(ProtocolPayloadDecoderRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            RegisterMemoryPack<ShooterInputPayload>(registry, "player-command.event");
            RegisterMemoryPack<ShooterStartGamePayload>(registry, "start-game.push");
            registry.TryRegister(CatalogId, "state.push", payload =>
                ShooterStateSnapshotCodec.Deserialize(Copy(payload)));
            RegisterMemoryPack<ShooterEventSnapshot[]>(registry, "events.push");
            registry.TryRegister(CatalogId, "packed-state.push", payload =>
                ShooterPackedSnapshotCodec.Deserialize(Copy(payload)));
            registry.TryRegister(CatalogId, "packed-state-delta.push", payload =>
                ShooterPackedSnapshotCodec.Deserialize(Copy(payload)));
            RegisterMemoryPack<ulong>(registry, "state-hash.push");
            registry.TryRegister(CatalogId, "pure-state.push", payload =>
                ShooterPureStateSyncCodec.Deserialize(Copy(payload)));
            registry.TryRegister(CatalogId, "pure-state-delta.push", payload =>
                ShooterPureStateSyncCodec.Deserialize(Copy(payload)));
        }

        private static void RegisterMemoryPack<T>(
            ProtocolPayloadDecoderRegistry registry,
            string messageId)
        {
            registry.TryRegister(CatalogId, messageId, payload =>
            {
                if (payload.Array == null || payload.Count == 0) return default(T);
                return MemoryPackSerializer.Deserialize<T>(
                    new ReadOnlySpan<byte>(payload.Array, payload.Offset, payload.Count));
            });
        }

        private static byte[] Copy(ArraySegment<byte> payload)
        {
            if (payload.Array == null || payload.Count == 0) return Array.Empty<byte>();
            if (payload.Offset == 0 && payload.Count == payload.Array.Length) return payload.Array;

            var copy = new byte[payload.Count];
            Buffer.BlockCopy(payload.Array, payload.Offset, copy, 0, payload.Count);
            return copy;
        }
    }
}
