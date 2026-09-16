using AbilityKit.Ability.FrameSync;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.HFSM.Definition;

namespace AbilityKit.Game.Battle.Agent
{
    internal static class CharacterHfsmGatewayStateImporter
    {
        public static bool TryApply(in GatewayStateSyncSnapshot snapshot,
            MobaActorRegistry registry, StateMachineDefinition definition)
        {
            if (snapshot.SchemaVersion < 2 && snapshot.PayloadOpCode == 0) return true;
            if (registry == null || snapshot.Payload == null || snapshot.Payload.Length == 0 ||
                (snapshot.PayloadOpCode != MobaCharacterHfsmRollbackProvider.DefaultKey &&
                 snapshot.PayloadOpCode != MobaCharacterHfsmWireCodec.CompressedOpCode) ||
                (snapshot.SchemaVersion >= MobaCharacterHfsmWireCodec.CompressedSchemaVersion) !=
                (snapshot.PayloadOpCode == MobaCharacterHfsmWireCodec.CompressedOpCode))
                return false;

            new MobaCharacterHfsmRollbackProvider(registry, definition)
                .Import(new FrameIndex(snapshot.Frame),
                    MobaCharacterHfsmWireCodec.Decode(snapshot.PayloadOpCode, snapshot.Payload));
            return true;
        }
    }
}
