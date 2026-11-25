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

        /*
         * 绝不能将活动状态机发布为另一帧的快照。
         */
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
