using AbilityKit.Ability.Host;
using AbilityKit.Protocol.Moba;
using MemoryPack;

namespace AbilityKit.Protocol.Moba.CreateWorld
{
    [MemoryPackable]
    public readonly partial struct MobaCreateWorldInitPayload
    {
        [MemoryPackOrder(0)] public readonly PlayerId LocalPlayerId;
        [MemoryPackOrder(1)] public readonly MobaCreateWorldSpec Spec;
        [MemoryPackOrder(2)] public readonly int OpCode;
        [MemoryPackOrder(3)] public readonly byte[] Payload;

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

        public MobaGameStartSpec ToGameStartSpec()
        {
            var req = ToEnterReq();
            return new MobaGameStartSpec(in req);
        }
    }
}
