#nullable enable

using System;
using AbilityKit.Ability.Host.Extensions.Moba.Room;
using AbilityKit.Protocol.Catalog;
using AbilityKit.Protocol.Moba.StateSync;
using MemoryPack;

namespace AbilityKit.Protocol.Moba
{
    /// <summary>Registers the MOBA battle payload decoders owned by the MOBA protocol package.</summary>
    public static class MobaProtocolDecoderModule
    {
        public const string CatalogId = "abilitykit.moba.battle";

        public static void Register(ProtocolPayloadDecoderRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));

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
