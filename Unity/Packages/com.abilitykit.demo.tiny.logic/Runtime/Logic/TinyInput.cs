using System;

namespace AbilityKit.Demo.Tiny
{
    public readonly struct TinyInput
    {
        public TinyInput(sbyte moveX, sbyte moveY, bool attack)
        {
            MoveX = moveX;
            MoveY = moveY;
            Attack = attack;
        }

        public sbyte MoveX { get; }
        public sbyte MoveY { get; }
        public bool Attack { get; }
        public bool IsValid => MoveX >= -1 && MoveX <= 1 && MoveY >= -1 && MoveY <= 1;

        public static TinyInput Decode(ReadOnlySpan<byte> payload)
        {
            if (payload.Length != 3 || payload[0] > 2 || payload[1] > 2 || payload[2] > 1)
                throw new ArgumentException("Tiny input must contain two ternary axes and one attack flag.", nameof(payload));

            return new TinyInput((sbyte)(payload[0] - 1), (sbyte)(payload[1] - 1), payload[2] == 1);
        }

        public byte[] Encode() => new[] { (byte)(MoveX + 1), (byte)(MoveY + 1), Attack ? (byte)1 : (byte)0 };
    }
}
