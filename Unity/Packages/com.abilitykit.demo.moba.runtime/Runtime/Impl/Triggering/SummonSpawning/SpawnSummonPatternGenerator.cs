using AbilityKit.Core.Math;

namespace AbilityKit.Ability.Impl.Triggering.SummonSpawning
{
    public static class SpawnSummonPatternGenerator
    {
        public delegate void SpawnAt(in Vec3 pos, in Vec3 forward);

        private static uint Hash(uint x)
        {
            // xorshift-style hash
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            return x;
        }

        private static float Hash01(int seed, int index)
        {
            var x = (uint)(seed * 73856093) ^ (uint)(index * 19349663) ^ 0x9E3779B9u;
            var h = Hash(x);
            // 24-bit mantissa
            return (h & 0x00FFFFFFu) / 16777215f;
        }

        private static Vec3 NormalizeXZ(in Vec3 v)
        {
            var xz = new Vec3(v.X, 0f, v.Z);
            if (xz.SqrMagnitude <= 0.0001f) return Vec3.Forward;
            return xz.Normalized;
        }

        private static Vec3 ApplyYawDeg(in Vec3 forward, float yawDeg)
        {
            if (System.MathF.Abs(yawDeg) <= 0.001f) return forward;
            var yawRad = yawDeg * (System.MathF.PI / 180f);
            return Quat.FromAxisAngle(Vec3.Up, yawRad).Rotate(forward);
        }

        private static Vec3 ResolvePerPointForward(in SpawnSummonSpec spec, in Vec3 anchorPos, bool hasTargetPos, in Vec3 targetPos, in Vec3 pos, in Vec3 baseForward)
        {
            var f = baseForward;
            if (spec.PerPointRotation == SpawnSummonSpec.PerPointRotationMode.FaceCenter)
            {
                f = new Vec3(anchorPos.X - pos.X, 0f, anchorPos.Z - pos.Z);
            }
            else if (spec.PerPointRotation == SpawnSummonSpec.PerPointRotationMode.FaceOutward)
            {
                f = new Vec3(pos.X - anchorPos.X, 0f, pos.Z - anchorPos.Z);
            }
            else if (spec.PerPointRotation == SpawnSummonSpec.PerPointRotationMode.FaceTargetActor)
            {
                var tp = hasTargetPos ? targetPos : anchorPos;
                f = new Vec3(tp.X - pos.X, 0f, tp.Z - pos.Z);
            }
            else if (spec.PerPointRotation == SpawnSummonSpec.PerPointRotationMode.TangentCw || spec.PerPointRotation == SpawnSummonSpec.PerPointRotationMode.TangentCcw)
            {
                var radial = new Vec3(pos.X - anchorPos.X, 0f, pos.Z - anchorPos.Z);
                if (radial.SqrMagnitude <= 0.0001f)
                {
                    f = baseForward;
                }
                else
                {
                    var r = radial.Normalized;
                    f = spec.PerPointRotation == SpawnSummonSpec.PerPointRotationMode.TangentCw
                        ? Vec3.Cross(Vec3.Up, r)
                        : Vec3.Cross(r, Vec3.Up);
                }
            }

            f = NormalizeXZ(f);
            f = ApplyYawDeg(f, spec.PerPointYawOffsetDeg);
            return NormalizeXZ(f);
        }

        public static void Generate(in SpawnSummonSpec spec, in Vec3 anchorPos, bool hasTargetPos, in Vec3 targetPos, in Vec3 baseForward, SpawnAt spawn)
        {
            if (spawn == null) return;

            var f = baseForward;
            if (f.SqrMagnitude <= 0.0001f) f = Vec3.Forward;

            if (System.MathF.Abs(spec.YawOffsetDeg) > 0.001f)
            {
                var yawRad = spec.YawOffsetDeg * (System.MathF.PI / 180f);
                f = Quat.FromAxisAngle(Vec3.Up, yawRad).Rotate(f);
            }

            f = NormalizeXZ(f);

            var right = Vec3.Cross(Vec3.Up, f).Normalized;
            if (right.SqrMagnitude <= 0.0001f) right = Vec3.Right;

            var count = spec.PatternCount > 0 ? spec.PatternCount : 1;

            switch (spec.Pattern)
            {
                case SpawnSummonSpec.PatternMode.Line:
                {
                    var spacing = spec.Spacing;
                    if (spacing <= 0.0001f) spacing = 1f;
                    var half = (count - 1) * 0.5f;
                    for (int i = 0; i < count; i++)
                    {
                        var t = (i - half) * spacing;
                        var p = anchorPos + f * t;
                        var pf = spec.PerPointRotation == SpawnSummonSpec.PerPointRotationMode.Inherit ? ApplyYawDeg(f, spec.PerPointYawOffsetDeg) : ResolvePerPointForward(in spec, in anchorPos, hasTargetPos, in targetPos, in p, in f);
                        spawn(in p, in pf);
                    }
                    break;
                }
                case SpawnSummonSpec.PatternMode.Arc:
                {
                    // Arc in XZ plane around anchor, centered on base forward.
                    var radius = spec.Radius;
                    if (radius <= 0.0001f) radius = 1f;

                    var arcDeg = System.MathF.Abs(spec.ArcAngleDeg);
                    if (arcDeg <= 0.001f) arcDeg = 90f;

                    var startDeg = spec.StartAngleDeg - arcDeg * 0.5f;
                    var stepDeg = count > 1 ? (arcDeg / (count - 1)) : 0f;

                    for (int i = 0; i < count; i++)
                    {
                        var angDeg = startDeg + stepDeg * i;
                        var angRad = angDeg * (System.MathF.PI / 180f);
                        var cs = System.MathF.Cos(angRad);
                        var sn = System.MathF.Sin(angRad);
                        var offset = right * (sn * radius) + f * (cs * radius);
                        var p = anchorPos + offset;
                        var pf = spec.PerPointRotation == SpawnSummonSpec.PerPointRotationMode.Inherit ? ApplyYawDeg(f, spec.PerPointYawOffsetDeg) : ResolvePerPointForward(in spec, in anchorPos, hasTargetPos, in targetPos, in p, in f);
                        spawn(in p, in pf);
                    }
                    break;
                }
                case SpawnSummonSpec.PatternMode.RandomCircle:
                {
                    var rMin = spec.RandomRadiusMin;
                    var rMax = spec.RandomRadiusMax;
                    if (rMax <= 0.0001f)
                    {
                        rMax = spec.Radius;
                        if (rMax <= 0.0001f) rMax = 1f;
                    }
                    if (rMin < 0f) rMin = 0f;
                    if (rMin > rMax) { var tmp = rMin; rMin = rMax; rMax = tmp; }

                    for (int i = 0; i < count; i++)
                    {
                        // deterministic: seed + i
                        var u = Hash01(spec.RandomSeed, i * 2);
                        var v = Hash01(spec.RandomSeed, i * 2 + 1);
                        var ang = v * (System.MathF.PI * 2f);

                        // uniform area distribution
                        var rr = System.MathF.Sqrt(u);
                        var radius = rMin + (rMax - rMin) * rr;

                        var cs = System.MathF.Cos(ang);
                        var sn = System.MathF.Sin(ang);
                        var offset = right * (cs * radius) + f * (sn * radius);
                        var p = anchorPos + offset;
                        var pf = spec.PerPointRotation == SpawnSummonSpec.PerPointRotationMode.Inherit ? ApplyYawDeg(f, spec.PerPointYawOffsetDeg) : ResolvePerPointForward(in spec, in anchorPos, hasTargetPos, in targetPos, in p, in f);
                        spawn(in p, in pf);
                    }
                    break;
                }
                case SpawnSummonSpec.PatternMode.Grid:
                {
                    var rows = spec.GridRows;
                    var cols = spec.GridCols;
                    if (rows <= 0 || cols <= 0)
                    {
                        // fallback: try to make a near-square grid with patternCount.
                        var n = count;
                        cols = (int)System.MathF.Ceiling(System.MathF.Sqrt(n));
                        if (cols <= 0) cols = 1;
                        rows = (int)System.MathF.Ceiling(n / (float)cols);
                        if (rows <= 0) rows = 1;
                    }

                    var sx = spec.GridSpacingX;
                    var sz = spec.GridSpacingZ;
                    if (sx <= 0.0001f) sx = spec.Spacing > 0.0001f ? spec.Spacing : 1f;
                    if (sz <= 0.0001f) sz = spec.Spacing > 0.0001f ? spec.Spacing : 1f;

                    var total = rows * cols;
                    var take = count;
                    if (take > total) take = total;

                    var halfX = (cols - 1) * 0.5f;
                    var halfZ = (rows - 1) * 0.5f;

                    var k = 0;
                    for (int r = 0; r < rows; r++)
                    {
                        for (int c = 0; c < cols; c++)
                        {
                            if (k >= take) break;
                            var dx = (c - halfX) * sx;
                            var dz = (r - halfZ) * sz;
                            var p = anchorPos + right * dx + f * dz;
                            var pf = spec.PerPointRotation == SpawnSummonSpec.PerPointRotationMode.Inherit ? ApplyYawDeg(f, spec.PerPointYawOffsetDeg) : ResolvePerPointForward(in spec, in anchorPos, hasTargetPos, in targetPos, in p, in f);
                            spawn(in p, in pf);
                            k++;
                        }
                        if (k >= take) break;
                    }

                    break;
                }
                case SpawnSummonSpec.PatternMode.Ring:
                {
                    var radius = spec.Radius;
                    if (radius <= 0.0001f) radius = 1f;

                    var startRad = spec.StartAngleDeg * (System.MathF.PI / 180f);
                    for (int i = 0; i < count; i++)
                    {
                        var ang = startRad + (i * (System.MathF.PI * 2f / count));
                        var cs = System.MathF.Cos(ang);
                        var sn = System.MathF.Sin(ang);
                        var offset = right * (cs * radius) + f * (sn * radius);
                        var p = anchorPos + offset;
                        var pf = spec.PerPointRotation == SpawnSummonSpec.PerPointRotationMode.Inherit ? ApplyYawDeg(f, spec.PerPointYawOffsetDeg) : ResolvePerPointForward(in spec, in anchorPos, hasTargetPos, in targetPos, in p, in f);
                        spawn(in p, in pf);
                    }
                    break;
                }
                case SpawnSummonSpec.PatternMode.Single:
                default:
                {
                    var pf = spec.PerPointRotation == SpawnSummonSpec.PerPointRotationMode.Inherit ? ApplyYawDeg(f, spec.PerPointYawOffsetDeg) : ResolvePerPointForward(in spec, in anchorPos, hasTargetPos, in targetPos, in anchorPos, in f);
                    spawn(in anchorPos, in pf);
                    break;
                }
            }
        }

        public static void Generate(in SpawnSummonSpec spec, in Vec3 anchorPos, in Vec3 baseForward, SpawnAt spawn)
        {
            Generate(in spec, in anchorPos, hasTargetPos: false, targetPos: default, in baseForward, spawn);
        }
    }
}
