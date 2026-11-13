using System;
using AbilityKit.Core.Mathematics;
using AbilityKit.Combat.Projectile;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    /// <summary>
    /// Layouts understood by spawn_area_pattern. The action owns Area lifetime;
    /// this enum only selects deterministic geometry.
    /// </summary>
    public enum SpawnAreaPatternKind
    {
        PolygonEdges = 0,
        RadialPoints = 1
    }

    public readonly struct SpawnAreaPatternElement
    {
        public readonly Vec3 Offset;
        public readonly Vec3 Forward;
        public readonly OverlapShapeKind ShapeKind;
        public readonly float Width;
        public readonly float Length;

        public SpawnAreaPatternElement(
            in Vec3 offset,
            in Vec3 forward,
            OverlapShapeKind shapeKind,
            float width,
            float length)
        {
            Offset = offset;
            Forward = forward;
            ShapeKind = shapeKind;
            Width = width;
            Length = length;
        }
    }

    /// <summary>
    /// Pure deterministic pattern expansion. New layouts extend this resolver,
    /// while spawn and lifecycle handling remain in the shared Area action.
    /// </summary>
    public static class SpawnAreaPatternResolver
    {
        public static bool TryResolve(
            in SpawnAreaBatchArgs args,
            int index,
            out SpawnAreaPatternElement element,
            out string error)
        {
            element = default;
            error = null;
            if (index < 0 || index >= args.PatternCount)
            {
                error = $"pattern index must be in range 0..{args.PatternCount - 1}.";
                return false;
            }

            if (args.PatternCount <= 0 || args.PatternCount > 64)
            {
                error = "pattern_count must be in the range 1..64.";
                return false;
            }

            switch ((SpawnAreaPatternKind)args.PatternKind)
            {
                case SpawnAreaPatternKind.PolygonEdges:
                    return TryResolvePolygonEdge(in args, index, out element, out error);
                case SpawnAreaPatternKind.RadialPoints:
                    return TryResolveRadialPoint(in args, index, out element, out error);
                default:
                    error = $"unsupported pattern_kind={args.PatternKind}.";
                    return false;
            }
        }

        private static bool TryResolvePolygonEdge(
            in SpawnAreaBatchArgs args,
            int index,
            out SpawnAreaPatternElement element,
            out string error)
        {
            element = default;
            error = null;
            if (args.PatternCount < 3)
            {
                error = "polygon_edges requires pattern_count >= 3.";
                return false;
            }
            if (!IsFinitePositive(args.PatternApothem) || !IsFinitePositive(args.PatternEdgeWidth))
            {
                error = "polygon_edges requires finite positive pattern_apothem and pattern_edge_width.";
                return false;
            }

            var edgeLength = args.PatternEdgeLength > 0f
                ? args.PatternEdgeLength
                : (float)(2d * args.PatternApothem * Math.Tan(Math.PI / args.PatternCount));
            if (!IsFinitePositive(edgeLength))
            {
                error = "pattern_edge_length must resolve to a finite positive value.";
                return false;
            }

            var angleStep = args.PatternAngleStepDeg > 0f
                ? args.PatternAngleStepDeg
                : 360f / args.PatternCount;
            if (!IsFinitePositive(angleStep))
            {
                error = "pattern_angle_step_deg must resolve to a finite positive value.";
                return false;
            }

            var angle = (args.PatternStartAngleDeg + index * angleStep) * (float)(Math.PI / 180d);
            var normal = new Vec3(DeterministicMathBridge.Cos(angle), 0f, DeterministicMathBridge.Sin(angle));
            var tangent = new Vec3(-normal.Z, 0f, normal.X);
            element = new SpawnAreaPatternElement(
                normal * args.PatternApothem,
                tangent,
                OverlapShapeKind.Rectangle,
                args.PatternEdgeWidth,
                edgeLength);
            return true;
        }

        private static bool TryResolveRadialPoint(
            in SpawnAreaBatchArgs args,
            int index,
            out SpawnAreaPatternElement element,
            out string error)
        {
            element = default;
            error = null;
            if (!IsFinitePositive(args.PatternApothem))
            {
                error = "radial_points requires finite positive pattern_apothem.";
                return false;
            }

            var angleStep = args.PatternAngleStepDeg > 0f
                ? args.PatternAngleStepDeg
                : 360f / args.PatternCount;
            if (!IsFinitePositive(angleStep))
            {
                error = "pattern_angle_step_deg must resolve to a finite positive value.";
                return false;
            }

            var angle = (args.PatternStartAngleDeg + index * angleStep) * (float)(Math.PI / 180d);
            var radial = new Vec3(DeterministicMathBridge.Cos(angle), 0f, DeterministicMathBridge.Sin(angle));
            var shape = args.ShapeKind < (int)OverlapShapeKind.Circle || args.ShapeKind > (int)OverlapShapeKind.Ring
                ? OverlapShapeKind.Circle
                : (OverlapShapeKind)args.ShapeKind;
            element = new SpawnAreaPatternElement(radial * args.PatternApothem, radial, shape, args.Width, args.Length);
            return true;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool IsFinitePositive(float value) => IsFinite(value) && value > 0f;
    }
}
