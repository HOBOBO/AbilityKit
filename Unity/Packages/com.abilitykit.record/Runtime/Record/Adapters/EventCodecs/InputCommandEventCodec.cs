using MemoryPack;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Recording.Core;

namespace AbilityKit.Core.Recording.Adapters.EventCodecs
{
    public static class InputCommandEventCodec
    {
        public static byte[] Encode(in PlayerInputCommand cmd)
        {
            var payload = new InputCommandEventPayload(cmd.Player.Value, cmd.OpCode, cmd.Payload);
            return MemoryPackSerializer.Serialize(payload);
        }

        public static PlayerInputCommand Decode(FrameIndex frame, byte[] payload)
        {
            var p = MemoryPackSerializer.Deserialize<InputCommandEventPayload>(payload);
            return new PlayerInputCommand(frame, new PlayerId(p.PlayerId), p.OpCode, p.PayloadBytes);
        }

        public static void Write(IEventTrackWriter writer, in PlayerInputCommand cmd)
        {
            if (writer == null) return;
            writer.Append(cmd.Frame, RecordEventTypes.InputCommand, Encode(in cmd));
        }

        public static bool TryRead(in RecordEvent e, out PlayerInputCommand cmd)
        {
            if (e.EventType != RecordEventTypes.InputCommand)
            {
                cmd = default;
                return false;
            }

            cmd = Decode(e.Frame, e.Payload);
            return true;
        }
    }

    [MemoryPackable]
    public readonly partial struct InputCommandEventPayload
    {
        [MemoryPackOrder(0)] public readonly string PlayerId;
        [MemoryPackOrder(1)] public readonly int OpCode;
        [MemoryPackOrder(2)] public readonly byte[] PayloadBytes;

        [MemoryPackConstructor]
        public InputCommandEventPayload(string playerId, int opCode, byte[] payloadBytes)
        {
            PlayerId = playerId;
            OpCode = opCode;
            PayloadBytes = payloadBytes;
        }
    }
}
