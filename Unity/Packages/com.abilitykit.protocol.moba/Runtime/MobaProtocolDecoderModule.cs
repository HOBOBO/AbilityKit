#nullable enable

using System;
using AbilityKit.Ability.Host.Extensions.Moba.Room;
using AbilityKit.Protocol.Catalog;
using AbilityKit.Protocol.Moba.Generated.GatewayFrameSync;
using AbilityKit.Protocol.Moba.StateSync;
using MemoryPack;

namespace AbilityKit.Protocol.Moba
{
    /// <summary>Registers the MOBA battle payload decoders owned by the MOBA protocol package.</summary>
    public static class MobaProtocolDecoderModule
    {
        public const string CatalogId = "abilitykit.moba.battle";

        public static WireSpectatorSubscribeRes DecodeSpectatorSubscribeResponse(ReadOnlySpan<byte> payload)
        {
            if (payload.Length == 0) throw new ArgumentException("Spectator subscribe response is empty.", nameof(payload));
            return MemoryPackSerializer.Deserialize<WireSpectatorSubscribeRes>(payload);
        }

        public static void Register(ProtocolPayloadDecoderRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            Register<WireSubmitFrameInputReq>(registry, "frame-sync-submit-input.request");
            Register<WireSubmitFrameInputRes>(registry, "frame-sync-submit-input.response");
            Register<WireCatchUpRequest>(registry, "frame-sync-catch-up.request");
            registry.TryRegister(CatalogId, "frame-sync-metrics.request", payload => payload.ToArray());
            Register<WireFrameSyncMetrics>(registry, "frame-sync-metrics.response");
            registry.TryRegister(CatalogId, "frame-sync-spectator-subscribe.request", payload =>
            {
                if (payload.Array == null || payload.Count != sizeof(ulong))
                    throw new FormatException("Spectator subscribe request must contain an 8-byte room ID.");
                return BitConverter.ToUInt64(payload.Array, payload.Offset);
            });
            Register<WireSpectatorSubscribeRes>(registry, "frame-sync-spectator-subscribe.response");
            Register<WireFramePushedPush>(registry, "frame-sync-frame.push");
            Register<WireCatchUpPayloadPush>(registry, "frame-sync-catch-up.push");

            Register<MobaMovePayload>(registry, "move-input.event");
            Register<SkillInputEvent>(registry, "skill-input.event");
            Register<MobaDebugSpawnUnitPayload>(registry, "debug-spawn-unit.event");
            Register<MobaDebugReplaceHeroPayload>(registry, "debug-replace-hero.event");
            Register<MobaRoomSnapshot>(registry, "lobby-snapshot.push");
            Register<MobaEnterGamePayload>(registry, "enter-game.push");
            Register<MobaActorTransformSnapshotPayload>(registry, "actor-transform.push");
            Register<MobaStateHashSnapshotPayload>(registry, "state-hash.push");
            Register<MobaActorSpawnSnapshotPayload>(registry, "actor-spawn.push");
            Register<MobaProjectileEventSnapshotPayload>(registry, "projectile-event.push");
            Register<MobaDamageEventSnapshotPayload>(registry, "damage-event.push");
            Register<MobaActorDespawnSnapshotPayload>(registry, "actor-despawn.push");
            Register<MobaAreaEventSnapshotPayload>(registry, "area-event.push");
            Register<MobaPresentationCueSnapshotPayload>(registry, "presentation-cue.push");
            Register<MobaSkillStateSnapshotPayload>(registry, "skill-state.push");
            Register<MobaPlayerHeroChangedSnapshotPayload>(registry, "player-hero-changed.push");
            Register<MobaActionAckPayload>(registry, "action-ack.push");
        }

        private static void Register<T>(ProtocolPayloadDecoderRegistry registry, string messageId)
        {
            registry.TryRegister(CatalogId, messageId, payload =>
            {
                if (payload.Array == null || payload.Count == 0) return default(T);
                return MemoryPackSerializer.Deserialize<T>(
                    new ReadOnlySpan<byte>(payload.Array, payload.Offset, payload.Count));
            });
        }
    }
}
