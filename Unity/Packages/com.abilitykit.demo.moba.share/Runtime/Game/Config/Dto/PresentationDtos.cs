using System;

namespace AbilityKit.Demo.Moba.Share.Config
{
    [Serializable]
    public sealed class VfxDTO
    {
        public int Id;
        public string Resource;
        public string ResourceKey;
        public int AssetKind;
        public int DurationMs;
        public int DelayMs;
        public int IntervalMs;
        public int PlayCount = 1;
        public bool Loop;
        public float Speed = 1f;
        public string ClipKey;
        public string PolicyGroup;
        public bool Unique;
        public int MinIntervalMs;
        public int MaxActive;
    }

    [Serializable]
    public sealed class PresentationTemplateDTO
    {
        public int Id;
        public string Name;

        public int Kind;
        public int AssetId;
        public int DefaultDurationMs;

        public int AssetKind;
        public string ResourceKey;
        public string ClipKey;
        public int DelayMs;
        public int IntervalMs;
        public int PlayCount = 1;
        public bool Loop;
        public float Speed = 1f;
        public string PolicyGroup;
        public bool Unique;
        public int MinIntervalMs;
        public int MaxActive;

        public int AttachMode;
        public string Socket;
        public bool Follow;

        public int StackPolicy;
        public int StopPolicy;

        public float Scale;
        public float ColorR;
        public float ColorG;
        public float ColorB;
        public float ColorA;
        public float Radius;
        public float OffsetX;
        public float OffsetY;
        public float OffsetZ;
    }
}
