using AbilityKit.Core.Math;

namespace AbilityKit.Ability.Impl.Triggering.SummonSpawning
{
    public sealed class SpawnSummonSpec
    {
        public enum OwnerKeyMode
        {
            None = 0,
            SourceContextId = 1,
            CasterActorId = 2,
        }

        public enum TargetMode
        {
            ExplicitTarget = 0,
            QueryTargets = 1,
        }

        public enum RotationMode
        {
            Caster = 0,
            AimDir = 1,
            FaceTarget = 2,
        }

        public enum PatternMode
        {
            Single = 0,
            Line = 1,
            Ring = 2,
            Arc = 3,
            RandomCircle = 4,
            Grid = 5,
        }

        public enum PerPointRotationMode
        {
            Inherit = 0,
            FaceCenter = 1,
            FaceOutward = 2,
            TangentCw = 3,
            TangentCcw = 4,
            FaceTargetActor = 5,
        }

        public enum PositionMode
        {
            Caster = 0,
            Target = 1,
            AimPos = 2,
            Fixed = 3,
        }

        public int SummonId;
        public TargetMode Target;
        public PositionMode Position;
        public RotationMode Rotation;
        public OwnerKeyMode OwnerKey;

        public PatternMode Pattern;
        public int PatternCount;
        public float Spacing;
        public float Radius;
        public float StartAngleDeg;
        public float ArcAngleDeg;
        public float YawOffsetDeg;

        public int RandomSeed;
        public float RandomRadiusMin;
        public float RandomRadiusMax;

        public int GridRows;
        public int GridCols;
        public float GridSpacingX;
        public float GridSpacingZ;

        public PerPointRotationMode PerPointRotation;
        public float PerPointYawOffsetDeg;

        public int IntervalMs;
        public int DurationMs;
        public int TotalCount;

        public string CasterKey;
        public string TargetKey;
        public int QueryTemplateId;

        public string AimPosKey;
        public string FixedPosKey;

        public Vec3 FixedPosFallback;
    }
}
