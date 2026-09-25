using System;
using System.Buffers.Binary;

namespace AbilityKit.Demo.Tiny
{
    public static class TinyBattleStateCodec
    {
        public const int PayloadOpCode = 31001;
        private const int HeaderSize = 5;
        private const int ActorSize = 20;

        public static byte[] Encode(TinyBattleState state)
        {
            if (state.Actors == null || state.Actors.Length < 1 || state.Actors.Length > 2)
                throw new ArgumentException("Tiny state requires one or two actors.", nameof(state));
            var bytes = new byte[HeaderSize + state.Actors.Length * ActorSize];
            BinaryPrimitives.WriteInt32LittleEndian(bytes, state.Frame);
            bytes[4] = (byte)state.Actors.Length;
            for (var i = 0; i < state.Actors.Length; i++)
            {
                var actor = state.Actors[i];
                var offset = HeaderSize + i * ActorSize;
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), actor.PlayerId);
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset + 4), actor.X);
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset + 8), actor.Y);
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset + 12), actor.Hp);
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset + 16), actor.CooldownFrames);
            }
            return bytes;
        }

        public static TinyBattleState Decode(byte[] payload)
        {
            if (payload == null || payload.Length < HeaderSize || payload[4] < 1 ||
                payload[4] > 2 || payload.Length != HeaderSize + payload[4] * ActorSize)
                throw new ArgumentException("Invalid Tiny state payload.", nameof(payload));
            var actors = new TinyActorState[payload[4]];
            for (var i = 0; i < actors.Length; i++)
            {
                var offset = HeaderSize + i * ActorSize;
                actors[i] = new TinyActorState(
                    BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(offset)),
                    BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(offset + 4)),
                    BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(offset + 8)),
                    BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(offset + 12)),
                    BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(offset + 16)));
            }
            var state = new TinyBattleState(BinaryPrimitives.ReadInt32LittleEndian(payload), actors);
            new TinyBattle().RestoreState(state);
            return state;
        }
    }
}
