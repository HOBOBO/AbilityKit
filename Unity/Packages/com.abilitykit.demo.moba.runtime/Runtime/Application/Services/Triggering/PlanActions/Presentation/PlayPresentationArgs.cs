using System;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public enum PresentationTargetMode
    {
        Explicit = 0,
        QueryTemplate = 1,
        Source = 2,
        Target = 3,
        Self = 4,
        PayloadAttacker = 5,
        PayloadTarget = 6,
        Position = 7,
        /// <summary>
        /// 仅从投射物生成事件读取世界位置与发射方向，播放一次且不跟随。
        /// </summary>
        ProjectileLaunchPose = 8,
    }

    public struct PlayPresentationArgs
    {
        public int TemplateId;
        public int TargetMode;
        public string RequestKey;
        public int DurationMs;
        public bool Stop;
        public float X;
        public float Y;
        public float Z;
        public float Scale;
        public float Radius;

        public PlayPresentationArgs(int templateId)
        {
            TemplateId = templateId;
            TargetMode = 0;
            RequestKey = null;
            DurationMs = 0;
            Stop = false;
            X = 0;
            Y = 0;
            Z = 0;
            Scale = 1;
            Radius = 0;
        }
    }
}
