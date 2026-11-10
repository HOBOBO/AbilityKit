using AbilityKit.Core.Mathematics;

namespace AbilityKit.Demo.Moba.Input
{
    public enum MobaActorMovementIntentKind
    {
        Hold = 0,
        Direction = 1,
        TargetPosition = 2,
    }

    /// <summary>
    /// Canonical logic intent shared by player, behavior-tree, and ML controllers.
    /// It describes a request only; validation and gameplay mutation belong to the input pipeline.
    /// </summary>
    public readonly struct MobaActorIntent
    {
        private MobaActorIntent(
            MobaActorMovementIntentKind movementKind,
            float moveX,
            float moveZ,
            Vec3 moveTarget,
            float moveStopRange,
            bool hasCast,
            int skillId,
            int skillSlot,
            int targetActorId,
            Vec3 aimPosition,
            Vec3 aimDirection)
        {
            MovementKind = movementKind;
            MoveX = moveX;
            MoveZ = moveZ;
            MoveTarget = moveTarget;
            MoveStopRange = moveStopRange;
            HasCast = hasCast;
            SkillId = skillId;
            SkillSlot = skillSlot;
            TargetActorId = targetActorId;
            AimPosition = aimPosition;
            AimDirection = aimDirection;
        }

        public MobaActorMovementIntentKind MovementKind { get; }
        public float MoveX { get; }
        public float MoveZ { get; }
        public Vec3 MoveTarget { get; }
        /// <summary>Planar stopping distance for target-position movement.</summary>
        public float MoveStopRange { get; }
        public bool HasCast { get; }
        public int SkillId { get; }
        public int SkillSlot { get; }
        public int TargetActorId { get; }
        public Vec3 AimPosition { get; }
        public Vec3 AimDirection { get; }

        public static MobaActorIntent Hold => new(
            MobaActorMovementIntentKind.Hold, 0f, 0f, Vec3.Zero, 0f,
            false, 0, 0, 0, Vec3.Zero, Vec3.Forward);

        public static MobaActorIntent MoveDirection(float x, float z)
        {
            return new MobaActorIntent(
                MobaActorMovementIntentKind.Direction,
                ClampUnit(x),
                ClampUnit(z),
                Vec3.Zero,
                0f,
                false, 0, 0, 0, Vec3.Zero, Vec3.Forward);
        }

        public static MobaActorIntent MoveTo(in Vec3 target, float stopRange = 0f)
        {
            return new MobaActorIntent(
                MobaActorMovementIntentKind.TargetPosition,
                0f, 0f, target,
                stopRange,
                false, 0, 0, 0, Vec3.Zero, Vec3.Forward);
        }

        public static MobaActorIntent Cast(
            int skillSlot,
            int skillId = 0,
            int targetActorId = 0,
            Vec3 aimPosition = default,
            Vec3 aimDirection = default)
        {
            return Hold.WithCast(skillSlot, skillId, targetActorId, in aimPosition, in aimDirection);
        }

        public MobaActorIntent WithCast(
            int skillSlot,
            int skillId = 0,
            int targetActorId = 0,
            in Vec3 aimPosition = default,
            in Vec3 aimDirection = default)
        {
            var direction = IsZero(in aimDirection) ? Vec3.Forward : aimDirection;
            return new MobaActorIntent(
                MovementKind, MoveX, MoveZ, MoveTarget, MoveStopRange,
                true, skillId, skillSlot, targetActorId, aimPosition, direction);
        }

        public bool HasFiniteMovement()
        {
            return MovementKind switch
            {
                MobaActorMovementIntentKind.Direction => IsFinite(MoveX) && IsFinite(MoveZ),
                MobaActorMovementIntentKind.TargetPosition =>
                    IsFinite(MoveTarget.X) && IsFinite(MoveTarget.Y) && IsFinite(MoveTarget.Z),
                _ => true,
            };
        }

        /// <summary>
        /// Validates every numeric value that can cross the logic input boundary.
        /// Constructors clamp directional movement, but aim/target values may come
        /// from behavior-tree or network payloads and must still be rejected here.
        /// </summary>
        public bool HasFiniteValues()
        {
            if (!HasFiniteMovement()) return false;
            if (!HasFinite(MoveTarget) || !HasFinite(AimPosition) || !HasFinite(AimDirection)) return false;
            return IsFinite(MoveStopRange) && MoveStopRange >= 0f && (!HasCast || SkillSlot > 0);
        }

        public bool IsValid() => HasFiniteValues();

        private static float ClampUnit(float value)
        {
            if (!IsFinite(value)) return 0f;
            return value < -1f ? -1f : value > 1f ? 1f : value;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool HasFinite(in Vec3 value) =>
            IsFinite(value.X) && IsFinite(value.Y) && IsFinite(value.Z);
        private static bool IsZero(in Vec3 value) =>
            value.X * value.X + value.Y * value.Y + value.Z * value.Z <= 0.00000001f;
    }
}
