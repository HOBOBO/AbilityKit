using AbilityKit.Ability.Host;
using AbilityKit.Core.Generic;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using MemoryPack;

namespace AbilityKit.Ability.Share.Impl.Moba.CreateWorld
{
    [MemoryPackable]
    public readonly partial struct MobaCreateWorldInitPayload
    {
        [MemoryPackOrder(0), BinaryMember(0)] public readonly PlayerId LocalPlayerId;
        [MemoryPackOrder(1), BinaryMember(1)] public readonly MobaCreateWorldSpec Spec;
        [MemoryPackOrder(2), BinaryMember(2)] public readonly int OpCode;
        [MemoryPackOrder(3), BinaryMember(3)] public readonly byte[] Payload;

        [MemoryPackConstructor]
        public MobaCreateWorldInitPayload(PlayerId localPlayerId, in MobaCreateWorldSpec spec, int opCode, byte[] payload)
        {
            LocalPlayerId = localPlayerId;
            Spec = spec;
            OpCode = opCode;
            Payload = payload;
        }

        public EnterMobaGameReq ToEnterReq()
        {
            return Spec.ToEnterReq(LocalPlayerId, OpCode, Payload);
        }
    }
}
