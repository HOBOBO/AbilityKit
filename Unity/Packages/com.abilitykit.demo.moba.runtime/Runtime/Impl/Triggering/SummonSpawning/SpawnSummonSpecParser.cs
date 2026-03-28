using System;
using System.Collections.Generic;
using AbilityKit.Ability.Impl.Triggering;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.BattleDemo.MO;
using AbilityKit.Ability.Triggering.Definitions;

namespace AbilityKit.Ability.Impl.Triggering.SummonSpawning
{
    public static class SpawnSummonSpecParser
    {
        public static SpawnSummonSpec FromTemplate(SpawnSummonActionTemplateDTO dto)
        {
            if (dto == null) return null;

            var spec = new SpawnSummonSpec
            {
                SummonId = dto.SummonId,
                Target = (SpawnSummonSpec.TargetMode)dto.TargetMode,
                Position = (SpawnSummonSpec.PositionMode)dto.PositionMode,
                Rotation = (SpawnSummonSpec.RotationMode)dto.RotationMode,
                OwnerKey = (SpawnSummonSpec.OwnerKeyMode)dto.OwnerKeyMode,

                Pattern = (SpawnSummonSpec.PatternMode)dto.PatternMode,
                PatternCount = dto.PatternCount,
                Spacing = dto.Spacing,
                Radius = dto.Radius,
                StartAngleDeg = dto.StartAngleDeg,
                ArcAngleDeg = dto.ArcAngleDeg,
                YawOffsetDeg = dto.YawOffsetDeg,

                RandomSeed = dto.RandomSeed,
                RandomRadiusMin = dto.RandomRadiusMin,
                RandomRadiusMax = dto.RandomRadiusMax,

                GridRows = dto.GridRows,
                GridCols = dto.GridCols,
                GridSpacingX = dto.GridSpacingX,
                GridSpacingZ = dto.GridSpacingZ,

                PerPointRotation = (SpawnSummonSpec.PerPointRotationMode)dto.PerPointRotationMode,
                PerPointYawOffsetDeg = dto.PerPointYawOffsetDeg,

                IntervalMs = dto.IntervalMs,
                DurationMs = dto.DurationMs,
                TotalCount = dto.TotalCount,

                CasterKey = dto.CasterKey,
                TargetKey = dto.TargetKey,
                QueryTemplateId = dto.QueryTemplateId,

                AimPosKey = dto.AimPosKey,
                FixedPosKey = dto.FixedPosKey,
                FixedPosFallback = new AbilityKit.Ability.Share.Math.Vec3(dto.FixedPosFallbackX, dto.FixedPosFallbackY, dto.FixedPosFallbackZ),
            };

            if (spec.PatternCount <= 0) spec.PatternCount = 1;
            return spec;
        }

        public static void ApplyArgs(SpawnSummonSpec spec, IReadOnlyDictionary<string, object> args)
        {
            if (spec == null || args == null) return;

            if (args.TryGetValue("summonId", out var sidObj) && sidObj != null)
            {
                spec.SummonId = sidObj is int si ? si : sidObj is long sl ? (int)sl : Convert.ToInt32(sidObj);
            }

            if (args.TryGetValue("targetMode", out var tmObj) && tmObj != null)
            {
                spec.Target = TriggerActionArgUtil.ParseEnum(tmObj, SpawnSummonSpec.TargetMode.ExplicitTarget);
            }

            if (args.TryGetValue("positionMode", out var pmObj) && pmObj != null)
            {
                spec.Position = TriggerActionArgUtil.ParseEnum(pmObj, SpawnSummonSpec.PositionMode.Caster);
            }

            if (args.TryGetValue("rotationMode", out var rmObj) && rmObj != null)
            {
                spec.Rotation = TriggerActionArgUtil.ParseEnum(rmObj, SpawnSummonSpec.RotationMode.Caster);
            }

            if (args.TryGetValue("ownerKeyMode", out var okObj) && okObj != null)
            {
                spec.OwnerKey = TriggerActionArgUtil.ParseEnum(okObj, SpawnSummonSpec.OwnerKeyMode.SourceContextId);
            }

            if (args.TryGetValue("patternMode", out var patObj) && patObj != null)
            {
                spec.Pattern = TriggerActionArgUtil.ParseEnum(patObj, SpawnSummonSpec.PatternMode.Single);
            }

            if (args.TryGetValue("patternCount", out var pcObj) && pcObj != null)
            {
                spec.PatternCount = pcObj is int pci ? pci : pcObj is long pcl ? (int)pcl : Convert.ToInt32(pcObj);
            }
            if (spec.PatternCount <= 0) spec.PatternCount = 1;

            if (args.TryGetValue("spacing", out var spObj) && spObj != null)
            {
                spec.Spacing = spObj is float sf ? sf : spObj is int si2 ? si2 : Convert.ToSingle(spObj);
            }

            if (args.TryGetValue("radius", out var raObj) && raObj != null)
            {
                spec.Radius = raObj is float rf ? rf : raObj is int ri ? ri : Convert.ToSingle(raObj);
            }

            if (args.TryGetValue("startAngleDeg", out var saObj) && saObj != null)
            {
                spec.StartAngleDeg = saObj is float sf2 ? sf2 : saObj is int si3 ? si3 : Convert.ToSingle(saObj);
            }

            if (args.TryGetValue("arcAngleDeg", out var aaObj) && aaObj != null)
            {
                spec.ArcAngleDeg = aaObj is float af ? af : aaObj is int ai ? ai : Convert.ToSingle(aaObj);
            }

            if (args.TryGetValue("yawOffsetDeg", out var yoObj) && yoObj != null)
            {
                spec.YawOffsetDeg = yoObj is float yf ? yf : yoObj is int yi ? yi : Convert.ToSingle(yoObj);
            }

            if (args.TryGetValue("randomSeed", out var rsObj) && rsObj != null)
            {
                spec.RandomSeed = rsObj is int ri ? ri : rsObj is long rl ? (int)rl : Convert.ToInt32(rsObj);
            }

            if (args.TryGetValue("randomRadiusMin", out var rminObj) && rminObj != null)
            {
                spec.RandomRadiusMin = rminObj is float rf ? rf : rminObj is int ri2 ? ri2 : Convert.ToSingle(rminObj);
            }

            if (args.TryGetValue("randomRadiusMax", out var rmaxObj) && rmaxObj != null)
            {
                spec.RandomRadiusMax = rmaxObj is float rf2 ? rf2 : rmaxObj is int ri3 ? ri3 : Convert.ToSingle(rmaxObj);
            }

            if (args.TryGetValue("gridRows", out var grObj) && grObj != null)
            {
                spec.GridRows = grObj is int gi ? gi : grObj is long gl ? (int)gl : Convert.ToInt32(grObj);
            }

            if (args.TryGetValue("gridCols", out var gcObj) && gcObj != null)
            {
                spec.GridCols = gcObj is int gi2 ? gi2 : gcObj is long gl2 ? (int)gl2 : Convert.ToInt32(gcObj);
            }

            if (args.TryGetValue("gridSpacingX", out var gsxObj) && gsxObj != null)
            {
                spec.GridSpacingX = gsxObj is float gf ? gf : gsxObj is int gi3 ? gi3 : Convert.ToSingle(gsxObj);
            }

            if (args.TryGetValue("gridSpacingZ", out var gszObj) && gszObj != null)
            {
                spec.GridSpacingZ = gszObj is float gf2 ? gf2 : gszObj is int gi4 ? gi4 : Convert.ToSingle(gszObj);
            }

            if (args.TryGetValue("perPointRotationMode", out var pprObj) && pprObj != null)
            {
                spec.PerPointRotation = TriggerActionArgUtil.ParseEnum(pprObj, SpawnSummonSpec.PerPointRotationMode.Inherit);
            }

            if (args.TryGetValue("perPointYawOffsetDeg", out var pyoObj) && pyoObj != null)
            {
                spec.PerPointYawOffsetDeg = pyoObj is float pf ? pf : pyoObj is int pi ? pi : Convert.ToSingle(pyoObj);
            }

            if (args.TryGetValue("intervalMs", out var itObj) && itObj != null)
            {
                spec.IntervalMs = itObj is int ii ? ii : itObj is long il ? (int)il : Convert.ToInt32(itObj);
            }

            if (args.TryGetValue("durationMs", out var duObj) && duObj != null)
            {
                spec.DurationMs = duObj is int di ? di : duObj is long dl ? (int)dl : Convert.ToInt32(duObj);
            }

            if (args.TryGetValue("totalCount", out var tcObj) && tcObj != null)
            {
                spec.TotalCount = tcObj is int ti ? ti : tcObj is long tl ? (int)tl : Convert.ToInt32(tcObj);
            }

            if (args.TryGetValue("casterKey", out var ckObj) && ckObj is string cks && !string.IsNullOrEmpty(cks)) spec.CasterKey = cks;
            if (args.TryGetValue("targetKey", out var tkObj) && tkObj is string tks && !string.IsNullOrEmpty(tks)) spec.TargetKey = tks;

            if (args.TryGetValue("queryTemplateId", out var qObj) && qObj != null)
            {
                if (qObj is int qi) spec.QueryTemplateId = qi;
                else if (qObj is long ql) spec.QueryTemplateId = (int)ql;
                else if (qObj is string qs && int.TryParse(qs, out var parsed)) spec.QueryTemplateId = parsed;
                else spec.QueryTemplateId = Convert.ToInt32(qObj);
            }

            if (args.TryGetValue("aimPosKey", out var apObj) && apObj is string aps && !string.IsNullOrEmpty(aps)) spec.AimPosKey = aps;
            if (args.TryGetValue("fixedPosKey", out var fpObj) && fpObj is string fps && !string.IsNullOrEmpty(fps)) spec.FixedPosKey = fps;

            if (args.TryGetValue("fixedPosFallback", out var fpfObj) && fpfObj is AbilityKit.Ability.Share.Math.Vec3 v3)
            {
                spec.FixedPosFallback = v3;
            }
        }

        public static SpawnSummonSpec FromDef(ActionDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            var args = def.Args;
            if (args == null) throw new InvalidOperationException("spawn_summon requires args");

            var spec = new SpawnSummonSpec
            {
                Target = SpawnSummonSpec.TargetMode.ExplicitTarget,
                Position = SpawnSummonSpec.PositionMode.Caster,
                Rotation = SpawnSummonSpec.RotationMode.Caster,
                OwnerKey = SpawnSummonSpec.OwnerKeyMode.SourceContextId,
                Pattern = SpawnSummonSpec.PatternMode.Single,
                PatternCount = 1,
                PerPointRotation = SpawnSummonSpec.PerPointRotationMode.Inherit,
            };

            ApplyArgs(spec, args);
            return spec;
        }
    }
}
