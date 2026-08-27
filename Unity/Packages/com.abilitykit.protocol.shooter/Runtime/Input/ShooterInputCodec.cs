using System;
using AbilityKit.Protocol.Serialization;
using MemoryPack;

namespace AbilityKit.Protocol.Shooter
{
    public partial struct ShooterPlayerCommand
    {
        public ShooterPlayerCommand(int playerId, float moveX, float moveY, float aimX, float aimY, bool fire)
            : this(playerId, moveX, moveY, aimX, aimY, fire, ShooterPlayerAttackSlots.Primary)
        {
        }

        public ShooterPlayerCommand(int playerId, float moveX, float moveY, float aimX, float aimY, bool fire, int attackSlot)
        {
            PlayerId = playerId;
            MoveX = moveX;
            MoveY = moveY;
            AimX = aimX;
            AimY = aimY;
            Fire = fire;
            AttackSlot = ShooterPlayerAttackSlots.Normalize(attackSlot);
        }
    }

    public static class ShooterPlayerAttackSlots
    {
        public const int Primary = 0;
        public const int Spread = 1;
        public const int Twin = 2;

        public static int Normalize(int attackSlot)
        {
            return attackSlot switch
            {
                Spread => Spread,
                Twin => Twin,
                _ => Primary
            };
        }
    }

    public partial struct ShooterInputPayload
    {
        [MemoryPackConstructor]
        public ShooterInputPayload(ShooterPlayerCommand[] commands)
        {
            Commands = commands;
        }
    }

    public static class ShooterInputCodec
    {
        public static byte[] Serialize(ShooterPlayerCommand[] commands)
        {
            commands ??= Array.Empty<ShooterPlayerCommand>();
            var payload = new ShooterInputPayload(commands);
            return MemoryPackSerializer.Serialize(payload);
        }

        public static ShooterPlayerCommand[] Deserialize(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
            {
                return Array.Empty<ShooterPlayerCommand>();
            }

            var value = MemoryPackSerializer.Deserialize<ShooterInputPayload>(payload);
            return value.Commands ?? Array.Empty<ShooterPlayerCommand>();
        }
    }
}
