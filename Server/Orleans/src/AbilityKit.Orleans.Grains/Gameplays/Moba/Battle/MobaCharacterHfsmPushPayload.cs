using AbilityKit.Ability.FrameSync;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.HFSM.Definition;
using AbilityKit.Orleans.Contracts.Battle;

namespace AbilityKit.Orleans.Grains.Gameplays.Moba.Battle;

internal static class MobaCharacterHfsmPushPayload
{
    public static StateSyncPush Attach(StateSyncPush push, MobaActorRegistry registry,
        StateMachineDefinition? definition, int frame)
    {
        ArgumentNullException.ThrowIfNull(push);
        ArgumentNullException.ThrowIfNull(registry);

        // A live machine must never be published as a snapshot of another frame.
        foreach (var id in registry.CopyActorIdsInOrder())
            if (registry.TryGet(id, out var actor) && actor.hasCharacterHfsm &&
                actor.characterHfsm.Runtime.Frame != frame) return push;

        var raw = new MobaCharacterHfsmRollbackProvider(registry, definition)
            .Export(new FrameIndex(frame));
        push.Payload = MobaCharacterHfsmWireCodec.Encode(raw, out var opCode);
        push.PayloadOpCode = opCode;
        push.SchemaVersion = opCode == MobaCharacterHfsmWireCodec.CompressedOpCode
            ? MobaCharacterHfsmWireCodec.CompressedSchemaVersion : 2;
        return push;
    }
}
