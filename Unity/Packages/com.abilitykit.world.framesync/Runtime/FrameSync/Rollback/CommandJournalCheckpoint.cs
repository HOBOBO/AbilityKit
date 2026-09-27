using System;

namespace AbilityKit.Ability.FrameSync.Rollback
{
    public readonly struct CommandJournalCheckpoint
    {
        public const int CurrentVersion = 1;

        public CommandJournalCheckpoint(int version, int epoch, long nextOrder)
        {
            Version = version;
            Epoch = epoch;
            NextOrder = nextOrder;
        }

        public int Version { get; }

        public int Epoch { get; }

        public long NextOrder { get; }
    }

    public static class CommandJournalCheckpointCodec
    {
        private const int Magic = 0x434A5242; // "BRJC" in little-endian byte order.
        private const int PayloadLength = 20;

        public static byte[] Encode(in CommandJournalCheckpoint checkpoint)
        {
            var payload = new byte[PayloadLength];
            WriteInt32(payload, 0, Magic);
            WriteInt32(payload, 4, checkpoint.Version);
            WriteInt32(payload, 8, checkpoint.Epoch);
            WriteInt64(payload, 12, checkpoint.NextOrder);
            return payload;
        }

        public static CommandJournalCheckpoint Decode(byte[] payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            if (payload.Length != PayloadLength)
            {
                throw new ArgumentException(
                    $"Invalid command journal checkpoint length. expected={PayloadLength} actual={payload.Length}",
                    nameof(payload));
            }

            var magic = ReadInt32(payload, 0);
            if (magic != Magic)
            {
                throw new ArgumentException("Invalid command journal checkpoint magic.", nameof(payload));
            }

            var checkpoint = new CommandJournalCheckpoint(
                ReadInt32(payload, 4),
                ReadInt32(payload, 8),
                ReadInt64(payload, 12));
            if (checkpoint.Version != CommandJournalCheckpoint.CurrentVersion)
            {
                throw new NotSupportedException(
                    $"Unsupported command journal checkpoint version: {checkpoint.Version}");
            }

            return checkpoint;
        }

        private static void WriteInt32(byte[] bytes, int offset, int value)
        {
            unchecked
            {
                bytes[offset] = (byte)value;
                bytes[offset + 1] = (byte)(value >> 8);
                bytes[offset + 2] = (byte)(value >> 16);
                bytes[offset + 3] = (byte)(value >> 24);
            }
        }

        private static int ReadInt32(byte[] bytes, int offset)
        {
            unchecked
            {
                return bytes[offset]
                    | (bytes[offset + 1] << 8)
                    | (bytes[offset + 2] << 16)
                    | (bytes[offset + 3] << 24);
            }
        }

        private static void WriteInt64(byte[] bytes, int offset, long value)
        {
            unchecked
            {
                for (var i = 0; i < 8; i++)
                {
                    bytes[offset + i] = (byte)(value >> (i * 8));
                }
            }
        }

        private static long ReadInt64(byte[] bytes, int offset)
        {
            unchecked
            {
                ulong value = 0;
                for (var i = 0; i < 8; i++)
                {
                    value |= (ulong)bytes[offset + i] << (i * 8);
                }

                return (long)value;
            }
        }
    }
}
