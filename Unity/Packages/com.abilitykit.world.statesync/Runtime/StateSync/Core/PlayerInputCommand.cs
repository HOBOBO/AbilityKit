using System;

namespace AbilityKit.Ability.StateSync
{
    public readonly struct PlayerInputCommand
    {
        public int PlayerId { get; }
        public int Frame { get; }
        public long Timestamp { get; }
        public InputFlags Flags { get; }
        public float MoveX { get; }
        public float MoveY { get; }
        public float LookX { get; }
        public float LookY { get; }
        public uint TriggerRequests { get; }
        public int TargetId { get; }
        public byte[] ExtraData { get; }

        public PlayerInputCommand(
            int playerId,
            int frame,
            long timestamp,
            InputFlags flags,
            float moveX = 0f,
            float moveY = 0f,
            float lookX = 0f,
            float lookY = 0f,
            uint triggerRequests = 0,
            int targetId = 0,
            byte[] extraData = null)
        {
            PlayerId = playerId;
            Frame = frame;
            Timestamp = timestamp;
            Flags = flags;
            MoveX = moveX;
            MoveY = moveY;
            LookX = lookX;
            LookY = lookY;
            TriggerRequests = triggerRequests;
            TargetId = targetId;
            ExtraData = extraData;
        }

        public bool HasFlag(InputFlags flag) => (Flags & flag) != 0;
    }

    [Flags]
    public enum InputFlags : uint
    {
        None = 0,
        MoveForward = 1 << 0,
        MoveBackward = 1 << 1,
        MoveLeft = 1 << 2,
        MoveRight = 1 << 3,
        Jump = 1 << 4,
        Crouch = 1 << 5,
        Sprint = 1 << 6,
        Aim = 1 << 7,
        Fire = 1 << 8,
        Reload = 1 << 9,
        UseAbility1 = 1 << 10,
        UseAbility2 = 1 << 11,
        UseUltimate = 1 << 12,
        Interact = 1 << 13,
    }
}
