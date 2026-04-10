using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Generic;
using AbilityKit.Core.Common.Record.Core;

namespace AbilityKit.Core.Common.Record.Adapters.EventCodecs
{
    public static class InputCommandEventCodec
    {
        public static byte[] Encode(in PlayerInputCommand cmd)
        {
            var payload = new Payload(cmd.Player.Value, cmd.OpCode, cmd.Payload);
            return BinaryObjectCodec.Encode(payload);
        }

        public static PlayerInputCommand Decode(FrameIndex frame, byte[] payload)
        {
            var p = BinaryObjectCodec.Decode<Payload>(payload);
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

        public readonly struct Payload
        {
            [BinaryMember(0)] public readonly string PlayerId;
            [BinaryMember(1)] public readonly int OpCode;
            [BinaryMember(2)] public readonly byte[] PayloadBytes;

            public Payload(string playerId, int opCode, byte[] payload)
            {
                PlayerId = playerId;
                OpCode = opCode;
                PayloadBytes = payload;
            }
        }
    }
}
