using System;

namespace AbilityKit.Demo.Moba.Config.BattleDemo.MO
{
    public sealed class SpawnSummonActionTemplateMO
    {
        public int Id { get; }
        public string Name { get; }

        public int SummonId { get; }

        public int TargetMode { get; }
        public int PositionMode { get; }
        public int RotationMode { get; }
        public int OwnerKeyMode { get; }

        public int PatternMode { get; }
        public int PatternCount { get; }
        public float Spacing { get; }
        public float Radius { get; }
        public float StartAngleDeg { get; }
        public float ArcAngleDeg { get; }
        public float YawOffsetDeg { get; }

        public int RandomSeed { get; }
        public float RandomRadiusMin { get; }
        public float RandomRadiusMax { get; }

        public int GridRows { get; }
        public int GridCols { get; }
        public float GridSpacingX { get; }
        public float GridSpacingZ { get; }

        public int PerPointRotationMode { get; }
        public float PerPointYawOffsetDeg { get; }

        public int IntervalMs { get; }
        public int DurationMs { get; }
        public int TotalCount { get; }

        public string CasterKey { get; }
        public string TargetKey { get; }
        public int QueryTemplateId { get; }

        public string AimPosKey { get; }
        public string FixedPosKey { get; }
        public float FixedPosFallbackX { get; }
        public float FixedPosFallbackY { get; }
        public float FixedPosFallbackZ { get; }

        public SpawnSummonActionTemplateMO(SpawnSummonActionTemplateDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            Id = dto.Id;
            Name = dto.Name;

            SummonId = dto.SummonId;

            TargetMode = dto.TargetMode;
            PositionMode = dto.PositionMode;
            RotationMode = dto.RotationMode;
            OwnerKeyMode = dto.OwnerKeyMode;

            PatternMode = dto.PatternMode;
            PatternCount = dto.PatternCount;
            Spacing = dto.Spacing;
            Radius = dto.Radius;
            StartAngleDeg = dto.StartAngleDeg;
            ArcAngleDeg = dto.ArcAngleDeg;
            YawOffsetDeg = dto.YawOffsetDeg;

            RandomSeed = dto.RandomSeed;
            RandomRadiusMin = dto.RandomRadiusMin;
            RandomRadiusMax = dto.RandomRadiusMax;

            GridRows = dto.GridRows;
            GridCols = dto.GridCols;
            GridSpacingX = dto.GridSpacingX;
            GridSpacingZ = dto.GridSpacingZ;

            PerPointRotationMode = dto.PerPointRotationMode;
            PerPointYawOffsetDeg = dto.PerPointYawOffsetDeg;

            IntervalMs = dto.IntervalMs;
            DurationMs = dto.DurationMs;
            TotalCount = dto.TotalCount;

            CasterKey = dto.CasterKey;
            TargetKey = dto.TargetKey;
            QueryTemplateId = dto.QueryTemplateId;

            AimPosKey = dto.AimPosKey;
            FixedPosKey = dto.FixedPosKey;
            FixedPosFallbackX = dto.FixedPosFallbackX;
            FixedPosFallbackY = dto.FixedPosFallbackY;
            FixedPosFallbackZ = dto.FixedPosFallbackZ;
        }
    }

    public sealed class PresentationTemplateMO
    {
        public int Id { get; }
        public string Name { get; }

        public int Kind { get; }
        public int AssetId { get; }
        public int DefaultDurationMs { get; }

        public int AttachMode { get; }
        public string Socket { get; }
        public bool Follow { get; }

        public int StackPolicy { get; }
        public int StopPolicy { get; }

        public float Scale { get; }
        public float ColorR { get; }
        public float ColorG { get; }
        public float ColorB { get; }
        public float ColorA { get; }
        public float Radius { get; }
        public float OffsetX { get; }
        public float OffsetY { get; }
        public float OffsetZ { get; }

        public PresentationTemplateMO(PresentationTemplateDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            Id = dto.Id;
            Name = dto.Name;

            Kind = dto.Kind;
            AssetId = dto.AssetId;
            DefaultDurationMs = dto.DefaultDurationMs;

            AttachMode = dto.AttachMode;
            Socket = dto.Socket;
            Follow = dto.Follow;

            StackPolicy = dto.StackPolicy;
            StopPolicy = dto.StopPolicy;

            Scale = dto.Scale;
            ColorR = dto.ColorR;
            ColorG = dto.ColorG;
            ColorB = dto.ColorB;
            ColorA = dto.ColorA;
            Radius = dto.Radius;
            OffsetX = dto.OffsetX;
            OffsetY = dto.OffsetY;
            OffsetZ = dto.OffsetZ;
        }
    }
}
